using System;
using System.Collections.Generic;
using System.Threading;
using FerramentaEMT.Core;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Core
{
    public class ProgressReportTests
    {
        [Fact]
        public void Fraction_is_clamped_between_0_and_1()
        {
            new ProgressReport(0, 10).Fraction.Should().Be(0d);
            new ProgressReport(5, 10).Fraction.Should().Be(0.5);
            new ProgressReport(10, 10).Fraction.Should().Be(1d);
            // nao deve explodir com current > total (erro do chamador)
            new ProgressReport(15, 10).Fraction.Should().Be(1d);
        }

        [Fact]
        public void Fraction_is_zero_when_total_is_zero_or_negative()
        {
            new ProgressReport(5, 0).Fraction.Should().Be(0d);
            new ProgressReport(5, -1).Fraction.Should().Be(0d);
        }

        [Fact]
        public void Negative_current_is_normalized_to_zero()
        {
            new ProgressReport(-3, 10).Current.Should().Be(0);
        }

        [Fact]
        public void Percent_is_fraction_times_100()
        {
            new ProgressReport(1, 4).Percent.Should().Be(25d);
        }

        [Fact]
        public void ToString_formats_with_total()
        {
            string s = new ProgressReport(3, 10, "ok").ToString();
            s.Should().Contain("3/10");
            s.Should().Contain("30%");
            s.Should().Contain("ok");
        }

        [Fact]
        public void ToString_formats_without_total()
        {
            string s = new ProgressReport(3, 0, "chugging").ToString();
            s.Should().Contain("3");
            s.Should().Contain("chugging");
            s.Should().NotContain("%");
        }
    }

    public class ProgressReporterTests
    {
        private sealed class CaptureProgress : IProgress<ProgressReport>
        {
            public List<ProgressReport> Events { get; } = new();
            public void Report(ProgressReport value) => Events.Add(value);
        }

        [Fact]
        public void Null_inner_progress_is_no_op()
        {
            ProgressReporter r = new ProgressReporter(null, 0);
            Action act = () =>
            {
                r.Report(1, 10);
                r.ReportFinal(10, 10);
            };
            act.Should().NotThrow();
        }

        [Fact]
        public void Throttle_suppresses_events_within_interval()
        {
            var sink = new CaptureProgress();
            var r = new ProgressReporter(sink, throttleMs: 10_000); // intervalo absurdo

            r.Report(1, 10); // primeiro emit sempre passa (lastEmit inicializado em -throttle)
            r.Report(2, 10);
            r.Report(3, 10);

            sink.Events.Should().HaveCount(1);
            sink.Events[0].Current.Should().Be(1);
        }

        [Fact]
        public void ReportFinal_always_propagates()
        {
            var sink = new CaptureProgress();
            var r = new ProgressReporter(sink, throttleMs: 10_000);

            r.Report(1, 10);
            r.Report(2, 10);
            r.ReportFinal(10, 10, "done");

            sink.Events.Should().HaveCount(2);
            sink.Events[0].Current.Should().Be(1);
            sink.Events[1].Current.Should().Be(10);
            sink.Events[1].Message.Should().Be("done");
        }

        [Fact]
        public void Throttle_lets_events_through_after_interval()
        {
            var sink = new CaptureProgress();
            var r = new ProgressReporter(sink, throttleMs: 5);

            r.Report(1, 10);
            Thread.Sleep(20);
            r.Report(2, 10);

            sink.Events.Should().HaveCount(2);
        }
    }
}
