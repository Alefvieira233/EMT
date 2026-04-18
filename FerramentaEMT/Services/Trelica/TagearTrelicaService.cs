#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Servico para tagging leve de treliças — so identifica perfis na elevacao,
    /// sem cotas. Util para projetistas que ja tem cotas mas falta identificar perfis.
    /// Reaproveita TrelicaClassificador e TrelicaPerfilFormatter.
    /// </summary>
    public class TagearTrelicaService
    {
        /// <summary>
        /// Resultado da operacao de tagging de treliça.
        /// </summary>
        public sealed class TagearTrelicaReport
        {
            public int TotalBarrasProcessadas { get; set; }
            public int TotalTagsCriadas { get; set; }
            public int TotalRotulosBanzos { get; set; }
            public int BarrasComErro { get; set; }
            public List<string> Erros { get; set; } = new();

            /// <summary>Resumo formatado para exibicao ao usuario.</summary>
            public string ObterResumo()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Total de barras processadas: {TotalBarrasProcessadas}");
                sb.AppendLine($"Tags de perfil criadas: {TotalTagsCriadas}");

                if (TotalRotulosBanzos > 0)
                    sb.AppendLine($"Rótulos de banzo criados: {TotalRotulosBanzos}");

                if (BarrasComErro > 0)
                    sb.AppendLine($"Barras com erro: {BarrasComErro}");

                return sb.ToString();
            }
        }

        private const string Titulo = "Tagear Treliça";

        /// <summary>
        /// Executa o tagging de treliça para elementos pre-selecionados em uma vista.
        /// </summary>
        public TagearTrelicaReport Executar(UIDocument uidoc, TagearTrelicaConfig config)
        {
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var relatorio = new TagearTrelicaReport();
            var doc = uidoc.Document;
            var view = doc.ActiveView;

            // Validacoes basicas
            if (view == null)
            {
                Logger.Warn("[{Title}] nenhuma vista ativa", Titulo);
                relatorio.Erros.Add("Nenhuma vista ativa no documento.");
                return relatorio;
            }

            // Coleta elementos pre-selecionados (barras de Structural Framing)
            var elementosSelecionados = uidoc.Selection.GetElementIds();
            if (elementosSelecionados.Count == 0)
            {
                Logger.Warn("[{Title}] nenhum elemento selecionado", Titulo);
                relatorio.Erros.Add("Selecione ao menos uma barra de treliça para tagear.");
                return relatorio;
            }

            // Filtra apenas barras de StructuralFraming
            var barras = new List<FamilyInstance>();
            foreach (var elemId in elementosSelecionados)
            {
                Element elem = doc.GetElement(elemId);
                if (elem == null) continue;

                if (!(elem is FamilyInstance fi)) continue;

                // Verificar se e' barra estrutural
                if (fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Beam ||
                    fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Brace)
                {
                    barras.Add(fi);
                }
            }

            if (barras.Count == 0)
            {
                Logger.Warn("[{Title}] nenhuma barra estrutural selecionada", Titulo);
                relatorio.Erros.Add("Nenhuma barra estrutural (vigas ou contraventos) selecionada.");
                return relatorio;
            }

            relatorio.TotalBarrasProcessadas = barras.Count;

            // Classificar barras (TrelicaClassificador e' static — chamar metodos direto)
            double zMedioBBox = barras.Average(b =>
            {
                if (b.Location is LocationCurve lc)
                {
                    var p0 = lc.Curve.GetEndPoint(0);
                    var p1 = lc.Curve.GetEndPoint(1);
                    return (p0.Z + p1.Z) / 2.0;
                }
                return 0.0;
            });

            var barrasClassificadas = new Dictionary<FamilyInstance, TrelicaClassificador.TipoMembro>();

            foreach (var barra in barras)
            {
                try
                {
                    TrelicaClassificador.TipoMembro tipo = TrelicaClassificador.TipoMembro.Indefinido;
                    if (barra.Location is LocationCurve locCurve && locCurve.Curve is Curve curve)
                    {
                        XYZ p0 = curve.GetEndPoint(0);
                        XYZ p1 = curve.GetEndPoint(1);
                        XYZ dir = (p1 - p0).Normalize();
                        double inclinacao = Math.Asin(Math.Abs(dir.Z));
                        tipo = TrelicaClassificador.ClassificarPorInclinacao(inclinacao);

                        if (tipo == TrelicaClassificador.TipoMembro.BanzoIndefinido)
                        {
                            double zMedioBarra = (p0.Z + p1.Z) / 2.0;
                            tipo = TrelicaClassificador.ClassificarBanzoPorAltura(zMedioBarra, zMedioBBox);
                        }
                    }
                    barrasClassificadas[barra] = tipo;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "[{Title}] erro classificando barra {BarraId}, assumindo indefinido", Titulo, barra.Id);
                    barrasClassificadas[barra] = TrelicaClassificador.TipoMembro.BanzoIndefinido;
                }
            }

            // Transacao para criar tags
            using (Transaction t = new Transaction(doc, "EMT - Tagear Treliça"))
            {
                try
                {
                    t.Start();

                    // Agrupar banzos para criar rotulos (opcional)
                    HashSet<string> perfisBanzoSuperior = new();
                    HashSet<string> perfisBanzoInferior = new();

                    foreach (var kvp in barrasClassificadas)
                    {
                        FamilyInstance barra = kvp.Key;
                        TrelicaClassificador.TipoMembro tipo = kvp.Value;

                        try
                        {
                            // Verificar se deve procescar este tipo
                            bool deveProcescar = false;
                            if (config.TagearBanzoSuperior && tipo == TrelicaClassificador.TipoMembro.BanzoSuperior) deveProcescar = true;
                            if (config.TagearBanzoInferior && tipo == TrelicaClassificador.TipoMembro.BanzoInferior) deveProcescar = true;
                            if (config.TagearMontantes && tipo == TrelicaClassificador.TipoMembro.Montante) deveProcescar = true;
                            if (config.TagearDiagonais && tipo == TrelicaClassificador.TipoMembro.Diagonal) deveProcescar = true;

                            if (!deveProcescar)
                                continue;

                            // Calcular posicao
                            XYZ posicaoTag = CalcularPosicaoTag(barra, view, config.OffsetTagMm);

                            // Obter nome do perfil
                            string nomePerfil = ObterNomePerfil(barra);

                            // Criar tag
                            var tag = IndependentTag.Create(
                                doc,
                                view.Id,
                                new Reference(barra),
                                addLeader: false,
                                TagMode.TM_ADDBY_CATEGORY,
                                TagOrientation.Horizontal,
                                posicaoTag);

                            if (tag != null)
                                relatorio.TotalTagsCriadas++;

                            // Coletar nomes para rotulos
                            if (config.CriarRotuloBanzos)
                            {
                                if (tipo == TrelicaClassificador.TipoMembro.BanzoSuperior)
                                    perfisBanzoSuperior.Add(nomePerfil);
                                else if (tipo == TrelicaClassificador.TipoMembro.BanzoInferior)
                                    perfisBanzoInferior.Add(nomePerfil);
                            }
                        }
                        catch (Exception ex)
                        {
                            relatorio.BarrasComErro++;
                            relatorio.Erros.Add($"Barra {barra.Id}: {ex.Message}");
                            Logger.Error(ex, "[{Title}] falha tagging barra {BarraId}", Titulo, barra.Id);
                        }
                    }

                    // Criar rotulos de banzo (TextNote)
                    if (config.CriarRotuloBanzos && (perfisBanzoSuperior.Count > 0 || perfisBanzoInferior.Count > 0))
                    {
                        try
                        {
                            ElementId tipoTextoId = new FilteredElementCollector(doc)
                                .OfClass(typeof(TextNoteType))
                                .FirstElementId();

                            if (tipoTextoId == ElementId.InvalidElementId)
                            {
                                Logger.Warn("[{Title}] nenhum tipo de TextNote encontrado no projeto", Titulo);
                            }
                            else
                            {
                                TextNoteOptions opts = new TextNoteOptions
                                {
                                    TypeId = tipoTextoId,
                                    HorizontalAlignment = HorizontalTextAlignment.Left
                                };

                                // Encontrar a barra mais a direita (na vista) de cada banzo
                                // para ancorar o rotulo na altura correta do banzo
                                XYZ posRefSup = XYZ.Zero, posRefInf = XYZ.Zero;
                                bool temSup = false, temInf = false;
                                XYZ rightDir = view.RightDirection.Normalize();

                                foreach (var kvp in barrasClassificadas)
                                {
                                    if (!(kvp.Key.Location is LocationCurve lc)) continue;
                                    XYZ mid = (lc.Curve.GetEndPoint(0) + lc.Curve.GetEndPoint(1)) / 2.0;

                                    if (kvp.Value == TrelicaClassificador.TipoMembro.BanzoSuperior && perfisBanzoSuperior.Count > 0)
                                    {
                                        if (!temSup || mid.DotProduct(rightDir) > posRefSup.DotProduct(rightDir))
                                        {
                                            posRefSup = mid;
                                            temSup = true;
                                        }
                                    }
                                    else if (kvp.Value == TrelicaClassificador.TipoMembro.BanzoInferior && perfisBanzoInferior.Count > 0)
                                    {
                                        if (!temInf || mid.DotProduct(rightDir) > posRefInf.DotProduct(rightDir))
                                        {
                                            posRefInf = mid;
                                            temInf = true;
                                        }
                                    }
                                }

                                double offsetLat = UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters);

                                if (temSup)
                                {
                                    string texto = $"BANZO SUPERIOR: {string.Join(", ", perfisBanzoSuperior)}";
                                    TextNote.Create(doc, view.Id, posRefSup + rightDir * offsetLat, texto, opts);
                                    relatorio.TotalRotulosBanzos++;
                                }

                                if (temInf)
                                {
                                    string texto = $"BANZO INFERIOR: {string.Join(", ", perfisBanzoInferior)}";
                                    TextNote.Create(doc, view.Id, posRefInf + rightDir * offsetLat, texto, opts);
                                    relatorio.TotalRotulosBanzos++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[{Title}] erro criando rotulos", Titulo);
                        }
                    }

                    t.Commit();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[{Title}] falha na transacao", Titulo);
                    relatorio.Erros.Add($"Falha na transação: {ex.Message}");
                    return relatorio;
                }
            }

            return relatorio;
        }

        /// <summary>
        /// Calcula a posicao da tag: midpoint do elemento + offset perpendicular a vista.
        /// </summary>
        private XYZ CalcularPosicaoTag(FamilyInstance fi, View view, double offsetMm)
        {
            // Converter offset de mm para pes (unidades internas Revit)
            double offsetPes = UnitUtils.ConvertToInternalUnits(offsetMm, UnitTypeId.Millimeters);

            // Tentar obter posicao a partir da Location
            var location = fi.Location as LocationCurve;
            if (location != null && location.Curve is Line line)
            {
                XYZ midpoint = (line.GetEndPoint(0) + line.GetEndPoint(1)) / 2.0;

                // Offset perpendicular — usar direcao "para cima" na vista
                XYZ offsetDir = view.UpDirection.Normalize();
                return midpoint + offsetDir * offsetPes;
            }

            // Fallback: BoundingBox
            BoundingBoxXYZ bb = fi.get_BoundingBox(view);
            if (bb != null)
            {
                XYZ center = (bb.Min + bb.Max) / 2.0;
                XYZ offsetDir = view.UpDirection.Normalize();
                return center + offsetDir * offsetPes;
            }

            // Fallback absoluto: origem
            return XYZ.Zero;
        }

        /// <summary>
        /// Obtem o nome do perfil (tipo) a partir de parametros do elemento.
        /// </summary>
        private string ObterNomePerfil(FamilyInstance fi)
        {
            try
            {
                // 1) Tentar parametro shared EMT_Perfil (se existir no projeto)
                var paramShared = fi.LookupParameter("EMT_Perfil");
                if (paramShared != null && paramShared.StorageType == StorageType.String)
                {
                    string val = paramShared.AsString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }

                // 2) Fallback: nome do tipo (FamilySymbol.Name)
                string typeName = fi.Symbol?.Name ?? "";
                if (!string.IsNullOrWhiteSpace(typeName))
                    return typeName;

                // 3) Fallback: nome da familia (via Symbol.Family, FamilyInstance nao expoe .Family diretamente)
                string familyName = fi.Symbol?.Family?.Name ?? "";
                if (!string.IsNullOrWhiteSpace(familyName))
                    return familyName;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[{Title}] erro obtendo nome do perfil para {BarraId}", Titulo, fi.Id);
            }

            return "-";
        }
    }
}
