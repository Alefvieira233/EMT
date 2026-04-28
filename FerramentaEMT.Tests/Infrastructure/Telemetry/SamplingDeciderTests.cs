using System;
using System.Collections.Generic;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre as 5 regras de sample rate documentadas em ADR-008 §Sampling.
    /// command.executed:success eh o unico amostrado (10%); o resto vai 100%.
    /// </summary>
    public class SamplingDeciderTests
    {
        private static TelemetryEvent Evt(string name, IReadOnlyDictionary<string, object> props = null)
            => new TelemetryEvent(name, props);

        [Fact]
        public void CommandExecuted_success_has_10_percent_rate()
        {
            TelemetryEvent evt = Evt(
                SamplingDecider.EventCommandExecuted,
                new Dictionary<string, object> { { "success", true } });

            SamplingDecider.ResolveRate(evt).Should().Be(SamplingDecider.CommandExecutedSuccessRate);
        }

        [Fact]
        public void CommandExecuted_failure_has_100_percent_rate()
        {
            TelemetryEvent evt = Evt(
                SamplingDecider.EventCommandExecuted,
                new Dictionary<string, object> { { "success", false } });

            SamplingDecider.ResolveRate(evt).Should().Be(SamplingDecider.FullSampleRate);
        }

        [Fact]
        public void CommandFailed_always_100_percent()
        {
            SamplingDecider.ResolveRate(Evt(SamplingDecider.EventCommandFailed))
                .Should().Be(SamplingDecider.FullSampleRate);
        }

        [Fact]
        public void LicenseStateChecked_always_100_percent()
        {
            SamplingDecider.ResolveRate(Evt(SamplingDecider.EventLicenseStateChecked))
                .Should().Be(SamplingDecider.FullSampleRate);
        }

        [Fact]
        public void Update_events_always_100_percent()
        {
            SamplingDecider.ResolveRate(Evt(SamplingDecider.EventUpdateDetected))
                .Should().Be(SamplingDecider.FullSampleRate);
            SamplingDecider.ResolveRate(Evt(SamplingDecider.EventUpdateApplied))
                .Should().Be(SamplingDecider.FullSampleRate);
        }

        [Fact]
        public void Unknown_event_defaults_to_100_percent()
        {
            // Defesa conservadora: melhor enviar dados desconhecidos do que perder.
            SamplingDecider.ResolveRate(Evt("custom.future.event"))
                .Should().Be(SamplingDecider.FullSampleRate);
        }

        [Fact]
        public void Null_event_returns_zero_rate_and_should_not_send()
        {
            SamplingDecider.ResolveRate(null).Should().Be(0.0);
            SamplingDecider.ShouldSend(null).Should().BeFalse();
        }

        [Fact]
        public void ShouldSend_with_seeded_rng_is_deterministic_for_command_executed()
        {
            // Random com seed fixa: sequencia de NextDouble() previsivel.
            // Verifica estatisticamente que sample rate 10% gera ~10% de trues
            // em 1000 amostras (intervalo aceitavel 5%–20%).
            Random rng = new Random(42);
            int trues = 0;
            TelemetryEvent evt = Evt(
                SamplingDecider.EventCommandExecuted,
                new Dictionary<string, object> { { "success", true } });

            for (int i = 0; i < 1000; i++)
            {
                if (SamplingDecider.ShouldSend(evt, rng)) trues++;
            }

            // 10% +- 5pts: aceita [5%, 20%]
            double observed = trues / 1000.0;
            observed.Should().BeInRange(0.05, 0.20,
                "rate 10% deve render ~50–200 trues em 1000 amostras");
        }
    }
}
