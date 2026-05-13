#nullable enable
using System;
using Autodesk.Revit.DB;

namespace FerramentaEMT.Utils
{
    /// <summary>
    /// Helper centralizado para configurar <see cref="IFailuresPreprocessor"/> em transacoes
    /// que rodam operacoes em lote (criacao de cotas, tags, vistas, etc.). Antes desta
    /// extracao, cada service implementava seu proprio <c>WarningSwallower</c> aninhado —
    /// o caso canonico era <c>CotarPecaFabricacaoService.WarningSwallower</c> (P1.1 do plano
    /// de lapidacao 2026-04-28).
    ///
    /// Sem preprocessor, warnings ruidosos do Revit ("dimension outside view", "joined
    /// geometry overlap", etc.) interrompem operacoes em lote com dialogo modal vermelho.
    /// </summary>
    internal static class FailureHandlingHelper
    {
        /// <summary>
        /// Configura a <paramref name="transaction"/> para deletar silenciosamente
        /// todos os <see cref="FailureSeverity.Warning"/> emitidos durante o commit.
        /// Erros (Error/DocumentCorruption) continuam interrompendo normalmente.
        /// </summary>
        /// <remarks>
        /// Deve ser chamado APOS <c>transaction.Start()</c> e ANTES da primeira operacao
        /// que pode emitir warning. Idempotente: chamar duas vezes apenas substitui o
        /// preprocessor anterior pelo mesmo tipo.
        /// </remarks>
        public static void SwallowWarnings(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            FailureHandlingOptions opts = transaction.GetFailureHandlingOptions();
            opts.SetFailuresPreprocessor(new WarningSwallower());
            transaction.SetFailureHandlingOptions(opts);
        }

        private sealed class WarningSwallower : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                if (failuresAccessor == null)
                    return FailureProcessingResult.Continue;

                var failures = failuresAccessor.GetFailureMessages();
                foreach (var f in failures)
                {
                    if (f.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(f);
                }
                return FailureProcessingResult.Continue;
            }
        }
    }
}
