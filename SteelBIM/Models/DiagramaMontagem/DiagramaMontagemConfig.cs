namespace SteelBIM.Models.DiagramaMontagem
{
    /// <summary>
    /// Orientacao do plano de secao do diagrama de montagem.
    /// AUTO = plugin decide com base na geometria dos elementos selecionados.
    /// XZ = secao paralela ao eixo X (corte na direcao Y, util para vista frontal/lateral)
    /// YZ = secao paralela ao eixo Y (corte na direcao X, util para vista lateral)
    /// </summary>
    public enum OrientacaoDiagrama
    {
        Auto = 0,
        ParaleloEixoX = 1,
        ParaleloEixoY = 2
    }

    public sealed class DiagramaMontagemConfig
    {
        public OrientacaoDiagrama Orientacao { get; set; } = OrientacaoDiagrama.Auto;
        public double MargemMm { get; set; } = 500.0; // 50 cm de folga em volta dos elementos
        public bool MostrarEixos { get; set; } = true;
        public bool AdicionarCotasEntreEixos { get; set; } = true;
        public bool AdicionarTagsMarca { get; set; } = true;
        public string NomeVista { get; set; } = "Diagrama de Montagem";
    }
}
