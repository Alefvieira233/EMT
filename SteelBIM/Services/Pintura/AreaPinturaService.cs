#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services.Pintura
{
    /// <summary>Resultado do calculo de area de pintura.</summary>
    public sealed record PinturaResultado(int Processadas, int Falhas, double TotalM2, bool TabelaCriada, string MotivoTabela);

    /// <summary>
    /// v2.8.26: calcula a area de pintura (perimetro da secao x comprimento) dos perfis metalicos
    /// a partir da GEOMETRIA — somando as faces do solido e descontando as faces de topo (corte das
    /// barras) e um percentual (parafusos/folgas). Grava num parametro de projeto EMT_Area_Pintura
    /// (tipo Area, instancia) e, opcionalmente, cria/atualiza uma tabela (ViewSchedule). Funciona
    /// mesmo sem material aplicado — resolve a causa raiz do "Material:Area = 0".
    /// </summary>
    public sealed class AreaPinturaService
    {
        public const string ParamArea = "EMT_Area_Pintura";

        public PinturaResultado Executar(UIDocument uidoc, IReadOnlyList<Element> alvos, AreaPinturaConfig cfg)
        {
            Document doc = uidoc.Document;

            // 1) garantir o parametro de projeto (transacao propria, como o padrao de Montagem).
            using (Transaction t = new Transaction(doc, "Criar parametro Area de Pintura"))
            {
                t.Start();
                GarantirParametro(doc);
                t.Commit();
            }

            // 2) calcular e gravar a area em cada peca.
            int processadas = 0;
            int falhas = 0;
            double totalFt2 = 0.0;
            using (Transaction t = new Transaction(doc, "Calcular Area de Pintura"))
            {
                t.Start();
                foreach (Element e in alvos)
                {
                    try
                    {
                        double areaFt2 = CalcularAreaPinturaFt2(e, cfg);
                        if (EscreverArea(e, areaFt2))
                        {
                            processadas++;
                            totalFt2 += areaFt2;
                        }
                        else
                        {
                            falhas++;
                        }
                    }
                    catch (Exception ex)
                    {
                        falhas++;
                        Logger.Warn(ex, "[AreaPintura] falha ao calcular peca {Id}", e.Id.Value);
                    }
                }
                t.Commit();
            }

            double totalM2 = UnitUtils.ConvertFromInternalUnits(totalFt2, UnitTypeId.SquareMeters);

            // 3) tabela (best-effort) — nao impede o calculo se falhar.
            bool tabelaCriada = false;
            string motivoTabela = string.Empty;
            if (cfg.CriarTabela)
            {
                try
                {
                    using (Transaction t = new Transaction(doc, "Criar tabela de Area de Pintura"))
                    {
                        t.Start();
                        tabelaCriada = CriarOuAtualizarTabelas(doc, cfg);
                        t.Commit();
                    }
                }
                catch (Exception ex)
                {
                    motivoTabela = ex.Message;
                    Logger.Warn(ex, "[AreaPintura] falha ao criar a tabela");
                }
            }

            Logger.Info("[AreaPintura] processadas={P} falhas={F} total={M2:0.00} m2 tabela={T}",
                processadas, falhas, totalM2, tabelaCriada);
            return new PinturaResultado(processadas, falhas, totalM2, tabelaCriada, motivoTabela);
        }

        // ===== Calculo da area por geometria =====
        private static double CalcularAreaPinturaFt2(Element e, AreaPinturaConfig cfg)
        {
            List<Solid> solids = e.GetAllSolidsFine(true, out _);
            XYZ? eixo = EixoDoElemento(e);

            double total = 0.0;
            double topos = 0.0;
            foreach (Solid s in solids)
            {
                foreach (Face f in s.Faces)
                {
                    double area = f.Area; // ft^2 (unidade interna)
                    total += area;
                    if (eixo != null && f is PlanarFace pf)
                    {
                        double dot = Math.Abs(pf.FaceNormal.Normalize().DotProduct(eixo));
                        if (PinturaFormula.EhFaceDeTopo(dot))
                            topos += area;
                    }
                }
            }

            bool descontaTopos = cfg.DescontarTopos && eixo != null;
            return PinturaFormula.AreaLiquida(total, topos, descontaTopos, cfg.PercentualDesconto);
        }

        /// <summary>Eixo (direcao) do membro, em coords do projeto: barra reta -> direcao da curva;
        /// pilar vertical (LocationPoint) -> Z. Null quando indeterminado (curvo) -> nao desconta topos.</summary>
        private static XYZ? EixoDoElemento(Element e)
        {
            if (e.Location is LocationCurve lc && lc.Curve is Line ln)
                return ln.Direction.Normalize();
            if (e.Location is LocationPoint)
                return XYZ.BasisZ;
            return null;
        }

        private static bool EscreverArea(Element e, double areaFt2)
        {
            Parameter? p = e.LookupParameter(ParamArea);
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                return false;
            return p.Set(areaFt2); // parametro Area e' armazenado em ft^2 (interno)
        }

        // ===== Parametro de projeto (padrao copiado de PlanoMontagemService) =====
        private static bool GarantirParametro(Document doc)
        {
            BindingMap bindings = doc.ParameterBindings;
            DefinitionBindingMapIterator it = bindings.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key is Definition def && string.Equals(def.Name, ParamArea, StringComparison.OrdinalIgnoreCase))
                    return true; // ja existe
            }

            string sharedFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"SteelBIM_SharedParams_{Guid.NewGuid():N}.txt");
            System.IO.File.WriteAllText(sharedFile,
                "# This is a Revit shared parameter file.\n*META\tVERSION\tMINVERSION\n*GROUP\tID\tNAME\n*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n");

            string previousSharedFile = doc.Application.SharedParametersFilename;
            try
            {
                doc.Application.SharedParametersFilename = sharedFile;
                DefinitionFile defFile = doc.Application.OpenSharedParameterFile();

                DefinitionGroup group = defFile.Groups.Create("SteelBIM");
                ExternalDefinitionCreationOptions opts = new ExternalDefinitionCreationOptions(ParamArea, SpecTypeId.Area)
                {
                    UserModifiable = true,
                    Description = "Area de pintura do perfil metalico (perimetro x comprimento)"
                };
                ExternalDefinition externalDef = (ExternalDefinition)group.Definitions.Create(opts);

                CategorySet categories = doc.Application.Create.NewCategorySet();
                foreach (BuiltInCategory bic in new[] { BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralColumns })
                {
                    Category cat = Category.GetCategory(doc, bic);
                    if (cat != null)
                        categories.Insert(cat);
                }

                InstanceBinding binding = doc.Application.Create.NewInstanceBinding(categories);
                doc.ParameterBindings.Insert(externalDef, binding, GroupTypeId.Construction);
                Logger.Info("[AreaPintura] parametro {Nome} criado", ParamArea);
                return true;
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousSharedFile))
                    doc.Application.SharedParametersFilename = previousSharedFile;
                try
                {
                    System.IO.File.Delete(sharedFile);
                }
                catch (Exception exDel)
                {
                    Logger.Debug("[AreaPintura] falha ao apagar SharedParams temp '{F}': {Msg}", sharedFile, exDel.Message);
                }
            }
        }

        // ===== Tabela (ViewSchedule) — best-effort =====
        private static bool CriarOuAtualizarTabelas(Document doc, AreaPinturaConfig cfg)
        {
            bool any = false;
            if (cfg.IncluirVigas)
                any |= CriarTabelaCategoria(doc, BuiltInCategory.OST_StructuralFraming, "EMT - Area de Pintura (Vigas)");
            if (cfg.IncluirPilares)
                any |= CriarTabelaCategoria(doc, BuiltInCategory.OST_StructuralColumns, "EMT - Area de Pintura (Pilares)");
            return any;
        }

        private static bool CriarTabelaCategoria(Document doc, BuiltInCategory bic, string nome)
        {
            // refresh: remove tabela homonima antiga.
            ViewSchedule? antiga = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(v => !v.IsTemplate && string.Equals(v.Name, nome, StringComparison.OrdinalIgnoreCase));
            if (antiga != null)
                doc.Delete(antiga.Id);

            ViewSchedule vs = ViewSchedule.CreateSchedule(doc, new ElementId(bic));
            vs.Name = nome;
            ScheduleDefinition def = vs.Definition;

            AddCampoPorParam(def, BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
            AddCampoCount(def);
            AddCampoPorParam(def, BuiltInParameter.INSTANCE_LENGTH_PARAM);
            AddCampoPorNome(doc, def, ParamArea);
            return true;
        }

        private static void AddCampoPorParam(ScheduleDefinition def, BuiltInParameter bip)
        {
            ElementId pid = new ElementId(bip);
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (sf.ParameterId == pid)
                {
                    def.AddField(sf);
                    return;
                }
            }
        }

        private static void AddCampoCount(ScheduleDefinition def)
        {
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (sf.FieldType == ScheduleFieldType.Count)
                {
                    def.AddField(sf);
                    return;
                }
            }
        }

        private static void AddCampoPorNome(Document doc, ScheduleDefinition def, string nome)
        {
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (string.Equals(sf.GetName(doc), nome, StringComparison.OrdinalIgnoreCase))
                {
                    def.AddField(sf);
                    return;
                }
            }
        }
    }
}
