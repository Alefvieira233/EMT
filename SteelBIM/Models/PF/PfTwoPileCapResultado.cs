using System.Collections.Generic;

namespace FerramentaEMT.Models.PF
{
    /// <summary>
    /// Resultado do <c>PfTwoPileCapRebarService.Execute</c>. Mantem o servico
    /// mudo (ADR-003): em vez de chamar <c>AppDialogService.Show*</c> direto,
    /// o servico preenche este DTO e o command (caller) decide a UX.
    /// </summary>
    public sealed class PfTwoPileCapResultado
    {
        /// <summary>True quando nenhum bloco elegivel foi selecionado.</summary>
        public bool SelecaoVazia { get; set; }

        /// <summary>Quantidade de hospedeiros (blocos) que entraram no loop.</summary>
        public int HostsProcessados { get; set; }

        /// <summary>Quantidade de hospedeiros que receberam ao menos uma armadura.</summary>
        public int HostsComSucesso { get; set; }

        /// <summary>Quantidade total de barras de armadura criadas.</summary>
        public int ArmadurasCriadas { get; set; }

        /// <summary>Mensagens de aviso por host (id + motivo). Limitado a 10 na UI.</summary>
        public List<string> Avisos { get; } = new List<string>();

        /// <summary>Texto formatado pronto para o caller exibir num dialog.</summary>
        public string ToResumo()
        {
            string resumo =
                "Hospedeiros processados: " + HostsProcessados + "\n" +
                "Hospedeiros com sucesso: " + HostsComSucesso + "\n" +
                "Armaduras criadas: " + ArmadurasCriadas;

            if (Avisos.Count > 0)
            {
                resumo += "\n\nOcorrencias:\n- ";
                resumo += string.Join("\n- ", Avisos.GetRange(0, System.Math.Min(10, Avisos.Count)));
            }

            return resumo;
        }
    }
}
