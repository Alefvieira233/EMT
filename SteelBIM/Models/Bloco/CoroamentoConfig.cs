#nullable enable
namespace SteelBIM.Models.Bloco
{
    /// <summary>
    /// v2.8.21: configuração do comando dedicado "Armadura de Fundação (Coroamento)".
    /// Monta a GAIOLA FECHADA do bloco de coroamento: malha de fundo (tração) + malha de topo +
    /// estribos perimetrais que fecham a gaiola, com cobrimento efetivo (cobrimento + Ø estribo)
    /// e desconto do topo da estaca (malha de fundo acima das estacas embutidas).
    /// </summary>
    public sealed class CoroamentoConfig
    {
        // Cobrimento e desconto da estaca
        public double CobrimentoBlocoCm { get; set; } = 5.0;          // cobrimento do bloco (4–5 cm)
        public double TopoEstacaEmbutidoCm { get; set; } = 5.0;       // estaca embutida; malha de fundo sobe

        // Fecha a gaiola: estribos perimetrais + ganchos para cima nas pontas da malha de fundo.
        public bool FecharGaiola { get; set; } = true;
        public double GanchoMalhaCm { get; set; } = 10.0;             // pata para cima nas pontas da malha

        // Malha de fundo (principal / tração) — obrigatória
        public string MalhaFundoBarType { get; set; } = string.Empty;
        public double MalhaFundoEspacamentoCm { get; set; } = 15.0;

        // Malha de topo (opcional)
        public bool LancarMalhaTopo { get; set; } = true;
        public string MalhaTopoBarType { get; set; } = string.Empty;
        public double MalhaTopoEspacamentoCm { get; set; } = 20.0;

        // Estribos perimetrais (fecham a gaiola) — obrigatórios quando FecharGaiola
        public string EstriboBarType { get; set; } = string.Empty;
        public double EstriboEspacamentoCm { get; set; } = 20.0;

        // Pele lateral (opcional)
        public bool LancarPeleLateral { get; set; }
        public string PeleBarType { get; set; } = string.Empty;
        public double PeleEspacamentoCm { get; set; } = 20.0;
    }
}
