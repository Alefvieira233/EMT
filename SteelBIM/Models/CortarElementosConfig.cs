#nullable enable
namespace SteelBIM.Models
{
    public enum CortarElementosEscopo
    {
        Selecao = 0,
        VistaAtiva = 1,
        Modelo = 2
    }

    public sealed class CortarElementosConfig
    {
        public CortarElementosEscopo Escopo { get; set; } = CortarElementosEscopo.VistaAtiva;
        public bool IncluirPisos { get; set; } = true;
        public bool IncluirQuadroEstrutural { get; set; } = true;
        public bool IncluirParedes { get; set; } = false;
        public bool IncluirPilaresEstruturais { get; set; } = true;
        public bool IncluirColunas { get; set; } = true;
    }
}
