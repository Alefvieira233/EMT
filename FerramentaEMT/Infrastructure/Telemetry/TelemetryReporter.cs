using System;
using System.Collections.Generic;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Models.Privacy;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Facade estatico para telemetria de uso via PostHog.
    /// Mesmo padrao do <see cref="FerramentaEMT.Infrastructure.CrashReporting.SentryReporter"/>:
    ///   - Idempotente.
    ///   - Try/catch raiz em todo entry point — NUNCA propaga.
    ///   - Wiring por delegates (callbacks publicos substituidos em
    ///     App.OnStartup pelos providers reais). Defaults silenciosos
    ///     permitem testes em xUnit sem linkar Logger.
    ///
    /// Ordem de Initialize():
    ///   1. Verifica consent (Telemetry == Granted) — caso contrario
    ///      no-op + log "Telemetry disabled (consent denied or unset)".
    ///   2. Resolve API key. Vazia → no-op + log "Telemetry disabled
    ///      (no API key configured)".
    ///   3. Constroi cliente via ClientFactory.
    ///   4. Emite evento sintetico license.state_checked com o status
    ///      atual da licenca (briefing PR-4 §4.9 Opcao B — emitido pelo
    ///      proprio Init em vez de queue interno).
    ///   5. Marca IsEnabled = true.
    ///
    /// Logs no boot:
    ///   [Telemetry] disabled (already initialized)
    ///   [Telemetry] disabled (privacy store not wired)
    ///   [Telemetry] disabled (consent denied or unset)
    ///   [Telemetry] disabled (no API key configured)
    ///   [Telemetry] disabled (no client factory configured)
    ///   [Telemetry] initialized (api key source: {Source}, host: {Host}, session: {SessionIdShort})
    /// </summary>
    public static class TelemetryReporter
    {
        // ==================== Wiring (set by App.OnStartup) ====================

        /// <summary>
        /// Loja de PrivacySettings. Default null — App.OnStartup precisa
        /// setar pra PrivacySettingsStore real antes de Initialize.
        /// Manter null permite que test csproj nao linke Logger via
        /// PrivacySettingsStore.
        /// </summary>
        public static IPrivacySettingsStore PrivacyStore { get; set; } = null;

        /// <summary>
        /// Fabrica do cliente. Default null — App.OnStartup configura
        /// () => new PostHogHttpTelemetryClient(...). Receber via
        /// factory permite teste com mock que nao toca rede.
        /// </summary>
        public static Func<ITelemetryClient> ClientFactory { get; set; } = null;

        /// <summary>
        /// Resolve license state na hora da captura (lazy). Default
        /// "Unknown". App.OnStartup substitui por
        /// LicenseService.GetCurrentState().Status.ToString().
        /// </summary>
        public static Func<string> LicenseStateResolver { get; set; } = () => "Unknown";

        /// <summary>
        /// Resolve release/version. Default usa Assembly version.
        /// </summary>
        public static Func<string> ReleaseResolver { get; set; } =
            () => typeof(TelemetryReporter).Assembly.GetName().Version?.ToString() ?? "unknown";

        // Callbacks de log — substituidos em App.OnStartup. Defaults silenciosos.
        public static Action<string> LogInfo { get; set; } = _ => { };
        public static Action<string, object[]> LogInfoTemplate { get; set; } = (_, _) => { };
        public static Action<Exception, string> LogWarn { get; set; } = (_, _) => { };

        // ==================== Estado interno ====================

        private static readonly object _lock = new object();
        private static bool _initialized;
        private static ITelemetryClient _client;

        /// <summary>True se Init rodou e cliente esta ativo.</summary>
        public static bool IsEnabled { get; private set; }

        // ==================== API publica ====================

        /// <summary>
        /// Inicializa o subsistema de telemetria. Idempotente: chamadas
        /// subsequentes sao no-op. Try/catch raiz: nunca lanca.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    SafeLogInfo("[Telemetry] disabled (already initialized)");
                    return;
                }

                try
                {
                    // 1. Consent
                    if (PrivacyStore == null)
                    {
                        SafeLogInfo("[Telemetry] disabled (privacy store not wired)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    PrivacySettings settings = SafeLoadSettings();
                    if (settings == null || settings.Telemetry != ConsentState.Granted)
                    {
                        SafeLogInfo("[Telemetry] disabled (consent denied or unset)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    // 2. API key
                    string apiKey = PostHogApiKeyProvider.GetApiKey();
                    PostHogApiKeyProvider.ApiKeySource keySource =
                        PostHogApiKeyProvider.GetResolvedSource();
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        SafeLogInfo("[Telemetry] disabled (no API key configured)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    // 3. Client via factory
                    if (ClientFactory == null)
                    {
                        SafeLogInfo("[Telemetry] disabled (no client factory configured)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    ITelemetryClient client;
                    try { client = ClientFactory(); }
                    catch (Exception fEx)
                    {
                        SafeLogWarn(fEx, "[Telemetry] client factory falhou");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }
                    if (client == null)
                    {
                        SafeLogInfo("[Telemetry] disabled (factory returned null)");
                        _initialized = true;
                        IsEnabled = false;
                        return;
                    }

                    _client = client;
                    _initialized = true;
                    IsEnabled = client.IsEnabled;

                    string host = PostHogHostProvider.GetHost();
                    string sessionPrefix = SessionIdProvider.GetShortPrefix();
                    SafeLogInfoTemplate(
                        "[Telemetry] initialized (api key source: {Source}, host: {Host}, session: {Session})",
                        new object[] { keySource, host, sessionPrefix });

                    // 4. license.state_checked emitido pelo proprio Init
                    //    (briefing §4.9 Opcao B — sem queue interno).
                    string licenseState = SafeResolveLicenseState();
                    Track(new TelemetryEvent(
                        SamplingDecider.EventLicenseStateChecked,
                        new Dictionary<string, object>
                        {
                            { "status", licenseState }
                        }));
                }
                catch (Exception ex)
                {
                    _initialized = true;
                    IsEnabled = false;
                    SafeLogWarn(ex, "[Telemetry] init falhou — telemetria continua desligada");
                }
            }
        }

        /// <summary>
        /// Captura um evento. No-op se !IsEnabled. Try/catch raiz: nunca lanca.
        /// Sampling eh aplicado no proprio cliente (PostHogHttpTelemetryClient).
        /// </summary>
        public static void Track(TelemetryEvent evt)
        {
            if (!IsEnabled) return;
            if (evt == null) return;
            if (_client == null) return;

            try { _client.Track(evt); }
            catch (Exception ex)
            {
                SafeLogWarn(ex, "[Telemetry] Track falhou");
            }
        }

        /// <summary>
        /// Drena eventos pendentes. No-op se !IsEnabled. PostHogHttp
        /// retorna Task.CompletedTask imediato (sem batch).
        /// </summary>
        public static void Flush(int timeoutMs = 2000)
        {
            if (!IsEnabled) return;
            if (_client == null) return;

            try
            {
                _client.FlushAsync(timeoutMs).Wait(timeoutMs);
            }
            catch (Exception ex)
            {
                SafeLogWarn(ex, "[Telemetry] Flush falhou");
            }
        }

        // ==================== Test helpers ====================

        internal static void OverrideClientForTests(ITelemetryClient fake)
        {
            lock (_lock) { _client = fake; }
        }

        internal static void ResetForTests()
        {
            lock (_lock)
            {
                _initialized = false;
                IsEnabled = false;
                _client = null;
                PrivacyStore = null;
                ClientFactory = null;
                LicenseStateResolver = () => "Unknown";
                ReleaseResolver = () =>
                    typeof(TelemetryReporter).Assembly.GetName().Version?.ToString() ?? "unknown";
                LogInfo = _ => { };
                LogInfoTemplate = (_, _) => { };
                LogWarn = (_, _) => { };
            }
        }

        // ==================== Internos ====================

        private static PrivacySettings SafeLoadSettings()
        {
            try { return PrivacyStore?.Load(); }
            catch { return null; }
        }

        private static string SafeResolveLicenseState()
        {
            try { return LicenseStateResolver?.Invoke() ?? "Unknown"; }
            catch { return "Unknown"; }
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
