using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Services
{
    internal static class AgrupamentoVisualService
    {
        private const string PrefixoGruposPilares = "EMT_COL_";
        private const string PrefixoGruposVigas = "EMT_VIG_";

        private static readonly Color[] PaletaCores =
        {
            new Color(220, 20, 60),
            new Color(30, 144, 255),
            new Color(34, 139, 34),
            new Color(255, 140, 0),
            new Color(148, 0, 211),
            new Color(0, 139, 139),
            new Color(205, 92, 92),
            new Color(70, 130, 180)
        };

        public static Result AgruparPilares(UIDocument uidoc, ref string message)
        {
            return AgruparPorTipo(
                uidoc,
                BuiltInCategory.OST_StructuralColumns,
                "Agrupar Pilares",
                PrefixoGruposPilares,
                ref message);
        }

        public static Result AgruparVigas(UIDocument uidoc, ref string message)
        {
            return AgruparPorTipo(
                uidoc,
                BuiltInCategory.OST_StructuralFraming,
                "Agrupar Vigas",
                PrefixoGruposVigas,
                ref message);
        }

        public static Result LimparAgrupamentos(UIDocument uidoc, ref string message)
        {
            if (uidoc is null)
            {
                AppDialogService.ShowError("Limpar Cores e Grupos", "UIDocument nulo.");
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            List<ElementId> idsPilares = ColetarElementosDaVista(doc, view, BuiltInCategory.OST_StructuralColumns, incluirElementosEmGruposDoUsuario: true)
                .Select(x => x.Id)
                .ToList();

            List<ElementId> idsVigas = ColetarElementosDaVista(doc, view, BuiltInCategory.OST_StructuralFraming, incluirElementosEmGruposDoUsuario: true)
                .Select(x => x.Id)
                .ToList();

            int gruposDesfeitos = 0;

            using (Transaction t = new Transaction(doc, "Limpar Cores e Grupos EMT"))
            {
                t.Start();

                OverrideGraphicSettings limpar = new OverrideGraphicSettings();
                foreach (ElementId id in idsPilares.Concat(idsVigas).Distinct())
                    view.SetElementOverrides(id, limpar);

                gruposDesfeitos += DesfazerGruposCriados(doc, PrefixoGruposPilares);
                gruposDesfeitos += DesfazerGruposCriados(doc, PrefixoGruposVigas);

                t.Commit();
            }

            uidoc.Selection.SetElementIds(new List<ElementId>());

            AppDialogService.ShowInfo(
                "Limpar Cores e Grupos",
                "Limpeza concluida." +
                $"\n\nOverrides removidos na vista: {idsPilares.Count + idsVigas.Count}" +
                $"\nGrupos EMT desfeitos: {gruposDesfeitos}",
                "Limpeza concluida");

            return Result.Succeeded;
        }

        private static Result AgruparPorTipo(
            UIDocument uidoc,
            BuiltInCategory categoria,
            string titulo,
            string prefixoGrupo,
            ref string message)
        {
            if (uidoc is null)
            {
                AppDialogService.ShowError(titulo, "UIDocument nulo.");
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            List<FamilyInstance> elementos = ColetarElementosDaVista(doc, view, categoria, incluirElementosEmGruposDoUsuario: false);
            if (elementos.Count == 0)
            {
                AppDialogService.ShowWarning(titulo, "Nenhum elemento estrutural valido foi encontrado na vista ativa.", "Nada para agrupar");
                return Result.Cancelled;
            }

            List<IGrouping<string, FamilyInstance>> gruposPorTipo = elementos
                .GroupBy(CriarAssinaturaEquivalencia)
                .OrderBy(x => DescreverElemento(x.FirstOrDefault(), doc))
                .ToList();

            bool criarGruposNativos = categoria != BuiltInCategory.OST_StructuralColumns;
            int gruposCriados = 0;
            int conjuntosColoridos = 0;
            int conjuntosSomenteVisuais = 0;
            int elementosColoridos = 0;
            int gruposDesfeitosAntes = 0;
            List<string> falhas = new List<string>();

            using (Transaction t = new Transaction(doc, titulo))
            {
                t.Start();

                gruposDesfeitosAntes += DesfazerGruposCriados(doc, prefixoGrupo);

                int indiceCor = 0;
                foreach (IGrouping<string, FamilyInstance> grupoPorTipo in gruposPorTipo)
                {
                    List<ElementId> ids = grupoPorTipo
                        .Select(x => x.Id)
                        .Distinct()
                        .ToList();

                    if (ids.Count == 0)
                        continue;

                    OverrideGraphicSettings overrideGrafico = CriarOverride(PaletaCores[indiceCor % PaletaCores.Length]);
                    foreach (ElementId id in ids)
                        view.SetElementOverrides(id, overrideGrafico);

                    conjuntosColoridos++;
                    elementosColoridos += ids.Count;
                    indiceCor++;

                    if (ids.Count < 2)
                        continue;

                    if (!criarGruposNativos)
                    {
                        conjuntosSomenteVisuais++;
                        continue;
                    }

                    try
                    {
                        Group group = doc.Create.NewGroup(ids);
                        doc.Regenerate();
                        string descricaoGrupo = DescreverElemento(grupoPorTipo.FirstOrDefault(), doc);
                        RenomearGrupo(doc, group, prefixoGrupo + SanitizarNome(descricaoGrupo));
                        gruposCriados++;
                    }
                    catch (Exception ex)
                    {
                        falhas.Add($"{DescreverElemento(grupoPorTipo.FirstOrDefault(), doc)}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            uidoc.Selection.SetElementIds(elementos.Select(x => x.Id).ToList());

            StringBuilder resumo = new StringBuilder();
            resumo.AppendLine("Processamento concluido.");
            resumo.AppendLine();
            resumo.AppendLine($"Elementos na vista: {elementos.Count}");
            resumo.AppendLine($"Conjuntos exatamente identicos: {gruposPorTipo.Count}");
            resumo.AppendLine($"Conjuntos coloridos: {conjuntosColoridos}");
            resumo.AppendLine($"Elementos com override: {elementosColoridos}");
            if (criarGruposNativos)
                resumo.AppendLine($"Grupos EMT criados: {gruposCriados}");
            else
                resumo.AppendLine($"Conjuntos mantidos apenas no agrupamento visual: {conjuntosSomenteVisuais}");
            resumo.AppendLine($"Grupos EMT antigos desfeitos antes da recriacao: {gruposDesfeitosAntes}");

            if (!criarGruposNativos)
            {
                resumo.AppendLine();
                resumo.AppendLine("Observacao:");
                resumo.AppendLine("Para pilares, o comando evita criar grupos nativos do Revit para nao desanexar membros dos eixos.");
            }

            if (falhas.Count > 0)
            {
                resumo.AppendLine();
                resumo.AppendLine("Falhas ao criar alguns grupos:");
                foreach (string falha in falhas.Take(6))
                    resumo.AppendLine("• " + falha);
                if (falhas.Count > 6)
                    resumo.AppendLine($"• ... e mais {falhas.Count - 6}.");
            }

            AppDialogService.ShowInfo(titulo, resumo.ToString(), "Processamento concluido");
            return Result.Succeeded;
        }

        private static List<FamilyInstance> ColetarElementosDaVista(
            Document doc,
            View view,
            BuiltInCategory categoria,
            bool incluirElementosEmGruposDoUsuario)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(categoria)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(x => x.Category != null)
                .Where(x => incluirElementosEmGruposDoUsuario || PodeParticiparDoAgrupamento(doc, x))
                .ToList();
        }

        private static bool PodeParticiparDoAgrupamento(Document doc, Element elemento)
        {
            if (elemento == null)
                return false;

            if (elemento.GroupId == ElementId.InvalidElementId)
                return true;

            Group grupo = doc.GetElement(elemento.GroupId) as Group;
            string nomeGrupo = grupo?.GroupType?.Name ?? string.Empty;

            return nomeGrupo.StartsWith(PrefixoGruposPilares, StringComparison.OrdinalIgnoreCase) ||
                   nomeGrupo.StartsWith(PrefixoGruposVigas, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly string[] TokensPeso =
        {
            "weight",
            "peso"
        };

        private static string CriarAssinaturaEquivalencia(FamilyInstance instancia)
        {
            if (instancia?.Category == null)
                return "SEM_CATEGORIA";

            BuiltInCategory categoria = (BuiltInCategory)instancia.Category.Id.Value;
            return categoria switch
            {
                BuiltInCategory.OST_StructuralColumns => CriarAssinaturaPilar(instancia),
                BuiltInCategory.OST_StructuralFraming => CriarAssinaturaViga(instancia),
                _ => CriarAssinaturaGenerica(instancia)
            };
        }

        private static string CriarAssinaturaPilar(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(512);
            AppendCabecalhoComum(sb, instancia, "COLUMN");

            AppendParametro(sb, "SLANT_TYPE", instancia, BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM);
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static string CriarAssinaturaViga(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(1024);
            AppendCabecalhoComum(sb, instancia, "BEAM");

            AppendParametro(sb, "CUT_LENGTH", instancia, BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametro(sb, "Y_JUST", instancia, BuiltInParameter.Y_JUSTIFICATION);
            AppendParametro(sb, "Z_JUST", instancia, BuiltInParameter.Z_JUSTIFICATION);
            AppendParametro(sb, "YZ_JUST", instancia, BuiltInParameter.YZ_JUSTIFICATION);
            AppendParametro(sb, "BEAM_H_JUST", instancia, BuiltInParameter.BEAM_H_JUSTIFICATION);
            AppendParametro(sb, "BEAM_V_JUST", instancia, BuiltInParameter.BEAM_V_JUSTIFICATION);
            AppendParametro(sb, "SY_JUST", instancia, BuiltInParameter.START_Y_JUSTIFICATION);
            AppendParametro(sb, "EY_JUST", instancia, BuiltInParameter.END_Y_JUSTIFICATION);
            AppendParametro(sb, "SZ_JUST", instancia, BuiltInParameter.START_Z_JUSTIFICATION);
            AppendParametro(sb, "EZ_JUST", instancia, BuiltInParameter.END_Z_JUSTIFICATION);
            AppendParametro(sb, "Y_OFF", instancia, BuiltInParameter.Y_OFFSET_VALUE);
            AppendParametro(sb, "Z_OFF", instancia, BuiltInParameter.Z_OFFSET_VALUE);
            AppendParametro(sb, "SY_OFF", instancia, BuiltInParameter.START_Y_OFFSET_VALUE);
            AppendParametro(sb, "EY_OFF", instancia, BuiltInParameter.END_Y_OFFSET_VALUE);
            AppendParametro(sb, "SZ_OFF", instancia, BuiltInParameter.START_Z_OFFSET_VALUE);
            AppendParametro(sb, "EZ_OFF", instancia, BuiltInParameter.END_Z_OFFSET_VALUE);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static string CriarAssinaturaGenerica(FamilyInstance instancia)
        {
            StringBuilder sb = new StringBuilder(2048);
            AppendCabecalhoComum(sb, instancia, "GENERIC");
            AppendComprimentoDaInstancia(sb, instancia);
            AppendRotacaoSecao(sb, instancia);
            AppendVolume(sb, instancia);
            AppendMaterialEstrutural(sb, instancia);
            AppendParametrosPorNome(sb, instancia, "WEIGHT", TokensPeso);
            AppendCurveInvariante(sb, (instancia.Location as LocationCurve)?.Curve);

            return sb.ToString();
        }

        private static void AppendCabecalhoComum(StringBuilder sb, FamilyInstance instancia, string marcador)
        {
            sb.Append("KIND=").Append(marcador).Append('|');
            sb.Append("CAT=").Append(instancia.Category?.Id.Value ?? 0).Append('|');
            sb.Append("TYPE=").Append(instancia.GetTypeId().Value).Append('|');
            sb.Append("STRUCTTYPE=").Append((int)instancia.StructuralType).Append('|');
        }

        private static void AppendComprimentoDaInstancia(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "LEN", elemento, BuiltInParameter.INSTANCE_LENGTH_PARAM);
        }

        private static void AppendRotacaoSecao(StringBuilder sb, FamilyInstance instancia)
        {
            Parameter parametroRotacao = instancia.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE);
            if (parametroRotacao != null && parametroRotacao.HasValue && parametroRotacao.StorageType == StorageType.Double)
            {
                sb.Append("ROT=").Append(FormatoExato(parametroRotacao.AsDouble())).Append('|');
                return;
            }

            if (instancia.Location is LocationPoint locationPoint)
                sb.Append("ROT=").Append(FormatoExato(locationPoint.Rotation)).Append('|');
        }

        private static void AppendVolume(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "VOL", elemento, BuiltInParameter.HOST_VOLUME_COMPUTED);
        }

        private static void AppendMaterialEstrutural(StringBuilder sb, Element elemento)
        {
            AppendParametro(sb, "MAT", elemento, BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            AppendParametro(sb, "MAT_TYPE", elemento, BuiltInParameter.STRUCTURAL_MATERIAL_TYPE);
        }

        private static void AppendParametro(StringBuilder sb, string chave, Element elemento, BuiltInParameter builtInParameter)
        {
            Parameter parametro = elemento?.get_Parameter(builtInParameter);
            if (parametro == null || !parametro.HasValue)
                return;

            switch (parametro.StorageType)
            {
                case StorageType.Double:
                    sb.Append(chave).Append('=').Append(FormatoExato(parametro.AsDouble())).Append('|');
                    break;
                case StorageType.Integer:
                    sb.Append(chave).Append('=').Append(parametro.AsInteger()).Append('|');
                    break;
                case StorageType.ElementId:
                    sb.Append(chave).Append('=').Append(parametro.AsElementId().Value).Append('|');
                    break;
            }
        }

        private static void AppendParametrosPorNome(StringBuilder sb, Element elemento, string prefixo, IEnumerable<string> tokens)
        {
            List<string> encontrados = new List<string>();
            foreach (Parameter parametro in elemento.Parameters)
            {
                if (parametro == null || !parametro.HasValue || parametro.Definition == null)
                    continue;
                if (parametro.StorageType == StorageType.String || parametro.StorageType == StorageType.None)
                    continue;

                string nome = parametro.Definition.Name ?? string.Empty;
                string nomeNormalizado = nome.ToLowerInvariant();
                if (!tokens.Any(x => nomeNormalizado.Contains(x)))
                    continue;

                long id = parametro.Id?.Value ?? 0;
                switch (parametro.StorageType)
                {
                    case StorageType.Double:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{FormatoExato(parametro.AsDouble())}");
                        break;
                    case StorageType.Integer:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{parametro.AsInteger()}");
                        break;
                    case StorageType.ElementId:
                        encontrados.Add($"{prefixo}:{id}:{nome}:{parametro.AsElementId().Value}");
                        break;
                }
            }

            foreach (string item in encontrados.OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(item).Append('|');
        }

        private static void AppendCurveInvariante(StringBuilder sb, Curve curve)
        {
            if (curve == null)
            {
                sb.Append("CURVE=NULL|");
                return;
            }

            sb.Append("CTYPE=").Append(curve.GetType().Name).Append('|');
            sb.Append("CLEN=").Append(FormatoExato(curve.Length)).Append('|');

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            XYZ delta = p1 - p0;
            double dx = Math.Abs(delta.X);
            double dy = Math.Abs(delta.Y);
            double dz = Math.Abs(delta.Z);
            double horizontal = Math.Sqrt((dx * dx) + (dy * dy));

            sb.Append("DX=").Append(FormatoExato(dx)).Append('|');
            sb.Append("DY=").Append(FormatoExato(dy)).Append('|');
            sb.Append("DZ=").Append(FormatoExato(dz)).Append('|');
            sb.Append("DH=").Append(FormatoExato(horizontal)).Append('|');

            if (curve is Arc arc)
            {
                sb.Append("R=").Append(FormatoExato(arc.Radius)).Append('|');
                sb.Append("ADIRX=").Append(FormatoExato(Math.Abs(arc.Normal.X))).Append('|');
                sb.Append("ADIRY=").Append(FormatoExato(Math.Abs(arc.Normal.Y))).Append('|');
                sb.Append("ADIRZ=").Append(FormatoExato(Math.Abs(arc.Normal.Z))).Append('|');
            }
        }

        private static string FormatoExato(double valor)
        {
            return valor.ToString("R", CultureInfo.InvariantCulture);
        }

        private static OverrideGraphicSettings CriarOverride(Color cor)
        {
            OverrideGraphicSettings settings = new OverrideGraphicSettings();
            settings.SetProjectionLineColor(cor);
            settings.SetCutLineColor(cor);
            settings.SetSurfaceTransparency(30);
            settings.SetCutForegroundPatternColor(cor);
            settings.SetSurfaceForegroundPatternColor(cor);
            return settings;
        }

        private static int DesfazerGruposCriados(Document doc, string prefixoGrupo)
        {
            List<Group> grupos = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(x => x.GroupType != null &&
                            x.GroupType.Name != null &&
                            x.GroupType.Name.StartsWith(prefixoGrupo, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int total = 0;
            foreach (Group grupo in grupos)
            {
                try
                {
                    grupo.UngroupMembers();
                    total++;
                }
                catch
                {
                }
            }

            return total;
        }

        private static void RenomearGrupo(Document doc, Group group, string nomeBase)
        {
            if (group?.GroupType == null)
                return;

            HashSet<string> nomesExistentes = new FilteredElementCollector(doc)
                .OfClass(typeof(GroupType))
                .Cast<GroupType>()
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            nomesExistentes.Remove(group.GroupType.Name);

            string nome = nomeBase;
            int indice = 2;
            while (nomesExistentes.Contains(nome))
            {
                nome = $"{nomeBase}_{indice:00}";
                indice++;
            }

            group.GroupType.Name = nome;
        }

        private static string DescreverTipo(Document doc, ElementId typeId)
        {
            ElementType tipo = doc.GetElement(typeId) as ElementType;
            if (tipo == null)
                return "Tipo sem nome";

            string familia = string.Empty;
            if (tipo is FamilySymbol simbolo && simbolo.FamilyName != null)
                familia = simbolo.FamilyName.Trim();

            string nomeTipo = tipo.Name?.Trim() ?? "Sem nome";
            return string.IsNullOrWhiteSpace(familia) ? nomeTipo : $"{familia} - {nomeTipo}";
        }

        private static string DescreverElemento(FamilyInstance instancia, Document doc)
        {
            if (instancia == null)
                return "Elemento sem descricao";

            return DescreverTipo(doc, instancia.GetTypeId());
        }

        private static string SanitizarNome(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "Grupo";

            StringBuilder sb = new StringBuilder(texto.Length);
            foreach (char c in texto)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == ' ' || c == '-' || c == '_')
                {
                    sb.Append('_');
                }
            }

            string resultado = sb.ToString().Trim('_');
            while (resultado.Contains("__"))
                resultado = resultado.Replace("__", "_");

            return string.IsNullOrWhiteSpace(resultado) ? "Grupo" : resultado;
        }
    }
}
