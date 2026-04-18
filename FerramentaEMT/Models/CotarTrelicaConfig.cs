#nullable enable
namespace FerramentaEMT.Models
{
    /// <summary>
    /// Configuracao da funcao Cotar Trelica (ver docs/PLANO-LAPIDACAO.md secao 6.1).
    /// DTO simples, populado pela CotarTrelicaWindow apos OK.
    /// </summary>
    public class CotarTrelicaConfig
    {
        /// <summary>Habilita a faixa 1 — paineis do banzo superior.</summary>
        public bool CotarPaineisBanzoSuperior { get; set; } = true;

        /// <summary>Habilita a faixa 2 — vaos entre apoios + vao total (faixa superior).</summary>
        public bool CotarVaosEntreApoios { get; set; } = true;

        /// <summary>Habilita a faixa 3 — paineis do banzo inferior.</summary>
        public bool CotarPaineisBanzoInferior { get; set; } = true;

        /// <summary>Habilita a faixa 4 — vao total (faixa inferior).</summary>
        public bool CotarVaoTotal { get; set; } = true;

        /// <summary>Habilita a faixa 5 — altura de cada montante (texto vertical).</summary>
        public bool CotarAlturaMontantes { get; set; } = true;

        /// <summary>Identifica cada barra com tag de perfil (2x L, W 200, etc.).</summary>
        public bool IdentificarPerfis { get; set; } = true;

        /// <summary>Trata cantoneiras (L) como pecas duplas (prefixo "2x"). Padrao EMT.</summary>
        public bool CantoneiraDupla { get; set; } = true;

        /// <summary>Offset vertical padrao (em mm) das faixas em relacao a trelica.</summary>
        public double OffsetFaixaMm { get; set; } = 500.0;
    }
}
