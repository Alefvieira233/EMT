using System.Collections.Generic;

namespace FerramentaEMT.Models.ModelCheck
{
    /// <summary>
    /// Configuracao para a verificacao de modelo.
    /// Define quais regras executar e opcoes de escopo/exportacao.
    /// </summary>
    public sealed class ModelCheckConfig
    {
        // --- Regras a executar ---

        /// <summary>Buscar elementos estruturais sem material atribuido (STRUCTURAL_MATERIAL_PARAM).</summary>
        public bool RunMissingMaterial { get; set; } = true;

        /// <summary>Buscar elementos sem marca (ALL_MODEL_MARK vazio).</summary>
        public bool RunMissingMark { get; set; } = true;

        /// <summary>Buscar marcas duplicadas — mesma marca em elementos de tipo diferente.</summary>
        public bool RunDuplicateMark { get; set; } = true;

        /// <summary>Buscar elementos estruturais sobreposto (interseccao de bounding box + solid boolean).</summary>
        public bool RunOverlappingElements { get; set; } = true;

        /// <summary>Buscar elementos com simbolo ausente ou dimensoes zero (height/width).</summary>
        public bool RunMissingProfile { get; set; } = true;

        /// <summary>Buscar elementos estruturais com comprimento zero ou < 1mm.</summary>
        public bool RunZeroLength { get; set; } = true;

        /// <summary>Buscar elementos sem level (LevelId == InvalidElementId).</summary>
        public bool RunMissingLevel { get; set; } = true;

        /// <summary>Buscar instancias com simbolo ou familia nulos.</summary>
        public bool RunStructuralWithoutType { get; set; } = true;

        /// <summary>Buscar elementos sem comentario (info apenas, nao erro).</summary>
        public bool RunMissingComment { get; set; } = false;

        /// <summary>Buscar grupos orfaos (0 elementos).</summary>
        public bool RunOrphanGroup { get; set; } = true;

        // --- Verificacao de carimbo (TitleBlock) ---

        /// <summary>Habilitar verificacao dos parametros do carimbo nas folhas.</summary>
        public bool RunTitleBlockParameters { get; set; } = false;

        /// <summary>Se true, verificar apenas a folha ativa. Se false, todas as folhas do projeto.</summary>
        public bool TitleBlockScopeActiveSheetOnly { get; set; } = false;

        /// <summary>Nome da familia do carimbo a filtrar (vazio = qualquer familia).</summary>
        public string TitleBlockFamilyName { get; set; } = string.Empty;

        /// <summary>Nome do tipo do carimbo a filtrar (vazio = qualquer tipo).</summary>
        public string TitleBlockTypeName { get; set; } = string.Empty;

        /// <summary>Lista de nomes de parametros do carimbo a conferir (ex: "Projetista", "Revisao").</summary>
        public List<string> TitleBlockParameters { get; set; } = new List<string>();

        // --- Escopo ---

        /// <summary>Se true, analisar apenas elementos visiveis na vista ativa. Se false, modelo inteiro.</summary>
        public bool ScopeViewOnly { get; set; } = true;

        // --- Exportacao ---

        /// <summary>Se true, exportar relatorio em Excel apos execucao.</summary>
        public bool ExportExcel { get; set; } = false;

        /// <summary>Caminho do arquivo Excel de destino (se ExportExcel == true).</summary>
        public string ExcelPath { get; set; } = string.Empty;

        /// <summary>
        /// Conta quantas regras estao habilitadas.
        /// </summary>
        public int GetEnabledRulesCount()
        {
            int count = 0;
            if (RunMissingMaterial) count++;
            if (RunMissingMark) count++;
            if (RunDuplicateMark) count++;
            if (RunOverlappingElements) count++;
            if (RunMissingProfile) count++;
            if (RunZeroLength) count++;
            if (RunMissingLevel) count++;
            if (RunStructuralWithoutType) count++;
            if (RunMissingComment) count++;
            if (RunOrphanGroup) count++;
            if (RunTitleBlockParameters && TitleBlockParameters != null)
                count += TitleBlockParameters.Count;
            return count;
        }
    }
}
