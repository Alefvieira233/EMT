#nullable enable
namespace SteelBIM.Models
{
    public enum OrdenacaoVista
    {
        /// <summary>Ordem natural por nome (TR-2 antes de TR-10).</summary>
        Nome = 0,
        /// <summary>Agrupa por escala (todas as 1:25 juntas), depois nome.</summary>
        Escala = 1,
        /// <summary>Mantem a ordem de seleção do usuário.</summary>
        Selecao = 2
    }

    /// <summary>
    /// v2.8.13 (Onda 3): configuração de "Pranchar Vistas" — cria a folha (title block escolhido)
    /// e distribui as vistas selecionadas em grade.
    /// </summary>
    public sealed class PrancharVistasConfig
    {
        // Title block (formato A1/A2/…): família + tipo escolhidos.
        public string FamiliaTitleBlock { get; set; } = string.Empty;
        public string TipoTitleBlock { get; set; } = string.Empty;

        // Layout (mm).
        public double MargemMm { get; set; } = 20.0;
        public double EspacamentoMm { get; set; } = 10.0;

        /// <summary>Largura reservada à DIREITA para o carimbo/selo (mm). Default 0.</summary>
        public double ReservaCarimboMm { get; set; } = 0.0;

        /// <summary>Nº de colunas; null/0 = automático (calcula pela largura).</summary>
        public int? Colunas { get; set; } = null;

        public OrdenacaoVista Ordenar { get; set; } = OrdenacaoVista.Nome;

        // Numeração/nome da folha nova.
        public string NumeroFolha { get; set; } = string.Empty;
        public string NomeFolha { get; set; } = "PRANCHA";
    }
}
