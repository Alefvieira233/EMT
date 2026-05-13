#nullable enable
using System;
using System.Collections.Generic;

namespace FerramentaEMT.Models
{
    /// <summary>
    /// Resumo do desfecho de uma operacao de cotagem (CotasService.Executar* e
    /// CriarCotaAlinhada). Carrega contadores e a mensagem de sucesso ja
    /// formatada para que o command apenas exiba — todo o trabalho de
    /// formatacao fica no service (P1.4 do plano de lapidacao 2026-04-28).
    ///
    /// Flag <see cref="Cancelado"/> distingue "user fechou seleção/dialog"
    /// de sucesso real — sem essa flag, o command nao sabe se mostra dialog
    /// de info ou retorna silenciosamente.
    /// </summary>
    public sealed class CotagemResumo
    {
        public int CotasCriadas { get; init; }

        public int ElementosCotados { get; init; }

        public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Texto pronto para o command exibir via <c>AppDialogService.ShowInfo</c>.
        /// <c>null</c> quando a operacao foi cancelada (sem dialog).
        /// </summary>
        public string? MensagemSucessoFormatada { get; init; }

        /// <summary>
        /// <c>true</c> quando o usuario abortou (Esc na selecao, fechou
        /// CotasModoWindow, OperationCanceledException da Revit API).
        /// O command deve retornar <c>Result.Cancelled</c> sem mostrar dialog.
        /// </summary>
        public bool Cancelado { get; init; }

        /// <summary>Atalho para criar um resumo cancelado (zerado).</summary>
        public static CotagemResumo CanceladoPeloUsuario() => new() { Cancelado = true };
    }
}
