#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using SteelBIM.Models.CncExport;

namespace SteelBIM.Services.CncExport
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
    ///   AK     -> outer contour (opt-in via DstvFile.IncluirContornoAk; default off)
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
            WriteOuterContour(sb, file);
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
        //  Bloco AK (contorno externo) — OPT-IN, desligado por padrao
        // ============================================================
        // Gera o contorno retangular da face da alma (v): comprimento x altura do perfil
        // — (0,0)->(L,0)->(L,h)->(0,h)->(0,0). Muitos leitores DSTV exigem AK, mas o
        // contorno exato varia por perfil/maquina; por isso fica atras de
        // DstvFile.IncluirContornoAk (default false) ate validar contra um .nc1 real.

        private static void WriteOuterContour(StringBuilder sb, DstvFile f)
        {
            if (!f.IncluirContornoAk)
                return;
            if (f.CutLengthMm <= 0 || f.ProfileHeightMm <= 0)
                return;

            string len = FormatNumber(f.CutLengthMm);
            string hgt = FormatNumber(f.ProfileHeightMm);
            string zero = FormatNumber(0.0);

            sb.Append("AK").Append(NewLine);
            AppendContourPoint(sb, zero, zero);
            AppendContourPoint(sb, len, zero);
            AppendContourPoint(sb, len, hgt);
            AppendContourPoint(sb, zero, hgt);
            AppendContourPoint(sb, zero, zero); // fecha o contorno
            sb.Append("EN").Append(NewLine);
        }

        // Ponto de contorno na face da alma 'v': "<face> <x> <y> <raio=0>" (canto reto).
        private static void AppendContourPoint(StringBuilder sb, string x, string y)
        {
            sb.Append(" v ").Append(x)
              .Append(' ').Append(y)
              .Append(' ').Append(FormatNumber(0.0))
              .Append(NewLine);
        }

        // ============================================================
        //  Bloco ST (cabecalho)
        // ============================================================

        private static void WriteHeader(StringBuilder sb, DstvFile f)
        {
            sb.Append("ST").Append(NewLine);

            // Bloco ST na ordem padrao DSTV NC1 (24 campos, um por linha, prefixados por
            // dois espacos). CORRECAO v2.8.9: o comprimento (Length) e' o campo 9 — logo
            // apos o codigo do perfil — e NAO o ultimo (estava na pos. 16); o tratamento de
            // superficie e' campo de TEXTO (21), nao numerico no meio das dimensoes. Os 4
            // angulos de corte e a area de pintura completam os 24 campos que os leitores
            // DSTV (Tekla/Advance/FICEP/Voortman/Peddinghaus) esperam.
            AppendField(sb, f.OrderNumber);                       // 1.  order
            AppendField(sb, f.DrawingNumber);                     // 2.  drawing
            AppendField(sb, f.Phase);                             // 3.  phase
            AppendField(sb, f.PieceMark);                         // 4.  piece mark
            AppendField(sb, f.SteelQuality);                      // 5.  steel quality
            AppendField(sb, f.Quantity.ToString(Inv));            // 6.  quantity
            AppendField(sb, f.ProfileName);                       // 7.  profile
            AppendField(sb, f.ProfileType.ToDstvCode());          // 8.  profile code
            AppendField(sb, FormatNumber(f.CutLengthMm));         // 9.  length [mm]
            AppendField(sb, FormatNumber(f.ProfileHeightMm));     // 10. height [mm]
            AppendField(sb, FormatNumber(f.FlangeWidthMm));       // 11. flange width [mm]
            AppendField(sb, FormatNumber(f.FlangeThicknessMm));   // 12. flange thickness [mm]
            AppendField(sb, FormatNumber(f.WebThicknessMm));      // 13. web thickness [mm]
            AppendField(sb, FormatNumber(f.FilletRadiusMm));      // 14. radius [mm]
            AppendField(sb, FormatNumber(f.WeightPerMeter));      // 15. weight [kg/m]
            AppendField(sb, FormatNumber(0.0));                   // 16. painting surface [m2/m] (nao calculado)
            AppendField(sb, FormatNumber(0.0));                   // 17. web start angle (0 = corte reto; miter -> bloco SC)
            AppendField(sb, FormatNumber(0.0));                   // 18. web end angle
            AppendField(sb, FormatNumber(0.0));                   // 19. flange start angle
            AppendField(sb, FormatNumber(0.0));                   // 20. flange end angle
            AppendField(sb, f.SurfaceTreatment);                  // 21. text 1 (tratamento de superficie)
            AppendField(sb, "");                                  // 22. text 2
            AppendField(sb, "");                                  // 23. text 3
            AppendField(sb, "");                                  // 24. text 4

            sb.Append("EN").Append(NewLine);
        }

        // ============================================================
        //  Bloco SC (cortes em extremidade)
        // ============================================================

        private static void WriteCuts(StringBuilder sb, DstvFile f)
        {
            if (!f.HasMiteredEnds())
                return;

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
            if (f.Holes == null || f.Holes.Count == 0)
                return;

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
            if (string.IsNullOrWhiteSpace(f.Notes))
                return;

            sb.Append("SI").Append(NewLine);
            // Quebra a string em linhas e prefixa cada uma com 2 espacos
            foreach (string line in f.Notes.Replace("\r", "").Split('\n'))
            {
                sb.Append("  ").Append(SanitizeAscii(line.Trim())).Append(NewLine);
            }
            sb.Append("EN").Append(NewLine);
        }

        // ============================================================
        //  Helpers
        // ============================================================

        private static void AppendField(StringBuilder sb, string? value)
        {
            // Indentacao padrao DSTV: dois espacos antes do campo.
            // v2.8.9: transliterar acentos PT-BR para ASCII (em vez do Encoding.ASCII
            // trocar por '?') — marca/nota da peca fica legivel e rastreavel no NC1.
            sb.Append("  ").Append(SanitizeAscii(value ?? string.Empty)).Append(NewLine);
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

        /// <summary>
        /// Converte um texto para ASCII transliterando acentos PT-BR comuns
        /// (ã→a, ç→c, é→e, …) em vez de deixar o <see cref="Encoding.ASCII"/> trocar por '?'.
        /// Mantem a marca/nota da peca legivel e rastreavel no NC1. Identidade em ASCII puro;
        /// caracteres nao-ASCII sem mapeamento viram '?'.
        /// </summary>
        public static string SanitizeAscii(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            // v2.8.9: normalizacao Unicode (FormD) decompoe cada acento em base + marca
            // combinante; descartamos a marca e mantemos o ASCII. Cobre todos os acentos
            // (nao so uma lista) e e' format-safe (sem switch multi-case por linha).
            string decomposto = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);
            foreach (char c in decomposto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(c <= 0x7F ? c : '?');
            }
            return sb.ToString();
        }

        private static int CompareHoles(DstvHole a, DstvHole b)
        {
            int c = a.XMm.CompareTo(b.XMm);
            if (c != 0)
                return c;
            c = a.YMm.CompareTo(b.YMm);
            if (c != 0)
                return c;
            return a.DiameterMm.CompareTo(b.DiameterMm);
        }
    }
}
