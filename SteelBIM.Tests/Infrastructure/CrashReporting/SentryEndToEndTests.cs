using System;
using System.Linq;
using FerramentaEMT.Infrastructure.CrashReporting;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Models.Privacy;
using FluentAssertions;
using Moq;
using Sentry;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.CrashReporting
{
    /// <summary>
    /// Smoke test end-to-end (Ajuste 1 do plano): exercita o pipeline
    /// completo SentryStartupWiring.InitializeServices →
    /// SentryReporter.Initialize → SentryReporter.CaptureCrash, com mock
    /// de ISentryHubFacade. NAO testa Idling event (depende de UIApplication
    /// Revit-bound). NAO testa flow real do Sentry (sem rede). So valida
    /// que o pipeline interno conecta as pecas certas.
    /// </summary>
    [Collection("SentryDsnSerial")]
    public class SentryEndToEndTests : IDisposable
    {
        private readonly string _origDsnEnv;

        public SentryEndToEndTests()
        {
            _origDsnEnv = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            SentryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, null);
            SentryDsnProvider.ResetCacheForTests();
        }

        public void Dispose()
        {
            SentryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(SentryDsnProvider.EnvVarName, _origDsnEnv);
            SentryDsnProvider.ResetCacheForTests();
        }

        private static IPrivacySettingsStore StoreReturning(PrivacySettings settings)
        {
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            store.Setup(s => s.Load()).Returns(settings);
            return store.Object;
        }

        [Fact]
        public void Wiring_then_simulated_crash_calls_hub_with_kind_tag_and_exception()
        {
            // ARRANGE: como App.OnStartup configuraria, mas com mocks.
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.IsEnabled).Returns(true);
            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { CrashReports = ConsentState.Granted });
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://e2e@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            // ACT 1: wiring + initialize.
            SentryStartupWiring.InitializeServices(
                privacyStore: store,
                hubFactory: () => hub.Object,
                licenseStateResolver: () => "Trial",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            SentryReporter.IsEnabled.Should().BeTrue("DSN + consent + hub OK");
            hub.Verify(h => h.Init(It.Is<SentryOptions>(o =>
                o.Dsn == "https://e2e@sentry.io/1" &&
                o.Release == "1.7.0")),
                Times.Once);

            // ACT 2: simula o que CrashReporter.DumpCrash faz —
            // SentryReporter.CaptureCrash(ex, kind).
            InvalidOperationException smokeEx;
            try { throw new InvalidOperationException("smoke"); }
            catch (InvalidOperationException caught) { smokeEx = caught; }
            SentryReporter.CaptureCrash(smokeEx, "unhandled");

            // ASSERT: hub recebeu kind como tag + exception com message + stack.
            hub.Verify(h => h.SetTag("kind", "unhandled"), Times.Once);
            hub.Verify(h => h.CaptureException(It.Is<Exception>(e =>
                e == smokeEx
                && e.Message == "smoke"
                && e.StackTrace != null
                && e.StackTrace.Length > 0)),
                Times.Once);
        }

        [Fact]
        public void End_to_end_options_carry_all_required_tags_via_BeforeSend()
        {
            // Capta as SentryOptions enviadas para hub.Init e roda o BeforeSend
            // hook manualmente — verifica que as 5 tags entram no evento.
            SentryOptions captured = null;
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.Init(It.IsAny<SentryOptions>()))
               .Callback<SentryOptions>(opts => captured = opts);
            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { CrashReports = ConsentState.Granted });
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://e2e@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            SentryStartupWiring.InitializeServices(
                privacyStore: store,
                hubFactory: () => hub.Object,
                licenseStateResolver: () => "Valid",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            captured.Should().NotBeNull();

            // Roda o BeforeSend de forma direta — usa o helper publico
            // ScrubAndTag (mesmo metodo registrado no hook, exposto pra test).
            SentryEvent evt = new SentryEvent { Message = "User joao@x.com falhou" };
            SentryOptionsBuilder.ScrubAndTag(evt, "1.7.0", "Valid");

            evt.Tags.Should().ContainKeys(
                "version", "revit_version", "os", "culture", "license_state");
            evt.Tags["version"].Should().Be("1.7.0");
            evt.Tags["revit_version"].Should().Be("2025");
            evt.Tags["license_state"].Should().Be("Valid");
            evt.Message.Message.Should().Contain("<EMAIL>");
            evt.Message.Message.Should().NotContain("joao@x.com");
        }

        [Fact]
        public void End_to_end_skips_capture_when_consent_denied()
        {
            // Pipeline com consent denied: Initialize vira no-op, CaptureCrash
            // tambem vira no-op. Hub.Init NUNCA chamado, hub.CaptureException
            // NUNCA chamado.
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            IPrivacySettingsStore store = StoreReturning(
                new PrivacySettings { CrashReports = ConsentState.Denied });
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://e2e@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            SentryStartupWiring.InitializeServices(
                privacyStore: store,
                hubFactory: () => hub.Object,
                licenseStateResolver: () => "Trial",
                logInfo: _ => { },
                logInfoTemplate: (_, _) => { },
                logWarn: (_, _) => { },
                releaseResolver: () => "1.7.0");

            SentryReporter.IsEnabled.Should().BeFalse();
            SentryReporter.CaptureCrash(new InvalidOperationException("x"), "test");

            hub.Verify(h => h.Init(It.IsAny<SentryOptions>()), Times.Never);
            hub.Verify(h => h.CaptureException(It.IsAny<Exception>()), Times.Never);
        }
    }
}
