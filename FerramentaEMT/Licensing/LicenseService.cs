using System;
using System.Runtime.Versioning;
using FerramentaEMT.Infrastructure;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Resultado consolidado do estado atual de licenciamento.
    /// Sempre que precisar tomar decisao (liberar comando, mostrar tela, etc.),
    /// chame <see cref="LicenseService.GetCurrentState"/> e use este objeto.
    /// </summary>
    public sealed class LicenseState
    {
        public LicenseStatus Status { get; init; }
        public DateTime? ExpiresAtUtc { get; init; }
        public int DiasRestantes { get; init; }
        public string Email { get; init; }
        public string MensagemAmigavel { get; init; }

        /// <summary>True se o usuario pode rodar comandos pagos.</summary>
        public bool PodeUsar => Status == LicenseStatus.Valid || Status == LicenseStatus.Trial;
    }

    /// <summary>
    /// Servico principal de licenciamento. Ponto de entrada para:
    /// - Saber se o plugin esta liberado
    /// - Ativar uma chave nova
    /// - Iniciar/checar trial
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class LicenseService
    {
        // Periodo de teste em dias.
        public const int TrialDurationDays = 14;

        // Cache em memoria — invalidado quando o usuario ativa uma chave nova.
        private static LicenseState _cached;

        /// <summary>
        /// Inicializa o servico no startup do plugin.
        /// Se nao existe trial nem licenca, comeca o trial automaticamente.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                var stored = LicenseStore.LoadLicense();
                var trial = LicenseStore.GetTrialStartUtc();

                if (stored == null && !trial.HasValue)
                {
                    LicenseStore.StartTrialIfNotStarted();
                    Logger.Info("[License] Primeira execucao — trial de {Dias} dias iniciado", TrialDurationDays);
                }

                LicenseState state = ComputeState();
                Logger.Info("[License] Estado inicial: {Status} (dias restantes: {Dias})",
                    state.Status, state.DiasRestantes);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[License] Falha ao inicializar servico de licenca");
            }
        }

        /// <summary>
        /// Tenta ativar uma chave de licenca. Retorna o estado pos-tentativa.
        /// Se o token for invalido, devolve um state com Status apropriado e mensagem.
        /// </summary>
        public static LicenseState Activate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new LicenseState
                {
                    Status = LicenseStatus.Tampered,
                    MensagemAmigavel = "Chave em branco. Cole a chave que voce recebeu por email.",
                };
            }

            LicensePayload payload = KeySigner.Verify(token.Trim());
            if (payload == null)
            {
                Logger.Warn("[License] Tentativa de ativacao com chave invalida");
                return new LicenseState
                {
                    Status = LicenseStatus.Tampered,
                    MensagemAmigavel = "Chave invalida. Verifique se copiou tudo (sem espacos extras).",
                };
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (payload.IsExpired(nowUtc))
            {
                Logger.Warn("[License] Chave expirada (expirou em {Data})", payload.ExpiresAtUtc);
                return new LicenseState
                {
                    Status = LicenseStatus.Expired,
                    Email = payload.Email,
                    ExpiresAtUtc = payload.ExpiresAtUtc,
                    MensagemAmigavel = $"Esta chave expirou em {payload.ExpiresAtUtc:dd/MM/yyyy}. Renove para continuar.",
                };
            }

            // OK — ativa: persiste com fingerprint da maquina atual
            string fingerprint = MachineFingerprint.Current();
            LicenseStore.SaveLicense(token.Trim(), fingerprint);
            InvalidateCache();

            Logger.Info("[License] Licenca ativada para {Email}, expira em {Data}",
                payload.Email, payload.ExpiresAtUtc);

            return new LicenseState
            {
                Status = LicenseStatus.Valid,
                Email = payload.Email,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                DiasRestantes = payload.DiasRestantes(nowUtc),
                MensagemAmigavel = $"Licenca ativada com sucesso! Valida ate {payload.ExpiresAtUtc:dd/MM/yyyy}.",
            };
        }

        /// <summary>
        /// Le o estado atual (com cache em memoria).
        /// Use sempre que precisar decidir se pode liberar uma feature.
        /// </summary>
        public static LicenseState GetCurrentState()
        {
            if (_cached != null) return _cached;
            _cached = ComputeState();
            return _cached;
        }

        /// <summary>Forca o proximo GetCurrentState a recalcular.</summary>
        public static void InvalidateCache() => _cached = null;

        private static LicenseState ComputeState()
        {
            DateTime nowUtc = DateTime.UtcNow;

            // 1) Tem licenca paga?
            var stored = LicenseStore.LoadLicense();
            if (stored.HasValue)
            {
                LicensePayload payload = KeySigner.Verify(stored.Value.Token);
                if (payload == null)
                {
                    return new LicenseState
                    {
                        Status = LicenseStatus.Tampered,
                        MensagemAmigavel = "Arquivo de licenca corrompido. Reative com sua chave.",
                    };
                }

                string fingerprintAtual = MachineFingerprint.Current();
                if (!string.Equals(stored.Value.Fingerprint, fingerprintAtual, StringComparison.Ordinal))
                {
                    return new LicenseState
                    {
                        Status = LicenseStatus.WrongMachine,
                        Email = payload.Email,
                        ExpiresAtUtc = payload.ExpiresAtUtc,
                        MensagemAmigavel = "Esta licenca foi ativada em outra maquina. " +
                                           "Entre em contato para liberar reativacao.",
                    };
                }

                if (payload.IsExpired(nowUtc))
                {
                    return new LicenseState
                    {
                        Status = LicenseStatus.Expired,
                        Email = payload.Email,
                        ExpiresAtUtc = payload.ExpiresAtUtc,
                        DiasRestantes = 0,
                        MensagemAmigavel = $"Sua licenca expirou em {payload.ExpiresAtUtc:dd/MM/yyyy}. Renove para continuar.",
                    };
                }

                return new LicenseState
                {
                    Status = LicenseStatus.Valid,
                    Email = payload.Email,
                    ExpiresAtUtc = payload.ExpiresAtUtc,
                    DiasRestantes = payload.DiasRestantes(nowUtc),
                };
            }

            // 2) Sem licenca paga — checar trial
            DateTime? trialStart = LicenseStore.GetTrialStartUtc();
            if (!trialStart.HasValue)
            {
                return new LicenseState
                {
                    Status = LicenseStatus.NotActivated,
                    MensagemAmigavel = "Plugin nao ativado. Inicie o trial gratuito ou ative sua licenca.",
                };
            }

            DateTime trialEnd = trialStart.Value.AddDays(TrialDurationDays);
            if (nowUtc >= trialEnd)
            {
                return new LicenseState
                {
                    Status = LicenseStatus.TrialExpired,
                    ExpiresAtUtc = trialEnd,
                    DiasRestantes = 0,
                    MensagemAmigavel = $"Seu periodo de teste de {TrialDurationDays} dias acabou. Ative uma licenca para continuar.",
                };
            }

            int diasRestTrial = (int)Math.Ceiling((trialEnd - nowUtc).TotalDays);
            return new LicenseState
            {
                Status = LicenseStatus.Trial,
                ExpiresAtUtc = trialEnd,
                DiasRestantes = diasRestTrial,
                MensagemAmigavel = $"Periodo de teste — {diasRestTrial} dia(s) restantes.",
            };
        }
    }
}
