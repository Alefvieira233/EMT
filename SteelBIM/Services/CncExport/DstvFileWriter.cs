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
    /// Estrutura do arquivo NC1 (ordem dos blocos), validada byte-a-byte contra
    /// arquivos .nc1 reais do fabricante (fixtures CH02/CH03/CH04 — golden test):
    ///   ST     -> cabecalho (always present). SEM terminador EN proprio.
    ///   AK     -> outer contour (opt-in via DstvFile.IncluirContornoAk; default off)
    ///   BO     -> hole block unico, uma linha por furo (so se houver furos)
    ///   SC     -> cuts at ends (gerado se HasMiteredEnds())
    ///   SI     -> additional information (opcional)
    ///   EN     -> end of file (UNICO EN do arquivo)
    ///
    /// IMPORTANTE: ao contrario de implementacoes ingenuas, o formato DSTV NC1 tem
    /// apenas UM marcador EN (fim do arquivo). Os blocos ST/AK/BO/SC/SI NAO sao
    /// terminados por EN — a leitura termina no proximo marcador de bloco de 2 letras.
    ///
    /// Campos numericos do bloco ST e blocos AK/BO usam colunas de largura fixa
    /// (right-aligned), com casas decimais fixas — ver helper <see cref="Col"/>.
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
            WriteHoles(sb, file);
            WriteCuts(sb, file);
            WriteNotes(sb, file);
            sb.Append("EN").Append(NewLine); // unico EN — fim do arquivo

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
        // Emite os pontos de DstvFile.ContornoAk (X, Y, Raio). Quando o contorno
        // esta vazio mas o usuario pediu AK, gera o retangulo padrao da face da alma
        // — (0,0)->(L,0)->(L,h)->(0,h)->(0,0). Fica atras de IncluirContornoAk
        // (default false) ate o contorno extraido do Revit ser validado na maquina alvo.
        //
        // Formato de cada linha (largura fixa, validado contra .nc1 do fabricante):
        //   <face:3><X:11><flag:1><Y:10><Raio:11><0:11><0:11><0:11><0:11>
        // A face 'v' e o marcador de inicio 'u' aparecem APENAS no primeiro ponto.

        private static void WriteOuterContour(StringBuilder sb, DstvFile f)
        {
            if (!f.IncluirContornoAk)
                return;

            System.Collections.Generic.List<(double X, double Y, double Raio)> pts = f.ContornoAk;
            if (pts == null || pts.Count == 0)
            {
                // Fallback retangular (comportamento v2.8.0) quando nao ha contorno explicito.
                if (f.CutLengthMm <= 0 || f.ProfileHeightMm <= 0)
                    return;
                pts = RetanguloFallback(f.CutLengthMm, f.ProfileHeightMm);
            }

            sb.Append("AK").Append(NewLine);
            for (int i = 0; i < pts.Count; i++)
            {
                (double x, double y, double raio) = pts[i];
                string face = i == 0 ? "  v" : "   ";
                string flag = i == 0 ? "u" : " ";
                sb.Append(face)
                  .Append(Col(x, 11, 2))
                  .Append(flag)
                  .Append(Col(y, 10, 2))
                  .Append(Col(raio, 11, 2))
                  .Append(Col(0.0, 11, 2))
                  .Append(Col(0.0, 11, 2))
                  .Append(Col(0.0, 11, 2))
                  .Append(Col(0.0, 11, 2))
                  .Append(NewLine);
            }
        }

        // Contorno retangular fechado da face da alma (canto reto, raio 0).
        private static System.Collections.Generic.List<(double X, double Y, double Raio)> RetanguloFallback(double l, double h)
            => new System.Collections.Generic.List<(double, double, double)>
            {
                (0.0, 0.0, 0.0),
                (l,   0.0, 0.0),
                (l,   h,   0.0),
                (0.0, h,   0.0),
                (0.0, 0.0, 0.0), // fecha o contorno
            };

        // ============================================================
        //  Bloco ST (cabecalho)
        // ============================================================

        private static void WriteHeader(StringBuilder sb, DstvFile f)
        {
            sb.Append("ST").Append(NewLine);

            // Linha-comentario com o nome do arquivo NC1 ("** CH02.nc1"), como nos
            // arquivos reais do fabricante. Sem o recuo de 2 espacos dos demais campos.
            string ncName = string.IsNullOrWhiteSpace(f.OutputFileName)
                ? SanitizeAscii(f.DrawingNumber) + ".nc1"
                : SanitizeAscii(f.OutputFileName);
            sb.Append("** ").Append(ncName).Append(NewLine);

            // 8 campos administrativos (texto, recuo de 2 espacos), na ordem DSTV.
            AppendField(sb, f.OrderNumber);                  // order
            AppendField(sb, f.DrawingNumber);                // drawing
            AppendField(sb, f.Phase);                        // phase
            AppendField(sb, f.PieceMark);                    // piece mark
            AppendField(sb, f.SteelQuality);                 // steel quality
            AppendField(sb, f.Quantity.ToString(Inv));       // quantity
            AppendField(sb, f.ProfileName);                  // profile
            AppendField(sb, f.ProfileType.ToDstvCode());     // profile code

            // 12 campos numericos em colunas de largura fixa (11). As 6 dimensoes
            // lineares com 2 casas; peso/area/4-angulos com 3 casas. Validado byte-a-byte
            // contra CH02/CH03/CH04. A linha de comprimento repete o valor (alma,mesa)
            // separado por virgula — "620.00,620.00".
            sb.Append(Col(f.CutLengthMm, 11, 2)).Append(',')
              .Append(f.CutLengthMm.ToString("F2", Inv)).Append(NewLine); // 1.  length [mm]
            AppendNum(sb, f.ProfileHeightMm, 2);             // 2.  height [mm]
            AppendNum(sb, f.FlangeWidthMm, 2);               // 3.  flange width [mm]
            AppendNum(sb, f.WebThicknessMm, 2);              // 4.  web thickness [mm]
            AppendNum(sb, f.FlangeThicknessMm, 2);           // 5.  flange thickness [mm]
            AppendNum(sb, f.FilletRadiusMm, 2);              // 6.  radius [mm]
            AppendNum(sb, f.WeightPerMeter, 3);              // 7.  weight [kg/m]
            AppendNum(sb, f.PaintingSurfacePerMeter, 3);     // 8.  painting surface [m2/m]
            AppendNum(sb, 0.0, 3);                           // 9.  web start angle (miter -> bloco SC)
            AppendNum(sb, 0.0, 3);                           // 10. web end angle
            AppendNum(sb, 0.0, 3);                           // 11. flange start angle
            AppendNum(sb, 0.0, 3);                           // 12. flange end angle

            // 4 linhas de texto vazias que encerram o bloco ST (campos texto 1-4 nao
            // usados). Linhas REALMENTE vazias — sem o recuo de 2 espacos.
            sb.Append(NewLine).Append(NewLine).Append(NewLine).Append(NewLine);

            // NB: o bloco ST NAO tem terminador EN — flui direto para o proximo bloco.
        }

        // Campo numerico do bloco ST: coluna largura 11, right-aligned, casas fixas.
        private static void AppendNum(StringBuilder sb, double value, int decimals)
            => sb.Append(Col(value, 11, decimals)).Append(NewLine);

        // ============================================================
        //  Bloco SC (cortes em extremidade)
        // ============================================================

        private static void WriteCuts(StringBuilder sb, DstvFile f)
        {
            if (!f.HasMiteredEnds())
                return;

            sb.Append("SC").Append(NewLine);
            // Formato simplificado: angulo de inicio e angulo de fim em graus.
            sb.Append("  ")
              .Append(FormatNumber(f.CutAngleStartDeg))
              .Append(" ")
              .Append(FormatNumber(f.CutAngleEndDeg))
              .Append(NewLine);
            // Sem EN proprio — encerra no proximo marcador de bloco / EN final.
        }

        // ============================================================
        //  Bloco BO (furos) — bloco unico, uma linha por furo
        // ============================================================
        // Formato de cada linha (largura fixa, validado contra .nc1 do fabricante):
        //   "  " + <faceCode> + <X:11> + 's' + <Y:10> + <Diametro:11>
        // O marcador 's' (col. 14) acompanha cada furo nos arquivos de referencia.
        // Ordenado por (X, Y, diametro) para arquivo deterministico (diff estavel).

        private static void WriteHoles(StringBuilder sb, DstvFile f)
        {
            if (f.Holes == null || f.Holes.Count == 0)
                return;

            var holes = new System.Collections.Generic.List<DstvHole>(f.Holes);
            holes.Sort(CompareHoles);

            sb.Append("BO").Append(NewLine);
            foreach (DstvHole h in holes)
            {
                sb.Append("  ").Append(h.Face.ToDstvCode())
                  .Append(Col(h.XMm, 11, 2))
                  .Append('s')
                  .Append(Col(h.YMm, 10, 2))
                  .Append(Col(h.DiameterMm, 11, 2))
                  .Append(NewLine);
            }
            // Sem EN proprio.
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
            // Sem EN proprio — encerra no EN final do arquivo.
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
        /// Formata um numero em coluna de largura fixa, right-aligned, com numero fixo
        /// de casas decimais (ponto invariante). Usado nos blocos ST/AK/BO do NC1, cujas
        /// colunas tem posicao byte-exata. Ex.: <c>Col(620, 11, 2)</c> = <c>"     620.00"</c>.
        /// NaN/Infinity caem para 0 (mesma politica defensiva de <see cref="FormatNumber"/>).
        /// </summary>
        public static string Col(double value, int width, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                System.Diagnostics.Debug.WriteLine("[DstvFileWriter] Valor nao-finito em coluna fixa, substituido por 0");
                value = 0.0;
            }

            string s = value.ToString("F" + decimals.ToString(Inv), Inv);
            return s.PadLeft(width);
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
