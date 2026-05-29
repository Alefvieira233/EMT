using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SteelBIM.Infrastructure;

namespace SteelBIM.Services.DiagramaMontagem
{
    /// <summary>
    /// v2.8.8 — Failure preprocessor que SUPRIME o dialog modal "Excluir cotas"
    /// (e similares) e faz auto-cleanup das cotas que o Revit considera invalidas
    /// no commit.
    ///
    /// Por que isso e necessario: <see cref="Document.NewDimension(View, Line, ReferenceArray)"/>
    /// pode ACEITAR criar um objeto Dimension mesmo quando a Line de cota nao
    /// alinha com as Refs, ou a Ref nao esta visivel na vista. O Revit so
    /// detecta a inconsistencia ao chamar <see cref="Transaction.Commit"/> e
    /// abre um dialog modal pedindo pro usuario excluir as cotas — UX ruim
    /// (Alef reportou em v2.8.7).
    ///
    /// Estrategia: capturar QUALQUER failure de severity <see cref="FailureSeverity.Error"/>
    /// cujos failing elements incluam <see cref="Dimension"/> ou <see cref="SpotDimension"/>.
    /// Em vez de tentar mapear ids especificos de <c>BuiltInFailures.DimensionFailures</c>
    /// (que podem mudar entre versoes do Revit), o handler atua por tipo de elemento.
    /// Isso e' tolerante a futuros nomes de failure novos.
    ///
    /// Tambem absorve <see cref="FailureSeverity.Warning"/> em cotas — alinhado com
    /// <see cref="Utils.FailureHandlingHelper.SwallowWarnings"/> que ja' usamos
    /// noutros services.
    ///
    /// Apos validacao em campo, este pattern pode ser replicado em outros services
    /// que criam Dimension/SpotElevation (Cotar Trelica, Auto-Vista, Cotar Peca
    /// Fabricacao, Plano de Montagem).
    /// </summary>
    public sealed class SuppressInvalidDimensionsHandler : IFailuresPreprocessor
    {
        private readonly Document _doc;

        public SuppressInvalidDimensionsHandler(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Contador de cotas suprimidas por instancia do handler. Permite ao
        /// caller logar resumo apos commit ("X cotas suprimidas durante a operacao").
        /// </summary>
        public int CotasSuprimidas { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            if (a == null)
                return FailureProcessingResult.Continue;

            IList<FailureMessageAccessor> fails = a.GetFailureMessages();
            if (fails == null || fails.Count == 0)
                return FailureProcessingResult.Continue;

            bool deletouAlgo = false;
            bool resolveuWarnings = false;

            foreach (FailureMessageAccessor f in fails)
            {
                FailureSeverity sev = f.GetSeverity();

                // Warnings em cotas: suprimir sempre (alinhado com SwallowWarnings).
                if (sev == FailureSeverity.Warning)
                {
                    a.DeleteWarning(f);
                    resolveuWarnings = true;
                    continue;
                }

                // Errors: so' deletar elementos se forem Dimension/SpotDimension.
                // Outros errors (ex: transaction corrompida) sobem normalmente.
                if (sev != FailureSeverity.Error)
                    continue;

                ICollection<ElementId> ids = f.GetFailingElementIds();
                if (ids == null || ids.Count == 0)
                    continue;

                var idsParaDeletar = new List<ElementId>();
                foreach (ElementId id in ids)
                {
                    Element el = _doc.GetElement(id);
                    if (el is Dimension || el is SpotDimension)
                        idsParaDeletar.Add(id);
                }

                if (idsParaDeletar.Count > 0)
                {
                    CotasSuprimidas += idsParaDeletar.Count;
                    Logger.Warn(
                        "[DiagramaMontagem] Suprimindo {N} cota(s) invalida(s) — auto-cleanup pra evitar dialog modal. Causa: {Causa}",
                        idsParaDeletar.Count, f.GetDescriptionText());
                    a.DeleteElements(idsParaDeletar);
                    a.DeleteWarning(f);
                    deletouAlgo = true;
                }
            }

            // ProceedWithCommit so' quando deletamos elementos (acao destrutiva).
            // Pra warnings simples, Continue basta — Revit continua o commit
            // sem o warning mostrado ao usuario.
            return deletouAlgo
                ? FailureProcessingResult.ProceedWithCommit
                : (resolveuWarnings ? FailureProcessingResult.Continue : FailureProcessingResult.Continue);
        }
    }
}
