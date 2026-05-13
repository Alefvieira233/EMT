using System;

namespace FerramentaEMT.Models
{
    public enum EscopoSelecaoPeca
    {
        SelecaoManual = 0,
        VistaAtiva = 1
    }

    public enum TipoVistaDetalhe
    {
        Longitudinal,
        Transversal
    }

    public enum VistaPecaCategoriaFiltro
    {
        Todos = 0,
        Pilares = 1,
        Vigas = 2
    }

    public sealed class GerarVistaPecaConfig
    {
        /// <summary>Escopo: selecao manual ou todos da vista ativa.</summary>
        public EscopoSelecaoPeca Escopo { get; set; } = EscopoSelecaoPeca.SelecaoManual;

        /// <summary>Criar vista longitudinal (elevacao lateral mostrando o comprimento total).</summary>
        public bool CriarVistaLongitudinal { get; set; } = true;

        /// <summary>Criar corte transversal (secao no meio da peca).</summary>
        public bool CriarCorteTransversal { get; set; } = true;

        /// <summary>Escala das vistas geradas (ex: 20 = 1:20).</summary>
        public int EscalaVista { get; set; } = 20;

        /// <summary>Margem em mm ao redor do elemento no crop da vista.</summary>
        public double MargemMm { get; set; } = 150;

        /// <summary>Profundidade do corte transversal em mm (metade para cada lado).</summary>
        public double ProfundidadeCorteTransversalMm { get; set; } = 100;

        /// <summary>Prefixo do nome das vistas geradas.</summary>
        public string PrefixoNome { get; set; } = "SD";

        /// <summary>Criar uma folha (ViewSheet) e colocar as vistas automaticamente.</summary>
        public bool CriarFolha { get; set; } = false;

        /// <summary>Nome da familia de folha de titulo (title block) a usar.</summary>
        public string FamiliaFolhaTitulo { get; set; } = string.Empty;

        /// <summary>Tipo da familia de folha de titulo.</summary>
        public string TipoFolhaTitulo { get; set; } = string.Empty;

        /// <summary>Filtro opcional de categoria usado por fluxos automatizados PF.</summary>
        public VistaPecaCategoriaFiltro FiltroCategoria { get; set; } = VistaPecaCategoriaFiltro.Todos;

        public bool TemVistasSelecionadas()
        {
            return CriarVistaLongitudinal || CriarCorteTransversal;
        }
    }
}
