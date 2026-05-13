using System;
using FerramentaEMT.Infrastructure.Privacy;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Cola entre App.OnStartup e SentryReporter — concentra todo o
    /// wiring num metodo testavel sem dependencia de UIControlledApplication
    /// nem de Logger/Serilog/LicenseService (App.cs nao roda em xUnit).
    ///
    /// Pure por design: so chama setters em SentryReporter e dispara
    /// SentryReporter.Initialize. Smoke test e2e (commit 7) chama
    /// InitializeServices direto com mocks de ISentryHubFacade e
    /// IPrivacySettingsStore.
    /// </summary>
    public static class SentryStartupWiring
    {
        /// <summary>
        /// Substitui os hooks publicos de SentryReporter pelos providers
        /// de runtime. NAO chama Initialize — caller decide quando.
        /// </summary>
        public static void Configure(
            IPrivacySettingsStore privacyStore,
            Func<ISentryHubFacade> hubFactory,
            Func<string> licenseStateResolver,
            Action<string> logInfo,
            Action<string, object[]> logInfoTemplate,
            Action<Exception, string> logWarn,
            Func<string> releaseResolver)
        {
            SentryReporter.PrivacyStore = privacyStore;
            SentryReporter.HubFactory = hubFactory;
            SentryReporter.LicenseStateResolver =
                licenseStateResolver ?? (() => "Unknown");
            SentryReporter.LogInfo = logInfo ?? (_ => { });
            SentryReporter.LogInfoTemplate = logInfoTemplate ?? ((_, _) => { });
            SentryReporter.LogWarn = logWarn ?? ((_, _) => { });
            SentryReporter.ReleaseResolver = releaseResolver
                ?? (() => typeof(SentryStartupWiring).Assembly.GetName().Version?.ToString() ?? "unknown");
        }

        /// <summary>
        /// Configure + Initialize numa unica chamada — o que App.OnStartup
        /// invoca em runtime e o que o smoke test e2e invoca com mocks.
        /// </summary>
        public static void InitializeServices(
            IPrivacySettingsStore privacyStore,
            Func<ISentryHubFacade> hubFactory,
            Func<string> licenseStateResolver,
            Action<string> logInfo,
            Action<string, object[]> logInfoTemplate,
            Action<Exception, string> logWarn,
            Func<string> releaseResolver)
        {
            Configure(privacyStore, hubFactory, licenseStateResolver,
                logInfo, logInfoTemplate, logWarn, releaseResolver);
            SentryReporter.Initialize();
        }
    }
}
