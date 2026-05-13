using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Infrastructure.Telemetry;
using FerramentaEMT.Models.Privacy;
using FluentAssertions;
using Moq;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre os contratos do TelemetryReporter — mesmo padrao dos
    /// SentryReporterTests (PR-3): idempotencia, no-op paths, Track via
    /// mock, Flush, swallow de excecoes. Pipeline real coberto pelo
    /// TelemetryEndToEndTests com CapturingHandler.
    /// </summary>
    [Collection("PostHogApiKeySerial")]
    public class TelemetryReporterTests : IDisposable
    {
        private readonly string _origApiKeyEnv;

        public TelemetryReporterTests()
        {
            _origApiKeyEnv = Environment.GetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName);
            TelemetryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, null);
            PostHogApiKeyProvider.ResetCacheForTests();
        }

        public void Dispose()
        {
            TelemetryReporter.ResetForTests();
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, _origApiKeyEnv);
            PostHogApiKeyProvider.ResetCacheForTests();
        }

        private static IPrivacySettingsStore StoreReturning(PrivacySettings settings)
        {
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            store.Setup(s => s.Load()).Returns(settings);
            return store.Object;
        }

        private static PrivacySettings ConsentGranted() =>
            new PrivacySettings { Telemetry = ConsentState.Granted };

        private static PrivacySettings ConsentDenied() =>
            new PrivacySettings { Telemetry = ConsentState.Denied };

        // ---------- Initialize: no-op paths ----------

        [Fact]
        public void Initialize_is_noop_when_privacy_store_not_wired()
        {
            List<string> infos = new List<string>();
            TelemetryReporter.LogInfo = msg => infos.Add(msg);
            TelemetryReporter.PrivacyStore = null;

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("privacy store not wired"));
        }

        [Fact]
        public void Initialize_is_noop_when_consent_denied()
        {
            List<string> infos = new List<string>();
            TelemetryReporter.LogInfo = msg => infos.Add(msg);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentDenied());

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("consent denied or unset"));
        }

        [Fact]
        public void Initialize_is_noop_when_no_api_key_configured()
        {
            List<string> infos = new List<string>();
            TelemetryReporter.LogInfo = msg => infos.Add(msg);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("no API key configured"));
        }

        [Fact]
        public void Initialize_is_noop_when_client_factory_missing()
        {
            List<string> infos = new List<string>();
            TelemetryReporter.LogInfo = msg => infos.Add(msg);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("no client factory"));
        }

        [Fact]
        public void Initialize_is_noop_when_factory_returns_null()
        {
            List<string> infos = new List<string>();
            TelemetryReporter.LogInfo = msg => infos.Add(msg);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => null;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeFalse();
            infos.Should().Contain(s => s.Contains("factory returned null"));
        }

        // ---------- Initialize: success path ----------

        [Fact]
        public void Initialize_sets_IsEnabled_when_consent_granted_and_api_key_present()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryReporter.Initialize();

            TelemetryReporter.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void Initialize_emits_license_state_checked_event_after_success()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            TelemetryReporter.LicenseStateResolver = () => "Trial";
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryReporter.Initialize();

            client.Verify(c => c.Track(It.Is<TelemetryEvent>(e =>
                e.Name == SamplingDecider.EventLicenseStateChecked &&
                (string)e.Properties["status"] == "Trial")),
                Times.Once);
        }

        [Fact]
        public void Initialize_is_idempotent()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            int factoryCalls = 0;
            TelemetryReporter.ClientFactory = () =>
            {
                Interlocked.Increment(ref factoryCalls);
                return client.Object;
            };
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            TelemetryReporter.Initialize();
            TelemetryReporter.Initialize();
            TelemetryReporter.Initialize();

            factoryCalls.Should().Be(1, "factory chamada uma vez apesar de 3 Initializes");
        }

        [Fact]
        public void Initialize_does_not_throw_when_factory_throws()
        {
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () =>
                throw new InvalidOperationException("factory ruim");
            List<Exception> warns = new List<Exception>();
            TelemetryReporter.LogWarn = (ex, _) => warns.Add(ex);
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();

            Action act = () => TelemetryReporter.Initialize();

            act.Should().NotThrow();
            TelemetryReporter.IsEnabled.Should().BeFalse();
            warns.Should().NotBeEmpty();
        }

        // ---------- Track ----------

        [Fact]
        public void Track_is_noop_when_not_initialized()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            TelemetryReporter.OverrideClientForTests(client.Object);

            TelemetryReporter.Track(new TelemetryEvent("custom.event", null));

            client.Verify(c => c.Track(It.IsAny<TelemetryEvent>()), Times.Never);
        }

        [Fact]
        public void Track_with_null_event_does_not_throw()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();
            TelemetryReporter.Initialize();
            client.Invocations.Clear();

            Action act = () => TelemetryReporter.Track(null);

            act.Should().NotThrow();
            client.Verify(c => c.Track(It.IsAny<TelemetryEvent>()), Times.Never);
        }

        [Fact]
        public void Track_passes_event_to_underlying_client()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();
            TelemetryReporter.Initialize();
            client.Invocations.Clear();

            TelemetryEvent evt = new TelemetryEvent("custom.event",
                new Dictionary<string, object> { { "k", "v" } });
            TelemetryReporter.Track(evt);

            client.Verify(c => c.Track(evt), Times.Once);
        }

        [Fact]
        public void Track_swallows_exception_from_client()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            client.Setup(c => c.Track(It.IsAny<TelemetryEvent>()))
                  .Throws(new InvalidOperationException("client down"));
            List<Exception> warns = new List<Exception>();
            TelemetryReporter.LogWarn = (ex, _) => warns.Add(ex);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();
            TelemetryReporter.Initialize();

            Action act = () => TelemetryReporter.Track(
                new TelemetryEvent("custom.event", null));

            act.Should().NotThrow();
            warns.Should().NotBeEmpty();
        }

        // ---------- Flush ----------

        [Fact]
        public void Flush_is_noop_when_not_initialized()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            TelemetryReporter.OverrideClientForTests(client.Object);

            TelemetryReporter.Flush(500);

            client.Verify(c => c.FlushAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void Flush_calls_client_FlushAsync_when_enabled()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            client.Setup(c => c.FlushAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();
            TelemetryReporter.Initialize();
            client.Invocations.Clear();

            TelemetryReporter.Flush(1500);

            client.Verify(c => c.FlushAsync(1500), Times.Once);
        }

        [Fact]
        public void Flush_swallows_exception()
        {
            Mock<ITelemetryClient> client = new Mock<ITelemetryClient>();
            client.Setup(c => c.IsEnabled).Returns(true);
            client.Setup(c => c.FlushAsync(It.IsAny<int>()))
                  .Throws(new InvalidOperationException("flush ruim"));
            TelemetryReporter.PrivacyStore = StoreReturning(ConsentGranted());
            TelemetryReporter.ClientFactory = () => client.Object;
            Environment.SetEnvironmentVariable(
                PostHogApiKeyProvider.EnvVarName, "phc_test");
            PostHogApiKeyProvider.ResetCacheForTests();
            TelemetryReporter.Initialize();

            Action act = () => TelemetryReporter.Flush(500);

            act.Should().NotThrow();
        }
    }
}
