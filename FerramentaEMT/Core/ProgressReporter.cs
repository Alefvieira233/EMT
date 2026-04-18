using System;
using System.Diagnostics;
using System.Threading;

namespace FerramentaEMT.Core
{
    /// <summary>
    /// Wrapper thread-safe sobre <see cref="IProgress{T}"/> com throttle por tempo.
    ///
    /// Evita floodar a UI com milhares de eventos em lacos curtos — so propaga o
    /// evento se passou pelo menos <see cref="ThrottleMs"/> ms desde o ultimo push
    /// (excecao: ultima etapa sempre e reportada via <see cref="ReportFinal"/>).
    ///
    /// Uso tipico em servico:
    /// <code>
    /// var reporter = new ProgressReporter(progress, throttleMs: 100);
    /// for (int i = 0; i &lt; items.Count; i++)
    /// {
    ///     ct.ThrowIfCancellationRequested();
    ///     Process(items[i]);
    ///     reporter.Report(i + 1, items.Count, $"Processando {items[i].Name}");
    /// }
    /// reporter.ReportFinal(items.Count, items.Count, "Concluido");
    /// </code>
    /// </summary>
    public sealed class ProgressReporter
    {
        private readonly IProgress<ProgressReport> _inner;
        private readonly int _throttleMs;
        private readonly Stopwatch _watch;
        private long _lastEmitMs;

        public ProgressReporter(IProgress<ProgressReport> inner, int throttleMs = 100)
        {
            _inner = inner; // null permitido — fica no-op (uso mais simples do caller)
            _throttleMs = throttleMs < 0 ? 0 : throttleMs;
            _watch = Stopwatch.StartNew();
            Interlocked.Exchange(ref _lastEmitMs, -_throttleMs); // libera primeiro push
        }

        /// <summary>Tempo minimo (ms) entre reports emitidos ao destino.</summary>
        public int ThrottleMs => _throttleMs;

        /// <summary>Report com throttle — pode ser descartado se vier dentro do intervalo.</summary>
        public void Report(int current, int total, string message = null)
        {
            if (_inner == null) return;

            long now = _watch.ElapsedMilliseconds;
            long last = Interlocked.Read(ref _lastEmitMs);
            if (now - last < _throttleMs) return;

            // Tentativa unica de reservar o slot de emit — se outro thread ganhou, nao emite.
            if (Interlocked.CompareExchange(ref _lastEmitMs, now, last) != last) return;

            _inner.Report(new ProgressReport(current, total, message));
        }

        /// <summary>Report que sempre propaga (ignora throttle). Use para etapa final ou marcos criticos.</summary>
        public void ReportFinal(int current, int total, string message = null)
        {
            if (_inner == null) return;
            Interlocked.Exchange(ref _lastEmitMs, _watch.ElapsedMilliseconds);
            _inner.Report(new ProgressReport(current, total, message));
        }
    }
}
