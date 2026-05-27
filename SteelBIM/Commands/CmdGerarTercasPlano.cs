#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SteelBIM.Models;
using SteelBIM.Services;
using SteelBIM.Utils;
using SteelBIM.Views;

namespace SteelBIM.Commands
{
    /// <summary>
    /// Comando "Gerar Terças por Plano".
    ///
    /// <para>v2.8.1 (Victor): adicionou 2 picks antes da janela pra evitar
    /// que o usuario tenha que digitar valores manualmente:</para>
    /// <list type="number">
    ///   <item><b>Pick viga de referencia</b>: extrai o angulo real da inclinacao
    ///         da viga (Esc = fallback pro angulo do plano de trabalho).
    ///         Resultado pre-preenche o campo "Angulo da seção" na janela.</item>
    ///   <item><b>Pick linha limite inicial</b>: extrai o vao em cm.
    ///         Resultado pre-preenche o campo "Vao total" na TercasSpacingWindow.
    ///         O elemento + linha pickados sao repassados ao TercasService
    ///         pra evitar pick duplicado.</item>
    /// </list>
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarTercasPlano : FerramentaCommandBase
    {
        protected override string CommandName => "Gerar Terças por Plano";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            Plane? plane = ObterPlanoDoPlanoDeTrabalhoAtual(doc);
            if (plane == null)
                return Result.Cancelled;

            List<FamilySymbol> listaPerfis = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .ToList();

            if (listaPerfis.Count == 0)
            {
                AppDialogService.ShowWarning(CommandName, "Nenhuma familia estrutural foi encontrada.", "Perfis nao encontrados");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();

            // v2.8.1 (Victor): inclinacao base = normal do plano de trabalho.
            // Usada como fallback quando usuario aperta Esc no pick da viga ref.
            double inclinacaoGraus = Math.Acos(Math.Abs(plane.Normal.Z)) * 180.0 / Math.PI;
            inclinacaoGraus = Math.Round(Math.Min(90.0, Math.Max(0.0, inclinacaoGraus)), 2);

            // Pick viga de referencia: extrai angulo real da inclinacao.
            // Esc pula essa etapa e mantem o angulo do plano de trabalho.
            try
            {
                Reference refViga = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    "Selecione a viga de referencia para o angulo da secao (Esc = usar angulo do plano)");

                Element elViga = doc.GetElement(refViga);
                Curve? curva = RevitUtils.GetElementCurve(elViga);
                if (curva != null)
                {
                    XYZ p1 = curva.GetEndPoint(0);
                    XYZ p2 = curva.GetEndPoint(1);
                    double dX = p2.X - p1.X;
                    double dY = p2.Y - p1.Y;
                    double dZ = p2.Z - p1.Z;
                    double dH = Math.Sqrt(dX * dX + dY * dY);
                    double anguloRad = Math.Atan2(Math.Abs(dZ), dH);
                    inclinacaoGraus = -Math.Round(anguloRad * 180.0 / Math.PI, 2);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Esc — manter inclinacao do plano de trabalho
            }

            // Pick antecipado da linha limite inicial: extrai o vao em cm pra
            // pre-preencher o campo "Vao total" na TercasSpacingWindow.
            // O elemento + linha ja pickados sao repassados ao servico pra
            // evitar pick duplicado.
            Element? elLimACmd = null;
            Line? lineACmd = null;
            double vaoRefCm = 600.0;

            if (!RevitUtils.TryGetLineFromPickedElement(
                    uidoc,
                    "Selecione a LINHA LIMITE INICIAL (define o vao de referencia)",
                    out elLimACmd,
                    out lineACmd))
                return Result.Cancelled;

            if (lineACmd != null)
                vaoRefCm = Math.Round(lineACmd.Length / RevitUtils.FT_PER_CM, 1);

            TercasWindow wnd = new TercasWindow(listaPerfis, settings, inclinacaoGraus, vaoRefCm);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            TercasConfig config = wnd.BuildConfig();
            if (config == null || config.SymbolSelecionado == null)
            {
                AppDialogService.ShowWarning(CommandName, "Configuracao invalida.", "Dados incompletos");
                return Result.Failed;
            }

            TercasService service = new TercasService();
            return service.Executar(uidoc, doc, config, plane, elLimACmd, lineACmd);
        }

        private Plane? ObterPlanoDoPlanoDeTrabalhoAtual(Document doc)
        {
            View? vistaAtiva = doc?.ActiveView;
            SketchPlane? sketchPlane = vistaAtiva?.SketchPlane;
            if (sketchPlane == null)
            {
                AppDialogService.ShowWarning(
                    CommandName,
                    "A vista ativa nao possui um plano de trabalho definido.\n\nDefina o plano de trabalho no Revit e execute o comando novamente.",
                    "Plano de trabalho ausente");
                return null;
            }

            Plane? plane = sketchPlane.GetPlane();
            if (plane == null)
            {
                AppDialogService.ShowError(CommandName, "Nao foi possivel obter a geometria do plano de trabalho atual.", "Falha ao ler plano");
                return null;
            }

            return plane;
        }
    }
}
