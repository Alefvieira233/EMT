using System;
using FerramentaEMT.Infrastructure.Privacy;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Cola entre App.OnStartup e TelemetryReporter — concentra o wiring
    /// num metodo testavel sem dependencia de UIControlledApplication
    /// nem de Logger/LicenseService. Mesmo padrao do
    /// <see cref="FerramentaEMT.Infrastructure.CrashReporting.SentryStartupWiring"/>.
    /// </summary>
    public static class TelemetryStartupWiring
    {
        public static void Configure(
            IPrivacySettingsStore privacyStore,
            Func<ITelemetryClient> clientFactory,
            Func<string> licenseStateResolver,
            Action<string> logInfo,
            Action<string, object[]> logInfoTemplate,
            Action<Exception, string> logWarn,
            Func<string> releaseResolver)
        {
            TelemetryReporter.PrivacyStore = privacyStore;
            TelemetryReporter.ClientFactory = clientFactory;
            TelemetryReporter.LicenseStateResolver =
                licenseStateResolver ?? (() => "Unknown");
            TelemetryReporter.LogInfo = logInfo ?? (_ => { });
            TelemetryReporter.LogInfoTemplate = logInfoTemplate ?? ((_, _) => { });
            TelemetryReporter.LogWarn = logWarn ?? ((_, _) => { });
            TelemetryReporter.ReleaseResolver = releaseResolver
                ?? (() => typeof(TelemetryStartupWiring).Assembly.GetName().Version?.ToString() ?? "unknown");
        }

        public static void InitializeServices(
            IPrivacySettingsStore privacyStore,
            Func<ITelemetryClient> clientFactory,
            Func<string> licenseStateResolver,
            Action<string> logInfo,
            Action<string, object[]> logInfoTemplate,
            Action<Exception, string> logWarn,
            Func<string> releaseResolver)
        {
            Configure(privacyStore, clientFactory, licenseStateResolver,
                logInfo, logInfoTemplate, logWarn, releaseResolver);
            TelemetryReporter.Initialize();
        }
    }
}
