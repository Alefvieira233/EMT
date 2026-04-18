using System;

namespace FerramentaEMT.Core
{
    /// <summary>
    /// Evento de progresso de uma operacao longa, reportado via <see cref="IProgress{T}"/>.
    /// Pure C#, sem dependencia de Revit.
    ///
    /// O servico produz os eventos; a UI (WPF) consome e atualiza barra/status.
    /// A instancia e imutavel — sempre crie nova em cada etapa.
    /// </summary>
    public readonly struct ProgressReport
    {
        /// <summary>Numero de itens processados ate agora.</summary>
        public int Current { get; }

        /// <summary>Total de itens (0 = indeterminado).</summary>
        public int Total { get; }

        /// <summary>Mensagem amigavel para exibir na UI (opcional).</summary>
        public string Message { get; }

        public ProgressReport(int current, int total, string message = null)
        {
            Current = current < 0 ? 0 : current;
            Total = total < 0 ? 0 : total;
            Message = message;
        }

        /// <summary>Percentual entre 0 e 1. Retorna 0 quando total e desconhecido.</summary>
        public double Fraction
        {
            get
            {
                if (Total <= 0) return 0d;
                double f = (double)Current / Total;
                if (f < 0d) return 0d;
                if (f > 1d) return 1d;
                return f;
            }
        }

        /// <summary>Percentual entre 0 e 100.</summary>
        public double Percent => Fraction * 100d;

        public override string ToString()
        {
            if (Total > 0)
                return $"{Current}/{Total} ({Percent:F0}%) {Message}";
            return $"{Current} {Message}";
        }
    }
}
