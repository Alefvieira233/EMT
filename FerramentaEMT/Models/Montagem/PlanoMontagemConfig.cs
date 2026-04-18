using System;

namespace FerramentaEMT.Models.Montagem
{
    /// <summary>Escopo de elementos para o plano de montagem.</summary>
    public enum EscopoMontagem
    {
        /// <summary>Todos os elementos do modelo.</summary>
        ModeloInteiro = 0,

        /// <summary>Apenas elementos visíveis na vista ativa.</summary>
        VistaAtiva = 1,

        /// <summary>Apenas elementos selecionados manualmente.</summary>
        SelecaoManual = 2
    }

    /// <summary>Configuração do Plano de Montagem (Erection Plan).</summary>
    public sealed class PlanoMontagemConfig
    {
        /// <summary>Nome do parâmetro que armazena o número da etapa de montagem.</summary>
        public string NomeParametroEtapa { get; set; } = "EMT_Etapa_Montagem";

        /// <summary>Se true, aplica destaque visual (colorização) por etapa após atribuição.</summary>
        public bool AplicarDestaqueVisual { get; set; } = true;

        /// <summary>Se true, exporta relatório Excel após gerar o plano.</summary>
        public bool ExportarRelatorio { get; set; } = false;

        /// <summary>Caminho completo para salvar o arquivo Excel de relatório.</summary>
        public string CaminhoRelatorio { get; set; } = string.Empty;

        /// <summary>Escopo de elementos a considerar no plano.</summary>
        public EscopoMontagem Escopo { get; set; } = EscopoMontagem.VistaAtiva;
    }
}
