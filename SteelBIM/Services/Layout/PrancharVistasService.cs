#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services.Layout
{
    /// <summary>
    /// v2.8.13 (Onda 3): "Pranchar Vistas" — cria a folha (title block escolhido) e distribui
    /// as vistas selecionadas em GRADE. Estratégia: coloca os viewports num ponto temporário,
    /// regenera, mede o footprint real (GetBoxOutline), roda o núcleo PURO Grade e reposiciona
    /// por delta (SetBoxCenter). Reusa o padrão do AutoVistaService.
    /// </summary>
    public sealed class PrancharVistasService
    {
        public void Executar(UIDocument uidoc, IList<View> vistas, PrancharVistasConfig config)
        {
            Document doc = uidoc.Document;

            FamilySymbol? tb = ObterTitleBlock(doc, config.FamiliaTitleBlock, config.TipoTitleBlock);
            if (tb == null)
            {
                AppDialogService.ShowError("Pranchar Vistas", "Nenhuma família de carimbo (title block) no projeto.", "Carimbo ausente");
                return;
            }

            List<View> ordenadas = Ordenar(vistas, config.Ordenar);
            int colocados = 0;
            var naoCoube = new List<string>();
            string numeroFolha = string.Empty;

            using (Transaction t = new Transaction(doc, "Pranchar Vistas"))
            {
                t.Start();
                if (!tb.IsActive)
                    tb.Activate();

                ViewSheet sheet = ViewSheet.Create(doc, tb.Id);
                if (!string.IsNullOrWhiteSpace(config.NumeroFolha))
                    TrySet(() => sheet.SheetNumber = config.NumeroFolha);
                if (!string.IsNullOrWhiteSpace(config.NomeFolha))
                    TrySet(() => sheet.Name = config.NomeFolha);
                numeroFolha = sheet.SheetNumber;

                doc.Regenerate();

                BoundingBoxUV ol = sheet.Outline;
                XYZ temp = new XYZ((ol.Min.U + ol.Max.U) / 2.0, (ol.Min.V + ol.Max.V) / 2.0, 0);

                var viewports = new List<Viewport>();
                foreach (View v in ordenadas)
                {
                    if (!Viewport.CanAddViewToSheet(doc, sheet.Id, v.Id))
                    {
                        naoCoube.Add(v.Name);
                        continue;
                    }

                    Viewport vp = Viewport.Create(doc, sheet.Id, v.Id, temp);
                    if (vp != null)
                        viewports.Add(vp);
                }

                doc.Regenerate();

                double x0 = ToMm(ol.Min.U);
                double y0 = ToMm(ol.Min.V);
                double x1 = ToMm(ol.Max.U) - (config.ReservaCarimboMm < 0 ? 0 : config.ReservaCarimboMm);
                double y1 = ToMm(ol.Max.V);
                var area = new AreaUtil(x0, y0, x1, y1);

                var caixas = new List<CaixaVista>();
                var dados = new Dictionary<string, (Viewport Vp, double CxMm, double CyMm, string Nome)>();
                foreach (Viewport vp in viewports)
                {
                    Outline bo = vp.GetBoxOutline();
                    double wMm = ToMm(bo.MaximumPoint.X - bo.MinimumPoint.X);
                    double hMm = ToMm(bo.MaximumPoint.Y - bo.MinimumPoint.Y);
                    double cxMm = ToMm((bo.MinimumPoint.X + bo.MaximumPoint.X) / 2.0);
                    double cyMm = ToMm((bo.MinimumPoint.Y + bo.MaximumPoint.Y) / 2.0);
                    string id = vp.Id.ToString();
                    caixas.Add(new CaixaVista(id, wMm, hMm, cxMm, cyMm));
                    dados[id] = (vp, cxMm, cyMm, vp.Name);
                }

                ResultadoGrade grade = LayoutVistasCalculator.Grade(
                    caixas, area, config.Colunas, config.MargemMm, config.EspacamentoMm);

                foreach (PosicaoVista pos in grade.Posicionados)
                {
                    if (!dados.TryGetValue(pos.Id, out var d))
                        continue;

                    double dxFt = ToFt(pos.CxMm - d.CxMm);
                    double dyFt = ToFt(pos.CyMm - d.CyMm);
                    XYZ c = d.Vp.GetBoxCenter();
                    d.Vp.SetBoxCenter(new XYZ(c.X + dxFt, c.Y + dyFt, c.Z));
                    colocados++;
                }

                foreach (string id in grade.NaoCoube)
                {
                    if (dados.TryGetValue(id, out var d))
                        naoCoube.Add(d.Nome);
                }

                t.Commit();
            }

            string resumo = $"Prancha {numeroFolha} criada.\n{colocados} vista(s) posicionada(s).";
            if (naoCoube.Count > 0)
                resumo += $"\n{naoCoube.Count} vista(s) NÃO couberam (reduza a escala ou use folha maior): {string.Join(", ", naoCoube.Take(8))}";

            Logger.Info("[PrancharVistas] folha={N} colocados={C} naoCoube={NC}", numeroFolha, colocados, naoCoube.Count);
            AppDialogService.ShowInfo("Pranchar Vistas", resumo, naoCoube.Count > 0 ? "Concluído (com avisos)" : "Concluído");
        }

        private static List<View> Ordenar(IList<View> vistas, OrdenacaoVista modo)
        {
            switch (modo)
            {
                case OrdenacaoVista.Escala:
                    return vistas.OrderBy(v => v.Scale).ThenBy(v => v.Name, OrdenacaoNatural.Comparer).ToList();
                case OrdenacaoVista.Selecao:
                    return vistas.ToList();
                default:
                    return vistas.OrderBy(v => v.Name, OrdenacaoNatural.Comparer).ToList();
            }
        }

        private static FamilySymbol? ObterTitleBlock(Document doc, string familia, string tipo)
        {
            List<FamilySymbol> simbolos = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();
            if (simbolos.Count == 0)
                return null;

            FamilySymbol? exato = simbolos.FirstOrDefault(s => s.Family?.Name == familia && s.Name == tipo);
            return exato ?? simbolos.First();
        }

        private static void TrySet(System.Action acao)
        {
            try
            {
                acao();
            }
            catch
            {
                // valor duplicado/inválido: mantém o gerado pelo Revit.
            }
        }

        private static double ToMm(double ft) => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);

        private static double ToFt(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
