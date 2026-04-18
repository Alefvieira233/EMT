using System;
using System.Collections.Generic;

namespace FerramentaEMT.Models.Montagem
{
    /// <summary>
    /// Representa uma etapa de montagem (sequência de erection).
    /// Cada etapa agrupa elementos estruturais que devem ser montados juntos.
    /// </summary>
    public class EtapaMontagem
    {
        /// <summary>Número da etapa (1..N).</summary>
        public int Numero { get; set; }

        /// <summary>Descrição textual da etapa (ex: "Fundações", "Estrutura principal").</summary>
        public string Descricao { get; set; } = string.Empty;

        /// <summary>Data planejada para início da montagem desta etapa.</summary>
        public DateTime? DataPlanejada { get; set; }

        /// <summary>
        /// Lista de IDs de elementos estruturais associados a esta etapa.
        /// Armazenamos como List de long para facilitar serialização (ElementId.Value).
        /// </summary>
        public List<long> ElementIds { get; set; } = new();

        public EtapaMontagem() { }

        public EtapaMontagem(int numero, string descricao = "")
        {
            Numero = numero;
            Descricao = descricao ?? string.Empty;
        }
    }
}
