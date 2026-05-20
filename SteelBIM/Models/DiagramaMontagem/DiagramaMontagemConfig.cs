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

        // === Cotagem vertical e total (novo v2.4.0) ===

        /// <summary>Adiciona SpotElevation em niveis chave (base/topo pilar, vigas, cumeeira) na lateral direita.</summary>
        public bool AdicionarCotasVerticais { get; set; } = true;

        /// <summary>Tolerancia para clusterizar elevacoes proximas como "mesmo nivel". Default 100mm = 10cm.</summary>
        public double ToleranciaClusterizacaoMm { get; set; } = 100.0;

        /// <summary>Adiciona cota total da extensao horizontal acima das cotas entre eixos.</summary>
        public bool AdicionarCotaTotalConjunto { get; set; } = true;

        /// <summary>Mostra bubble dos Levels estruturais do projeto visiveis na vista.</summary>
        public bool MostrarSimboloDeNivel { get; set; } = true;

        // === Folha (novo v2.4.0) ===

        /// <summary>Cria uma ViewSheet (folha) com TitleBlock e coloca a Section View como Viewport.</summary>
        public bool ColocarEmFolha { get; set; } = false;

        /// <summary>Numero da folha (preenche sheet.SheetNumber). Vazio = gera automatico.</summary>
        public string NumeroFolha { get; set; } = "EM-XX";

        /// <summary>Nome/titulo da folha (preenche sheet.Name). Vazio = usa nome da vista.</summary>
        public string NomeFolha { get; set; } = "";

        // === Comprimentos individuais (v2.6.5: Dimension real; v2.6.6: offset adaptativo) ===

        /// <summary>
        /// Cria uma <c>Dimension</c> por peca usando FamilyInstance.GetReferences(Left/Right).
        /// Quando o length geometrico diverge mais de 5mm do STRUCTURAL_FRAME_CUT_LENGTH,
        /// aplica <c>ValueOverride</c> para mostrar o comprimento de fabricacao.
        /// Refatorado em v2.6.5 (antes v2.4.0 criava TextNote experimental).
        /// </summary>
        public bool AdicionarComprimentosIndividuais { get; set; } = false;

        /// <summary>
        /// Folga (mm) entre a face externa do perfil e a linha da cota individual no
        /// Diagrama de Montagem. Default 35mm garante leitura confortavel sem colidir
        /// com o perfil, independente da seccao (U75, U100, W360, etc).
        /// </summary>
        /// <remarks>
        /// v2.6.6: substitui offset fixo de 200mm da v2.6.5 por offset adaptativo
        /// <c>max(sectionDepth, sectionWidth)/2 + clearance</c>. Lido do FamilySymbol
        /// via BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT/WIDTH.
        /// Configuravel via codigo; UI fica pra v2.7.0+.
        /// </remarks>
        public double ClearanceCotaIndividualMm { get; set; } = 35.0;
    }
}
