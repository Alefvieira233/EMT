using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    /// <summary>
    /// Resultado do ContraventamentoPlanoService.Executar.
    /// ADR-003: Service nao chama AppDialogService. Popula este DTO
    /// e o caller (Command) decide como apresentar (Warning, Error,
    /// Info ou silencioso).
    /// </summary>
    public sealed class ContraventamentoPlanoResultado
    {
        // ---- Flags de erro (tecnicos) ----
        public bool DocumentoIndisponivel { get; set; }
        public bool PlanoInvalido { get; set; }
        public bool ConfiguracaoInvalida { get; set; }
        public bool EixosInvalidos { get; set; }
        public bool NivelNaoEncontrado { get; set; }

        // ---- Flags de erro logico (usuario) ----
        public bool PontosInvalidos { get; set; }    // C1/C2 nao formam painel valido
        public bool PreviewCancelado { get; set; }   // usuario cancelou na confirmacao
        public bool PickPointCancelado { get; set; } // usuario ESC durante pick
        public bool NenhumaBarraCriada { get; set; } // sucesso parcial

        // ---- Dados de sucesso ----
        public bool Sucesso { get; set; }
        public int BarrasCriadas { get; set; }
        public string PerfilNome { get; set; } = string.Empty;
        public double LarguraMm { get; set; }
        public double AlturaMm { get; set; }
        public double OffsetSegundaDiagonalMm { get; set; }
        public List<ElementId> IdsBarrasCriadas { get; } = new List<ElementId>();

        /// <summary>
        /// Texto formatado pronto pro Command exibir em dialog de sucesso.
        /// </summary>
        public string ToResumoSucesso()
        {
            return "Contraventamento criado com sucesso.\n\n" +
                   "Perfil: " + PerfilNome + "\n" +
                   "Barras criadas: " + BarrasCriadas + "\n" +
                   "Largura do painel: " + System.Math.Round(LarguraMm, 0) + " mm\n" +
                   "Altura do painel: " + System.Math.Round(AlturaMm, 0) + " mm\n" +
                   "Offset da segunda diagonal: " + System.Math.Round(OffsetSegundaDiagonalMm, 1) + " mm";
        }
    }
}
