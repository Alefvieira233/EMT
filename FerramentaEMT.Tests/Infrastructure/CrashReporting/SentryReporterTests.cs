using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// Cobre os contratos do SentryReporter:
    ///   - Initialize eh idempotente.
    ///   - Init no-op em DSN ausente, consent denied/unset, PrivacyStore nao wirado.
    ///   - CaptureCrash eh no-op quando IsEnabled == false.
    ///   - CaptureCrash anexa kind como tag e chama hub.CaptureException.
    ///   - Flush respeita IsEnabled.
    ///   - Falha em hub.Init nao crasha o caller.
    ///
    /// Nao testa SDK real — usa ISentryHubFacade mockado via Moq.
    /// </summary>
    [Collection("SentryReporterSerial")]
    public class SentryReporterTests : IDisposable
    {
        private readonly string _origDsnEnv;

        public SentryReporterTests()
        {
            _origDsnEnv = Environment.GetEnvironmentVariable(SentryDsnProvider.EnvVarName);
            // Garante baseline limpo entre testes.
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

        private static PrivacySettings ConsentGranted() =>
            new PrivacySettings { CrashReports = ConsentState.Granted };

        private static PrivacySettings ConsentDenied() =>
            new PrivacySettings { CrashReports = ConsentState.Denied };

        private static PrivacySettings ConsentUnset() =>
            new PrivacySettings { CrashReports = ConsentState.Unset };

        // ---------- Initialize: no-op paths ----------

        [Fact]
        public void Initialize_is_noop_when_privacy_store_not_wired()
        {
            List<string> infos = new List<string>();
            SentryReporter.LogInfo = msg => infos.Add(msg);
            SentryReporter.PrivacyStore = null;

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("privacy store not wired"));
        }

        [Fact]
        public void Initialize_is_noop_when_consent_denied()
        {
            List<string> infos = new List<string>();
            SentryReporter.LogInfo = msg => infos.Add(msg);
            SentryReporter.PrivacyStore = StoreReturning(ConsentDenied());

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("consent denied or unset"));
        }

        [Fact]
        public void Initialize_is_noop_when_consent_unset()
        {
            List<string> infos = new List<string>();
            SentryReporter.LogInfo = msg => infos.Add(msg);
            SentryReporter.PrivacyStore = StoreReturning(ConsentUnset());

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("consent denied or unset"));
        }

        [Fact]
        public void Initialize_is_noop_when_no_dsn_configured()
        {
            List<string> infos = new List<string>();
            SentryReporter.LogInfo = msg => infos.Add(msg);
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            // Sem env var EMT_SENTRY_DSN: DsnSource.DevFallbackEmpty.

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("no DSN configured"));
        }

        [Fact]
        public void Initialize_is_noop_when_hub_factory_missing()
        {
            // Consent + DSN OK, mas HubFactory null e nenhum override.
            List<string> infos = new List<string>();
            SentryReporter.LogInfo = msg => infos.Add(msg);
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("no hub factory"));
        }

        // ---------- Initialize: success path ----------

        [Fact]
        public void Initialize_sets_IsEnabled_when_consent_granted_and_dsn_present()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            SentryReporter.Initialize();

            SentryReporter.IsEnabled.Should().BeTrue();
            hub.Verify(h => h.Init(It.Is<SentryOptions>(o =>
                o.Dsn == "https://abc@sentry.io/1")), Times.Once);
        }

        [Fact]
        public void Initialize_is_idempotent_on_subsequent_calls()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            SentryReporter.Initialize();
            SentryReporter.Initialize();
            SentryReporter.Initialize();

            // hub.Init chamado UMA vez, mesmo com 3 chamadas a Initialize.
            hub.Verify(h => h.Init(It.IsAny<SentryOptions>()), Times.Once);
        }

        [Fact]
        public void Initialize_does_not_throw_when_hub_init_fails()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.Init(It.IsAny<SentryOptions>()))
               .Throws(new InvalidOperationException("DSN ruim"));
            List<Exception> warns = new List<Exception>();
            SentryReporter.LogWarn = (ex, _) => warns.Add(ex);
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();

            Action act = () => SentryReporter.Initialize();

            act.Should().NotThrow();
            SentryReporter.IsEnabled.Should().BeFalse();
            warns.Should().NotBeEmpty();
        }

        // ---------- CaptureCrash ----------

        [Fact]
        public void CaptureCrash_is_noop_when_not_initialized()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.OverrideHubForTests(hub.Object);
            // Initialize NAO chamado.

            SentryReporter.CaptureCrash(new InvalidOperationException("x"), "test");

            hub.Verify(h => h.CaptureException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void CaptureCrash_handles_null_exception_without_throwing()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            Action act = () => SentryReporter.CaptureCrash(null, "test");

            act.Should().NotThrow();
            hub.Verify(h => h.CaptureException(It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void CaptureCrash_attaches_kind_tag_and_calls_capture()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            InvalidOperationException ex = new InvalidOperationException("smoke");
            SentryReporter.CaptureCrash(ex, "unhandled");

            hub.Verify(h => h.SetTag("kind", "unhandled"), Times.Once);
            hub.Verify(h => h.CaptureException(ex), Times.Once);
        }

        [Fact]
        public void CaptureCrash_skips_kind_when_null_or_empty()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            SentryReporter.CaptureCrash(new Exception("x"), null);
            SentryReporter.CaptureCrash(new Exception("y"), string.Empty);

            hub.Verify(h => h.SetTag(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            hub.Verify(h => h.CaptureException(It.IsAny<Exception>()), Times.Exactly(2));
        }

        [Fact]
        public void CaptureCrash_swallows_exception_from_hub()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.CaptureException(It.IsAny<Exception>()))
               .Throws(new InvalidOperationException("hub down"));
            List<Exception> warns = new List<Exception>();
            SentryReporter.LogWarn = (ex, _) => warns.Add(ex);
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            Action act = () => SentryReporter.CaptureCrash(new Exception("x"), "test");

            act.Should().NotThrow();
            warns.Should().NotBeEmpty();
        }

        // ---------- Flush ----------

        [Fact]
        public void Flush_is_noop_when_not_initialized()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            SentryReporter.OverrideHubForTests(hub.Object);

            SentryReporter.Flush(500);

            hub.Verify(h => h.FlushAsync(It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public void Flush_calls_hub_FlushAsync_when_enabled()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.FlushAsync(It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            SentryReporter.Flush(1500);

            hub.Verify(h => h.FlushAsync(TimeSpan.FromMilliseconds(1500)), Times.Once);
        }

        [Fact]
        public void Flush_swallows_exception_from_hub()
        {
            Mock<ISentryHubFacade> hub = new Mock<ISentryHubFacade>();
            hub.Setup(h => h.FlushAsync(It.IsAny<TimeSpan>()))
               .Throws(new InvalidOperationException("disco cheio"));
            SentryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            SentryReporter.OverrideHubForTests(hub.Object);
            Environment.SetEnvironmentVariable(
                SentryDsnProvider.EnvVarName, "https://abc@sentry.io/1");
            SentryDsnProvider.ResetCacheForTests();
            SentryReporter.Initialize();

            Action act = () => SentryReporter.Flush(500);

            act.Should().NotThrow();
        }
    }
}
