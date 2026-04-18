namespace FerramentaEMT.Models.CncExport
{
    public enum EscopoExportacaoDstv
    {
        SelecaoManual = 0,
        VistaAtiva = 1,
        ModeloInteiro = 2
    }

    public enum AgrupamentoArquivosDstv
    {
        /// <summary>Um arquivo .nc1 por instancia (ID).</summary>
        UmPorInstancia = 0,

        /// <summary>Um arquivo .nc1 por marca (peciamento), com Quantity = numero de instancias.</summary>
        UmPorMarca = 1
    }

    public sealed class ExportarDstvConfig
    {
        public EscopoExportacaoDstv Escopo { get; set; } = EscopoExportacaoDstv.SelecaoManual;

        /// <summary>Pasta de destino para os arquivos .nc1.</summary>
        public string PastaDestino { get; set; } = "";

        /// <summary>Tipo de agrupamento dos arquivos.</summary>
        public AgrupamentoArquivosDstv Agrupamento { get; set; } = AgrupamentoArquivosDstv.UmPorMarca;

        /// <summary>
        /// Codigo do projeto/pedido (vai no campo OrderNumber do bloco ST).
        /// Se vazio, usa o nome do arquivo Revit.
        /// </summary>
        public string CodigoProjeto { get; set; } = "";

        /// <summary>Numero da fase de fabricacao (default "1").</summary>
        public string Fase { get; set; } = "1";

        /// <summary>Tratamento de superficie padrao (deixe vazio para usar parametro do elemento).</summary>
        public string TratamentoSuperficiePadrao { get; set; } = "";

        /// <summary>
        /// Nome do parametro a ler para obter a piece mark.
        /// Se vazio, usa BuiltInParameter.ALL_MODEL_MARK.
        /// </summary>
        public string NomeParametroMarca { get; set; } = "";

        /// <summary>
        /// Categoria de pecas a exportar.
        /// </summary>
        public bool ExportarVigas { get; set; } = true;
        public bool ExportarPilares { get; set; } = true;
        public bool ExportarContraventamentos { get; set; } = true;

        /// <summary>
        /// Se true, gera arquivo de relatorio (.txt) com lista de pecas exportadas
        /// e quaisquer warnings.
        /// </summary>
        public bool GerarRelatorio { get; set; } = true;

        /// <summary>
        /// Se true, abre a pasta de destino no Explorer apos a exportacao.
        /// </summary>
        public bool AbrirPastaAposExportar { get; set; } = true;

        /// <summary>
        /// Permite sobrescrever arquivos .nc1 existentes na pasta destino.
        /// </summary>
        public bool SobrescreverExistentes { get; set; } = true;
    }
}
