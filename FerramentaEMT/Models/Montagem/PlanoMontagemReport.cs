using System;
using System.Collections.Generic;

namespace FerramentaEMT.Models.Montagem
{
    /// <summary>
    /// Relatório de um plano de montagem gerado, contendo a sequência de etapas
    /// e elementos agrupados por etapa, pronto para exportação ou visualização.
    /// </summary>
    public sealed class PlanoMontagemReport
    {
        /// <summary>Total de elementos com atribuição de etapa.</summary>
        public int TotalElementos { get; set; }

        /// <summary>Total de etapas distintas encontradas.</summary>
        public int TotalEtapas { get; set; }

        /// <summary>Lista de etapas com seus elementos.</summary>
        public List<EtapaMontagem> Etapas { get; set; } = new();

        /// <summary>Tempo gasto na geração do relatório.</summary>
        public TimeSpan Duracao { get; set; }

        public PlanoMontagemReport() { }

        public PlanoMontagemReport(int totalElementos, int totalEtapas, TimeSpan duracao)
        {
            TotalElementos = totalElementos;
            TotalEtapas = totalEtapas;
            Duracao = duracao;
        }
    }
}
