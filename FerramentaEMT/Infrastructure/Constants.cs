namespace FerramentaEMT.Infrastructure
{
    /// <summary>
    /// Constantes globais do FerramentaEMT.
    /// Substitui magic numbers espalhados pelo codigo.
    /// </summary>
    /// <remarks>
    /// Convencao: TODAS as distancias em <b>milimetros</b> (mm), exceto quando
    /// indicado em comentario. Conversao para feet (unidade interna do Revit)
    /// usa <see cref="Autodesk.Revit.DB.UnitUtils"/> ou helpers de RevitUtils.
    /// </remarks>
    public static class Constants
    {
        /// <summary>
        /// Constantes relacionadas a tolerancia geometrica.
        /// </summary>
        public static class Tolerancia
        {
            /// <summary>Tolerancia padrao em mm para considerar dois pontos coincidentes.</summary>
            public const double PontosCoincidentesMm = 0.5;

            /// <summary>Tolerancia em mm para considerar duas linhas paralelas.</summary>
            public const double LinhasParalelasMm = 1.0;

            /// <summary>Tolerancia angular em graus para considerar perpendicular.</summary>
            public const double AnguloPerpendicularGraus = 1.0;

            /// <summary>Tolerancia em mm para alinhamento de cotas.</summary>
            public const double AlinhamentoCotaMm = 5.0;
        }

        /// <summary>
        /// Constantes para geracao de cotas.
        /// </summary>
        public static class Cotas
        {
            /// <summary>Offset padrao em mm da linha de cota em relacao a peca.</summary>
            public const double OffsetLinhaCotaMm = 150.0;

            /// <summary>Espacamento em mm entre linhas de cota empilhadas.</summary>
            public const double EspacamentoLinhasCotaMm = 100.0;

            /// <summary>Tamanho minimo em mm para uma cota ser desenhada.</summary>
            public const double TamanhoMinimoCotaMm = 5.0;

            /// <summary>Distancia em mm para colocar a cota fora dos limites da peca.</summary>
            public const double FolgaForaPecaMm = 20.0;
        }

        /// <summary>
        /// Constantes para vistas geradas.
        /// </summary>
        public static class Vistas
        {
            /// <summary>Padding em mm ao redor do bounding box da peca.</summary>
            public const double PaddingBoundingBoxMm = 200.0;

            /// <summary>Escala padrao para vista de fabricacao (1:N).</summary>
            public const int EscalaPadraoFabricacao = 25;

            /// <summary>Escala padrao para vista de detalhe.</summary>
            public const int EscalaPadraoDetalhe = 10;

            /// <summary>Escala padrao para vista geral.</summary>
            public const int EscalaPadraoGeral = 100;
        }

        /// <summary>
        /// Constantes para fabricacao e marcacao.
        /// </summary>
        public static class Fabricacao
        {
            /// <summary>Comprimento maximo padrao em mm de uma peca de aco (limite caminhao).</summary>
            public const double ComprimentoMaximoPecaMm = 12000.0;

            /// <summary>Comprimento minimo em mm para uma peca ser considerada valida.</summary>
            public const double ComprimentoMinimoPecaMm = 50.0;

            /// <summary>Diametro padrao em mm de furos para parafuso M16.</summary>
            public const double FuroPadraoM16Mm = 17.5;

            /// <summary>Diametro padrao em mm de furos para parafuso M20.</summary>
            public const double FuroPadraoM20Mm = 22.0;

            /// <summary>Prefixo padrao para marcas geradas automaticamente.</summary>
            public const string PrefixoMarcaPadrao = "M";
        }

        /// <summary>
        /// Constantes para listas de materiais e exportacao.
        /// </summary>
        public static class ListaMateriais
        {
            /// <summary>Nome padrao da aba do Excel.</summary>
            public const string NomeAbaPadrao = "Lista de Materiais";

            /// <summary>Nome padrao do arquivo Excel gerado.</summary>
            public const string PrefixoNomeArquivo = "ListaMateriais";

            /// <summary>Densidade do aco em kg/m³ (para conferir pesos calculados).</summary>
            public const double DensidadeAcoKgPorM3 = 7850.0;

            /// <summary>Limite em peso (kg) acima do qual uma peca e marcada como "alerta".</summary>
            public const double PesoAlertaKg = 5000.0;
        }

        /// <summary>
        /// Constantes de UI e comportamento de janelas.
        /// </summary>
        public static class Ui
        {
            /// <summary>Tempo em ms de delay antes de mostrar tooltip de loading.</summary>
            public const int DelayTooltipMs = 200;

            /// <summary>Largura padrao em pixels de janela WPF de configuracao.</summary>
            public const int LarguraJanelaConfigPx = 480;

            /// <summary>Altura maxima em pixels para janela com lista scrollavel.</summary>
            public const int AlturaMaximaListaPx = 600;
        }

        /// <summary>
        /// Identificadores GUID do plugin (fixos — nao mudar entre versoes!).
        /// </summary>
        public static class Identificadores
        {
            /// <summary>GUID Application Id do FerramentaEMT.</summary>
            public const string AddInApplicationId = "4F1C4FBE-1234-5678-90AB-CDEF12345678";

            /// <summary>VendorId no .addin manifest.</summary>
            public const string VendorId = "EMT";

            /// <summary>Descricao do vendor.</summary>
            public const string VendorDescription = "EMT Estruturas Metalicas";

            /// <summary>Nome do tab no ribbon do Revit.</summary>
            public const string RibbonTabName = "EMT";
        }
    }
}
