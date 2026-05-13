using System;

namespace FerramentaEMT.Models
{
    public enum ListaMateriaisEscopo
    {
        ModeloInteiro = 0,
        VistaAtiva = 1,
        SelecaoAtual = 2
    }

    public sealed class ExportarListaMateriaisConfig
    {
        public ListaMateriaisEscopo Escopo { get; set; } = ListaMateriaisEscopo.VistaAtiva;
        public bool IncluirVigas { get; set; } = true;
        public bool IncluirPilares { get; set; } = true;
        public bool IncluirFundacoes { get; set; } = true;
        public bool IncluirContraventamentos { get; set; } = true;
        public bool IncluirChapasConexoes { get; set; } = true;
        public bool ExportarPerfisLineares { get; set; } = true;
        public bool ExportarChapas { get; set; } = true;
        public bool ExportarResumo { get; set; } = true;
        public string CaminhoArquivo { get; set; } = string.Empty;

        public bool TemCategoriaSelecionada()
        {
            return IncluirVigas || IncluirPilares || IncluirFundacoes || IncluirContraventamentos || IncluirChapasConexoes;
        }

        public bool TemAbaSelecionada()
        {
            return ExportarPerfisLineares || ExportarChapas || ExportarResumo;
        }

        public string SugerirNomeArquivo()
        {
            return $"ListaMateriais_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }
    }
}
