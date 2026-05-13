#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Services.Trelica;

namespace FerramentaEMT.Services.IdentificacaoPerfil
{
    /// <summary>
    /// Servico para identificacao em massa de perfis metalicos em qualquer vista
    /// (planta, elevacao, 3D). Usuario seleciona elementos, clica botao, plugin coloca
    /// tags de perfil automaticamente. Reaproveita TrelicaPerfilFormatter para formatacao.
    /// </summary>
    public class IdentificarPerfilService
    {
        /// <summary>
        /// Resultado da operacao de identificacao em massa.
        /// </summary>
        public sealed class IdentificarPerfilReport
        {
            public int TotalElementosSelecionados { get; set; }
            public int TotalTagsCriadas { get; set; }
            public int ElementosPuladosTagExistente { get; set; }
            public int ElementosComErro { get; set; }
            public List<string> Erros { get; set; } = new();

            /// <summary>Resumo formatado para exibicao ao usuario.</summary>
            public string ObterResumo()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Total de elementos processados: {TotalElementosSelecionados}");
                sb.AppendLine($"Tags criadas com sucesso: {TotalTagsCriadas}");

                if (ElementosPuladosTagExistente > 0)
                    sb.AppendLine($"Elementos pulados (ja tinham tag): {ElementosPuladosTagExistente}");

                if (ElementosComErro > 0)
                    sb.AppendLine($"Elementos com erro: {ElementosComErro}");

                return sb.ToString();
            }
        }

        private const string Titulo = "Identificar Perfil em Massa";

        /// <summary>
        /// Executa a identificacao de perfis em massa para elementos pre-selecionados.
        /// </summary>
        public IdentificarPerfilReport Executar(UIDocument uidoc, IdentificarPerfilConfig config)
        {
            if (uidoc == null) throw new ArgumentNullException(nameof(uidoc));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var relatorio = new IdentificarPerfilReport();
            var doc = uidoc.Document;
            var view = doc.ActiveView;

            // Validacoes basicas
            if (view == null)
            {
                Logger.Warn("[{Title}] nenhuma vista ativa", Titulo);
                relatorio.Erros.Add("Nenhuma vista ativa no documento.");
                return relatorio;
            }

            // Coleta elementos pre-selecionados
            var elementosSelecionados = uidoc.Selection.GetElementIds();
            if (elementosSelecionados.Count == 0)
            {
                Logger.Warn("[{Title}] nenhum elemento selecionado", Titulo);
                relatorio.Erros.Add("Selecione ao menos um elemento para identificar.");
                return relatorio;
            }

            relatorio.TotalElementosSelecionados = elementosSelecionados.Count;

            // Filtra elementos por categoria
            var elementosFiltrados = new List<FamilyInstance>();
            foreach (var elemId in elementosSelecionados)
            {
                Element elem = doc.GetElement(elemId);
                if (elem == null) continue;

                if (!(elem is FamilyInstance fi)) continue;

                bool deveProcescar = false;

                // Verificar categoria
                if (config.IncluirVigas && fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Beam)
                    deveProcescar = true;
                if (config.IncluirPilares && fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Column)
                    deveProcescar = true;
                if (config.IncluirContraventos && fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Brace)
                    deveProcescar = true;

                if (deveProcescar)
                    elementosFiltrados.Add(fi);
            }

            if (elementosFiltrados.Count == 0)
            {
                Logger.Warn("[{Title}] nenhum elemento com categoria selecionada apos filtragem", Titulo);
                relatorio.Erros.Add("Nenhum elemento com categoria selecionada (vigas, pilares, contraventos).");
                return relatorio;
            }

            // Transacao para criar tags
            using (Transaction t = new Transaction(doc, "EMT - Identificar Perfil"))
            {
                try
                {
                    t.Start();

                    foreach (var fi in elementosFiltrados)
                    {
                        try
                        {
                            // Verificar se ja tem tag
                            if (!config.SubstituirTagsExistentes && JaTemTagNaVista(doc, view, fi.Id))
                            {
                                relatorio.ElementosPuladosTagExistente++;
                                continue;
                            }

                            // Calcular posicao da tag (midpoint + offset perpendicular)
                            XYZ posicaoTag = CalcularPosicaoTag(fi, view, config.OffsetTagMm);

                            // Obter nome do perfil
                            string nomePerfil = ObterNomePerfil(fi);

                            
                            // Criar tag
                            var tag = IndependentTag.Create(
                                doc,
                                view.Id,
                                new Reference(fi),
                                addLeader: false,
                                TagMode.TM_ADDBY_CATEGORY,
                                TagOrientation.Horizontal,
                                posicaoTag);

                            if (tag != null)
                                relatorio.TotalTagsCriadas++;
                        }
                        catch (Exception ex)
                        {
                            relatorio.ElementosComErro++;
                            relatorio.Erros.Add($"Elemento {fi.Id}: {ex.Message}");
                            Logger.Error(ex, "[{Title}] falha identificando elemento {ElemId}", Titulo, fi.Id);
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
        /// Verifica se um elemento ja tem tag independente na vista atual.
        /// </summary>
        private bool JaTemTagNaVista(Document doc, View view, ElementId elementId)
        {
            try
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>();

                foreach (var tag in tags)
                {
                    var taggedIds = tag.GetTaggedLocalElementIds();
                    if (taggedIds.Contains(elementId))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[{Title}] erro verificando tag existente", Titulo);
            }

            return false;
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
                Logger.Error(ex, "[{Title}] erro obtendo nome do perfil para {ElemId}", Titulo, fi.Id);
            }

            return "-";
        }
    }
}
