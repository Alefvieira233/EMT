using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.CncExport;

namespace FerramentaEMT.Services.CncExport
{
    /// <summary>
    /// Extrai furos (holes) de um FamilyInstance estrutural para gerar
    /// blocos BO no arquivo NC1.
    /// </summary>
    /// <remarks>
    /// IMPORTANTE: extracao de furos a partir de geometria nativa do Revit
    /// e altamente dependente da modelagem (familia parametrica, void, hole element).
    /// Esta implementacao trata o caso comum onde os furos sao modelados como
    /// parametros tipo "Hole 1 Diameter", "Hole 1 X", "Hole 1 Y", "Hole 1 Face"
    /// (convencao adotada por familias EMT padronizadas).
    ///
    /// Familias sem essa convencao retornam lista vazia e o NC1 sai sem bloco BO
    /// — o que e valido (o operador adiciona furos manualmente na maquina).
    /// </remarks>
    public static class DstvHoleExtractor
    {
        // Tentativa de ate N furos parametricos por elemento
        private const int MaxHoles = 60;

        public static List<DstvHole> Extract(Document doc, FamilyInstance element)
        {
            var holes = new List<DstvHole>();
            if (element == null) return holes;

            for (int i = 1; i <= MaxHoles; i++)
            {
                DstvHole h = TryReadParametricHole(element, i);
                if (h == null) continue;
                holes.Add(h);
            }

            return holes;
        }

        private static DstvHole TryReadParametricHole(FamilyInstance element, int index)
        {
            // Convencao: parametros "Hole {i} Diameter", "Hole {i} X", "Hole {i} Y", "Hole {i} Face"
            // Aceita variantes em portugues: "Furo {i} Diametro", "Furo {i} X", etc.

            double? diam = ReadLengthMm(element,
                $"Hole {index} Diameter",
                $"Furo {index} Diametro",
                $"Furo {index} Diâmetro");

            if (!diam.HasValue || diam.Value <= 0) return null;

            double? x = ReadLengthMm(element, $"Hole {index} X", $"Furo {index} X");
            double? y = ReadLengthMm(element, $"Hole {index} Y", $"Furo {index} Y");
            string face = ReadString(element, $"Hole {index} Face", $"Furo {index} Face");

            return new DstvHole
            {
                XMm = x ?? 0,
                YMm = y ?? 0,
                DiameterMm = diam.Value,
                Face = ParseFace(face)
            };
        }

        private static double? ReadLengthMm(Element elem, params string[] candidateNames)
        {
            foreach (string name in candidateNames)
            {
                try
                {
                    Parameter p = elem.LookupParameter(name);
                    if (p == null || !p.HasValue) continue;
                    if (p.StorageType == StorageType.Double)
                        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro de furo"); }
            }
            return null;
        }

        private static string ReadString(Element elem, params string[] candidateNames)
        {
            foreach (string name in candidateNames)
            {
                try
                {
                    Parameter p = elem.LookupParameter(name);
                    if (p == null || !p.HasValue) continue;
                    if (p.StorageType == StorageType.String) return p.AsString() ?? "";
                    if (p.StorageType == StorageType.Integer) return p.AsInteger().ToString();
                }
                catch (Exception ex) { Logger.Warn(ex, "Falha ao ler parametro de furo"); }
            }
            return "";
        }

        private static DstvFace ParseFace(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DstvFace.WebFront;
            string s = raw.Trim().ToLowerInvariant();

            return s switch
            {
                "v" or "front" or "frente" or "alma" => DstvFace.WebFront,
                "h" or "back" or "traseira" or "alma_tras" => DstvFace.WebBack,
                "o" or "top" or "topo" or "mesa_superior" or "upper" => DstvFace.TopFlange,
                "u" or "bottom" or "fundo" or "mesa_inferior" or "lower" => DstvFace.BottomFlange,
                "s" or "side" or "lado" => DstvFace.Side,
                _ => DstvFace.WebFront
            };
        }
    }
}
