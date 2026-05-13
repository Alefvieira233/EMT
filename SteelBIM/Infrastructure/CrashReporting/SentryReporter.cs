using System;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Models.Privacy;
using Sentry;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Facade estatico para crash reporting remoto via Sentry.
    /// Idempotente, try/catch raiz em todo entry point.
    /// NUNCA lanca para o caller — toda falha eh logada via callback.
    ///
    /// Wiring (mesmo padrao da PR-2 UpdateLog): callbacks publicos sao
    /// substituidos em App.OnStartup pelo Logger real e pelo
    /// LicenseService.GetCurrentState. Sem o wiring, defaults silenciosos
    /// permitem testes em xUnit sem linkar Logger nem LicenseService.
    ///
    /// Ordem de Initialize():
    ///   1. Verifica consent (CrashReports == Granted) — caso contrario
    ///      no-op + log "Sentry disabled (consent denied or unset)".
    ///   2. Resolve DSN. Vazio → no-op + log "Sentry disabled (no DSN
    ///      configured)".
    ///   3. Monta options via SentryOptionsBuilder (com licenseStateResolver
    ///      lazy) e chama _hub.Init.
    ///   4. Marca IsEnabled = true.
    /// </summary>
    public static class SentryReporter
    {
        // ==================== Wiring (set by App.OnStartup) ====================

        /// <summary>
        /// Resolve o license state na hora da captura (lazy). Default
        /// retorna "Unknown" — App.OnStartup substitui por
        /// LicenseService.GetCurrentState().Status.ToString().
        /// </summary>
        public static Func<string> LicenseStateResolver { get; set; } =
            () => "Unknown";

        /// <summary>
        /// Resolve o release/version. Default usa o assembly version
        /// do proprio SentryReporter — em testes pode ser substituido.
        /// </summary>
        public static Func<string> ReleaseResolver { get; set; } =
            () => typeof(SentryReporter).Assembly.GetName().Version?.ToString() ?? "unknown";

        /// <summary>
        /// Fabrica do hub. Default retorna null (caller injeta via
        /// OverrideHubForTests ou via App.OnStartup com () => new SentryHubFacade()).
        /// Manter null permite que o test csproj nao linke SentryHubFacade.
        /// </summary>
        public static Func<ISentryHubFacade> HubFactory { get; set; } = null;

        /// <summary>
        /// Loja de PrivacySettings. Default null — App.OnStartup precisa
        /// setar para PrivacySettingsStore real antes de chamar Initialize.
        /// Manter null permite que o test csproj nao linke
        /// PrivacySettingsStore (que depende de Logger/Serilog). Se ficar
        /// null em runtime, Initialize trata como consent denied
        /// (no-op silencioso).
        /// </summary>
        public static IPrivacySettingsStore PrivacyStore { get; set; } = null;

        // Callbacks de log — substituidos em App.OnStartup. Defaults silenciosos
        // pra testes nao precisarem mockar Logger.
        public static Action<string> LogInfo { get; set; } = _ => { };
        public static Action<string, object[]> LogInfoTemplate { get; set; } = (_, _) => { };
        public static Action<Exception, string> LogWarn { get; set; } = (_, _) => { };

        // ==================== Estado interno ====================

        private static readonly object _lock = new object();
        private static bool _initialized;
        private static ISentryHubFacade _hub;

        /// <summary>
        /// True se Init rodou e SentrySdk esta ativo. False se DSN ausente,
        /// consent denied, init lancou, ou Initialize ainda nao foi chamado.
        /// </summary>
        public static bool IsEnabled { get; private set; }

        // ==================== API publica ====================

        /// <summary>
        /// Inicializa a SDK. Idempotente: chamadas subsequentes sao no-op.
        /// Try/catch raiz: nunca lanca. Caller pode chamar sem precaucao.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    SafeLogInfo("[Sentry] disabled (already initialized)");
                    return;
                }

                try
                {
                    // 1. Consent — PrivacyStore null eh tratado como consent
                    //    nao concedido (caller esqueceu de wirar; comportamento
                    //    conservador).
                    if (PrivacyStore == null)
                    {
                        SafeLogInfo("[Sentry] disabled (privacy store not wired)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    PrivacySettings settings = SafeLoadSettings();
                    if (settings == null || settings.CrashReports != ConsentState.Granted)
                    {
                        SafeLogInfo("[Sentry] disabled (consent denied or unset)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    // 2. DSN
                    string dsn = SentryDsnProvider.GetDsn();
                    SentryDsnProvider.DsnSource source = SentryDsnProvider.GetResolvedSource();
                    if (string.IsNullOrWhiteSpace(dsn))
                    {
                        SafeLogInfo("[Sentry] disabled (no DSN configured)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    // 3. Build options + init
                    string release = SafeRelease();
                    SentryOptions options = SentryOptionsBuilder.Build(
                        dsn, release, LicenseStateResolver);

                    ISentryHubFacade hub = ResolveHub();
                    if (hub == null)
                    {
                        SafeLogInfo("[Sentry] disabled (no hub factory configured)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    hub.Init(options);
                    _hub = hub;
                    _initialized = true;
                    IsEnabled = true;

                    SafeLogInfoTemplate(
                        "[Sentry] initialized (DSN source: {Source}, sample rates: errors=100% breadcrumbs<={Max})",
                        new object[] { source, SentryOptionsBuilder.MaxBreadcrumbs });
                }
                catch (Exception ex)
                {
                    // Nunca propagar. CrashReporter local continua funcionando.
                    _initialized = true;
                    IsEnabled = false;
                    SafeLogWarn(ex, "[Sentry] init falhou — captura local continua");
                }
            }
        }

        /// <summary>
        /// Captura uma excecao no Sentry. No-op se !IsEnabled. Try/catch
        /// raiz: nunca lanca. <paramref name="kind"/> eh anexado como tag
        /// (ex.: "unhandled", "unobserved-task", "test").
        /// </summary>
        public static void CaptureCrash(Exception ex, string kind)
        {
            if (!IsEnabled) return;
            if (ex == null) return;
            if (_hub == null) return;

            try
            {
                if (!string.IsNullOrWhiteSpace(kind))
                {
                    _hub.SetTag("kind", kind);
                }
                _hub.CaptureException(ex);
            }
            catch (Exception capEx)
            {
                SafeLogWarn(capEx, "[Sentry] CaptureException falhou");
            }
        }

        /// <summary>
        /// Drena eventos pendentes ate o timeout. No-op se !IsEnabled.
        /// </summary>
        public static void Flush(int timeoutMs = 2000)
        {
            if (!IsEnabled) return;
            if (_hub == null) return;

            try
            {
                _hub.FlushAsync(TimeSpan.FromMilliseconds(timeoutMs))
                    .Wait(timeoutMs);
            }
            catch (Exception ex)
            {
                SafeLogWarn(ex, "[Sentry] Flush falhou");
            }
        }

        // ==================== Test helpers ====================

        /// <summary>Substitui o hub para testes. Null restaura factory default.</summary>
        internal static void OverrideHubForTests(ISentryHubFacade fake)
        {
            lock (_lock) { _hub = fake; }
        }

        /// <summary>
        /// Reset completo: clean slate para o proximo teste. Limpa _initialized,
        /// IsEnabled, _hub e devolve PrivacyStore + delegates aos defaults.
        /// </summary>
        internal static void ResetForTests()
        {
            lock (_lock)
            {
                _initialized = false;
                IsEnabled = false;
                _hub = null;
                PrivacyStore = null;
                LicenseStateResolver = () => "Unknown";
                ReleaseResolver = () => typeof(SentryReporter).Assembly.GetName().Version?.ToString() ?? "unknown";
                HubFactory = null;
                LogInfo = _ => { };
                LogInfoTemplate = (_, _) => { };
                LogWarn = (_, _) => { };
            }
        }

        // ==================== Internos ====================

        private static ISentryHubFacade ResolveHub()
        {
            if (_hub != null) return _hub;
            if (HubFactory != null)
            {
                try { return HubFactory(); } catch { return null; }
            }
            return null;
        }

        private static PrivacySettings SafeLoadSettings()
        {
            try { return PrivacyStore?.Load(); }
            catch { return null; }
        }

        private static string SafeRelease()
        {
            try { return ReleaseResolver?.Invoke() ?? "unknown"; }
            catch { return "unknown"; }
        }

        private static void SafeLogInfo(string message)
        {
            try { LogInfo?.Invoke(message); } catch { }
        }

        private static void SafeLogInfoTemplate(string template, object[] args)
        {
            try { LogInfoTemplate?.Invoke(template, args); } catch { }
        }

        private static void SafeLogWarn(Exception ex, string message)
        {
            try { LogWarn?.Invoke(ex, message); } catch { }
        }
    }
}
