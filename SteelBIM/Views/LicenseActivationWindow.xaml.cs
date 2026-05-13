using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Infrastructure.Update;
using FerramentaEMT.Licensing;
using FerramentaEMT.Models.Privacy;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela de ativacao de licenca. Usuario cola a chave, clica Ativar.
    /// Tambem mostra o estado atual e o fingerprint da maquina (para suporte).
    /// PR-2: rodape ganha botao "Verificar atualizacoes" — abre PrivacyConsentWindow
    /// se ainda nao houve consent, senao consulta GitHub e mostra resultado.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class LicenseActivationWindow : Window
    {
        public LicenseActivationWindow()
        {
            InitializeComponent();
            this.InitializeFerramentaWindow();

            txtFingerprint.Text = MachineFingerprint.Current();

            btnAtivar.Click += BtnAtivar_Click;
            btnFechar.Click += (_, __) => { DialogResult = false; };
            btnCopiarFingerprint.Click += BtnCopiarFingerprint_Click;
            btnVerificarAtualizacoes.Click += BtnVerificarAtualizacoes_Click;

            AtualizarStatus();
        }

        private void AtualizarStatus()
        {
            LicenseState state = LicenseService.GetCurrentState();
            txtStatusTitle.Text = $"Estado atual: {DescreverStatus(state.Status)}";
            txtStatusBody.Text = state.MensagemAmigavel ?? string.Empty;
        }

        private static string DescreverStatus(LicenseStatus status) => status switch
        {
            LicenseStatus.Valid => "Licença ativa",
            LicenseStatus.Trial => "Período de teste",
            LicenseStatus.Expired => "Licença expirada",
            LicenseStatus.TrialExpired => "Teste expirado",
            LicenseStatus.NotActivated => "Não ativado",
            LicenseStatus.Tampered => "Arquivo corrompido",
            LicenseStatus.WrongMachine => "Máquina diferente",
            _ => status.ToString(),
        };

        private void BtnAtivar_Click(object sender, RoutedEventArgs e)
        {
            string token = txtChave.Text?.Trim();
            LicenseState result = LicenseService.Activate(token);

            ExibirFeedback(result);

            if (result.Status == LicenseStatus.Valid)
            {
                // Pequena pausa visual seria bom, mas como e modal e simples,
                // fechamos direto com sucesso. O caller mostra o aviso.
                DialogResult = true;
            }
            else
            {
                AtualizarStatus();
            }
        }

        private void ExibirFeedback(LicenseState result)
        {
            if (result == null) return;
            txtFeedback.Text = result.MensagemAmigavel ?? string.Empty;
            borderFeedback.Visibility = string.IsNullOrEmpty(txtFeedback.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnCopiarFingerprint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtFingerprint.Text ?? string.Empty);
                btnCopiarFingerprint.Content = "Copiado";
            }
            catch
            {
                // Clipboard pode falhar em sessoes RDP ou se outro app travar a area
                btnCopiarFingerprint.Content = "Falhou";
            }
        }

        // ===========================================================
        // PR-2 — Verificar atualizacoes manualmente
        // ===========================================================

        private async void BtnVerificarAtualizacoes_Click(object sender, RoutedEventArgs e)
        {
            // Bloqueia clique-duplo enquanto consulta
            btnVerificarAtualizacoes.IsEnabled = false;
            string originalText = btnVerificarAtualizacoes.Content as string ?? "Verificar atualizacoes";
            try
            {
                btnVerificarAtualizacoes.Content = "Verificando...";

                PrivacySettingsStore store = new PrivacySettingsStore();
                PrivacySettings current = store.Load();

                // Se ainda nao houve consent, abrir PrivacyConsentWindow primeiro
                if (current.AutoUpdate == ConsentState.Unset)
                {
                    PrivacyConsentWindow consent = new PrivacyConsentWindow(current) { Owner = this };
                    bool? consentDialog = consent.ShowDialog();
                    if (consentDialog != true || consent.Result == null)
                    {
                        // Usuario fechou sem decidir — tratar como Denied silenciosamente
                        return;
                    }
                    store.Save(consent.Result);
                    current = consent.Result;

                    if (current.AutoUpdate != ConsentState.Granted)
                    {
                        ExibirInfoUpdate("Verificacao de atualizacoes desativada.");
                        return;
                    }
                }
                else if (current.AutoUpdate == ConsentState.Denied)
                {
                    ExibirInfoUpdate("Verificacao de atualizacoes desativada. Edite privacy.json em %LocalAppData%\\FerramentaEMT\\ para ativar.");
                    return;
                }

                // Forcar verificacao online (zerar cache 24h temporariamente para esta acao)
                current.LastUpdateCheckUtc = DateTime.MinValue;
                store.Save(current);

                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                GitHubReleaseProvider provider = new GitHubReleaseProvider("Alefvieira233", "EMT");
                UpdateCheckService service = new UpdateCheckService(provider, store, version);

                UpdateCheckResult result;
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    result = await service.CheckAsync(cts.Token).ConfigureAwait(true);
                }

                switch (result.Outcome)
                {
                    case UpdateCheckOutcome.UpdateAvailable:
                        ExibirInfoUpdate($"Versao nova disponivel: {result.LatestVersion}. Abrir pagina de release?");
                        if (MessageBox.Show(
                                this,
                                $"Versao {result.LatestVersion} disponivel. Abrir a pagina de release no navegador?\n\nO download manual eh seguro; o auto-download em background sera implementado em v1.7.x.",
                                "Atualizacao disponivel",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information) == MessageBoxResult.Yes
                            && !string.IsNullOrEmpty(result.ReleaseUrl))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "[Update] falha ao abrir browser");
                            }
                        }
                        break;
                    case UpdateCheckOutcome.NoUpdate:
                        ExibirInfoUpdate("Voce ja tem a versao mais recente.");
                        break;
                    case UpdateCheckOutcome.Unknown:
                        ExibirInfoUpdate("Nao foi possivel verificar (sem internet, GitHub indisponivel ou rate limit). Tente novamente mais tarde.");
                        break;
                    case UpdateCheckOutcome.ConsentDenied:
                        ExibirInfoUpdate("Verificacao desativada nas preferencias de privacidade.");
                        break;
                    case UpdateCheckOutcome.ConsentRequired:
                        ExibirInfoUpdate("Consent ainda nao registrado.");
                        break;
                }
            }
            finally
            {
                btnVerificarAtualizacoes.Content = originalText;
                btnVerificarAtualizacoes.IsEnabled = true;
            }
        }

        private void ExibirInfoUpdate(string message)
        {
            txtFeedback.Text = message;
            borderFeedback.Visibility = string.IsNullOrEmpty(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
