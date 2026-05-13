using System;
using System.Globalization;
using System.IO;
using System.Text;
using FerramentaEMT.Models.CncExport;

namespace FerramentaEMT.Services.CncExport
{
    /// <summary>
    /// Serializa um <see cref="DstvFile"/> em texto NC1 (formato DSTV ASCII).
    /// </summary>
    /// <remarks>
    /// Implementacao pura — nao depende da API do Revit, totalmente testavel
    /// por unit tests.
    ///
    /// Estrutura do arquivo NC1 (ordem dos blocos):
    ///   ST     -> cabecalho (always present)
    ///   EN     -> end of header
    ///   AK     -> outer contour (omitido nesta implementacao base)
    ///   IK     -> inner contour (omitido)
    ///   SC     -> cuts at ends (gerado se HasMiteredEnds())
    ///   BO     -> hole block, um por face (so se houver furos)
    ///   SI     -> additional information (opcional)
    ///   EN     -> end of file
    ///
    /// Encoding: ASCII (cuidado com acentos no notes / piece mark).
    /// Line endings: CRLF (padrao da industria CNC).
    /// </remarks>
    public static class DstvFileWriter
    {
        // CRLF — padrao DSTV adotado pela maioria das maquinas CNC alemas
        private const string NewLine = "\r\n";

        // Cultura invariante para que o decimal sempre seja "." e nunca ","
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Serializa o arquivo NC1 em uma string.
        /// </summary>
        public static string Write(DstvFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            var sb = new StringBuilder();

            WriteHeader(sb, file);
            WriteCuts(sb, file);
            WriteHoles(sb, file);
            WriteNotes(sb, file);
            sb.Append("EN").Append(NewLine);

            return sb.ToString();
        }

        /// <summary>
        /// Salva o NC1 em arquivo. Encoding ASCII puro (caracteres nao-ASCII viram '?').
        /// </summary>
        public static void Save(DstvFile file, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath obrigatorio", nameof(filePath));

            string content = Write(file);

            // ASCII — maquinas CNC antigas nao toleram BOM nem UTF-8
            // Se houver caracteres acentuados eles viram '?'.
            File.WriteAllText(filePath, content, Encoding.ASCII);
        }

        // ============================================================
        //  Bloco ST (cabecalho)
        // ============================================================

        private static void WriteHeader(StringBuilder sb, DstvFile f)
        {
            sb.Append("ST").Append(NewLine);

            // Os campos sao posicionais, um por linha, prefixados por dois espacos.
            AppendField(sb, f.OrderNumber);             // 1.  Order
            AppendField(sb, f.DrawingNumber);           // 2.  Drawing No.
            AppendField(sb, f.Phase);                   // 3.  Phase
            AppendField(sb, f.PieceMark);               // 4.  Piece No (mark)
            AppendField(sb, f.SteelQuality);            // 5.  Steel quality
            AppendField(sb, f.Quantity.ToString(Inv));  // 6.  Quantity
            AppendField(sb, f.ProfileName);             // 7.  Profile name
            AppendField(sb, f.ProfileType.ToDstvCode());// 8.  Profile code
            AppendField(sb, FormatNumber(f.ProfileHeightMm));    // 9.  height
            AppendField(sb, FormatNumber(f.FlangeWidthMm));      // 10. flange width
            AppendField(sb, FormatNumber(f.FlangeThicknessMm));  // 11. flange thickness
            AppendField(sb, FormatNumber(f.WebThicknessMm));     // 12. web thickness
            AppendField(sb, FormatNumber(f.FilletRadiusMm));     // 13. radius
            AppendField(sb, FormatNumber(f.WeightPerMeter));     // 14. weight per meter
            AppendField(sb, f.SurfaceTreatment);                 // 15. surface treatment
            AppendField(sb, FormatNumber(f.CutLengthMm));        // 16. length

            sb.Append("EN").Append(NewLine);
        }

        // ============================================================
        //  Bloco SC (cortes em extremidade)
        // ============================================================

        private static void WriteCuts(StringBuilder sb, DstvFile f)
        {
            if (!f.HasMiteredEnds()) return;

            sb.Append("SC").Append(NewLine);
            // Formato simplificado: angulo de inicio e angulo de fim em graus
            sb.Append("  ")
              .Append(FormatNumber(f.CutAngleStartDeg))
              .Append(" ")
              .Append(FormatNumber(f.CutAngleEndDeg))
              .Append(NewLine);
            sb.Append("EN").Append(NewLine);
        }

        // ============================================================
        //  Bloco BO (furos) — um bloco por face com furos
        // ============================================================

        private static void WriteHoles(StringBuilder sb, DstvFile f)
        {
            if (f.Holes == null || f.Holes.Count == 0) return;

            // Agrupar por face e ordenar para reprodutibilidade
            var byFace = new System.Collections.Generic.Dictionary<DstvFace, System.Collections.Generic.List<DstvHole>>();
            foreach (DstvHole h in f.Holes)
            {
                if (!byFace.ContainsKey(h.Face))
                    byFace[h.Face] = new System.Collections.Generic.List<DstvHole>();
                byFace[h.Face].Add(h);
            }

            // Ordem fixa de faces para arquivo determinístico (facilita diff em git)
            DstvFace[] orderedFaces = { DstvFace.WebFront, DstvFace.WebBack, DstvFace.TopFlange, DstvFace.BottomFlange, DstvFace.Side };

            foreach (DstvFace face in orderedFaces)
            {
                if (!byFace.TryGetValue(face, out var holes) || holes.Count == 0)
                    continue;

                holes.Sort(CompareHoles);

                sb.Append("BO").Append(NewLine);
                foreach (DstvHole h in holes)
                {
                    // Formato:  <face> <x> <y> <diametro> [<profundidade>]
                    sb.Append(' ')
                      .Append(face.ToDstvCode())
                      .Append(' ').Append(FormatNumber(h.XMm))
                      .Append(' ').Append(FormatNumber(h.YMm))
                      .Append(' ').Append(FormatNumber(h.DiameterMm));

                    if (h.DepthMm > 0)
                        sb.Append(' ').Append(FormatNumber(h.DepthMm));

                    sb.Append(NewLine);
                }
                sb.Append("EN").Append(NewLine);
            }
        }

        // ============================================================
        //  Bloco SI (informacoes adicionais)
        // ============================================================

        private static void WriteNotes(StringBuilder sb, DstvFile f)
        {
            if (string.IsNullOrWhiteSpace(f.Notes)) return;

            sb.Append("SI").Append(NewLine);
            // Quebra a string em linhas e prefixa cada uma com 2 espacos
            foreach (string line in f.Notes.Replace("\r", "").Split('\n'))
            {
                sb.Append("  ").Append(line.Trim()).Append(NewLine);
            }
            sb.Append("EN").Append(NewLine);
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private static void AppendField(StringBuilder sb, string value)
        {
            // Indentacao padrao DSTV: dois espacos antes do campo
            sb.Append("  ").Append(value ?? string.Empty).Append(NewLine);
        }

        /// <summary>
        /// Formata numero com ate 2 casas decimais, ponto invariante,
        /// removendo zeros a direita (12.50 -> "12.5", 12.00 -> "12").
        /// Maquinas CNC nao toleram virgula como separador decimal.
        /// </summary>
        public static string FormatNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                // Antes retornava "0" silenciosamente — CNC recebia arquivo "valido" com dim 0.
                // Agora loga aviso para que fique evidente em diagnostico; mantem "0" para nao quebrar estrutura do arquivo.
                // Debug.WriteLine em vez de Logger.Warn para manter este arquivo puro (linkavel em testes sem Serilog).
                System.Diagnostics.Debug.WriteLine("[DstvFileWriter] Valor nao-finito detectado (NaN/Infinity), substituido por 0");
                return "0";
            }

            // Arredondar para 2 casas e remover zeros desnecessarios
            double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            string s = rounded.ToString("0.##", Inv);
            return s;
        }

        private static int CompareHoles(DstvHole a, DstvHole b)
        {
            int c = a.XMm.CompareTo(b.XMm);
            if (c != 0) return c;
            c = a.YMm.CompareTo(b.YMm);
            if (c != 0) return c;
            return a.DiameterMm.CompareTo(b.DiameterMm);
        }
    }
}
