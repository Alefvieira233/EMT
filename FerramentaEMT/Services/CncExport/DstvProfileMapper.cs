using System;
using System.Text.RegularExpressions;
using FerramentaEMT.Models.CncExport;

namespace FerramentaEMT.Services.CncExport
{
    /// <summary>
    /// Mapeia nomes de familias/perfis Revit para o codigo DSTV correspondente.
    /// </summary>
    /// <remarks>
    /// Implementacao pura — testavel sem Revit.
    ///
    /// Padrao americano (AISC):  W12X26, W14X22, HSS6X4X1/4, L4X4X1/2
    /// Padrao europeu (Euronorm): HEA200, HEB300, IPE160, UPN240
    /// Padrao brasileiro (NBR):  W360X51, CS300X62, VS400X51
    ///
    /// A funcao normaliza tudo para upper-case e tenta os padroes mais comuns primeiro.
    /// </remarks>
    public static class DstvProfileMapper
    {
        // Reconhecer "round HSS"  ex: HSS5.5x0.258, HSS6X0.25, HSSroundXXX
        private static readonly Regex HssRoundPattern = new Regex(
            @"^HSS\s*\d+(\.\d+)?\s*X\s*\d+(\.\d+)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Reconhecer "rectangular HSS"  ex: HSS6X4X1/4, HSS8X4X3/8
        private static readonly Regex HssRectPattern = new Regex(
            @"^HSS\s*\d+(\.\d+)?\s*X\s*\d+(\.\d+)?\s*X\s*\S+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Determina o codigo DSTV a partir do nome de familia e nome de tipo do Revit.
        /// </summary>
        /// <param name="familyName">Nome da familia (ex: "W-Wide Flange").</param>
        /// <param name="typeName">Nome do tipo (ex: "W12X26", "HEA200").</param>
        /// <returns>Codigo DSTV correspondente. <see cref="DstvProfileType.SO"/> se nao reconhecido.</returns>
        public static DstvProfileType Map(string familyName, string typeName)
        {
            string family = (familyName ?? "").Trim().ToUpperInvariant();
            string type = (typeName ?? "").Trim().ToUpperInvariant();

            // 1. Verificar nome do tipo primeiro (mais especifico)
            DstvProfileType? byType = MapByDesignation(type);
            if (byType.HasValue) return byType.Value;

            // 2. Cair para nome da familia
            DstvProfileType? byFamily = MapByDesignation(family);
            if (byFamily.HasValue) return byFamily.Value;

            // 3. Heuristicas de palavras-chave na familia
            if (family.Contains("WIDE FLANGE") || family.Contains("PERFIL I") || family.Contains("PERFIL H"))
                return DstvProfileType.I;
            if (family.Contains("CHANNEL") || family.Contains("PERFIL U"))
                return DstvProfileType.U;
            if (family.Contains("ANGLE") || family.Contains("CANTONEIRA"))
                return DstvProfileType.L;
            if (family.Contains("PLATE") || family.Contains("CHAPA"))
                return DstvProfileType.B;
            if (family.Contains("ROUND TUBE") || family.Contains("PIPE") || family.Contains("TUBO REDONDO"))
                return DstvProfileType.RO;
            if (family.Contains("RECTANGULAR TUBE") || family.Contains("HOLLOW") || family.Contains("TUBO RETANGULAR"))
                return DstvProfileType.M;
            if (family.Contains("ROUND BAR") || family.Contains("BARRA REDONDA"))
                return DstvProfileType.RU;
            if (family.Contains("TEE") || family.Contains("PERFIL T"))
                return DstvProfileType.T;

            return DstvProfileType.SO;
        }

        /// <summary>
        /// Tenta mapear pela designacao em si (ex: "W12X26" -> I, "HEA200" -> I).
        /// </summary>
        private static DstvProfileType? MapByDesignation(string designation)
        {
            if (string.IsNullOrEmpty(designation))
                return null;

            // Senior: para designacoes de perfil (W12X26, C310X45, U200, L4X4, T75...),
            // o digito precisa vir logo apos a letra (tolerando separador opcional).
            // Antes o mapper usava StartsWith(L) + HasDigit, o que classificava strings
            // livres tipo "CUSTOM-001" como U-channel porque comecam com 'C' e tem digito.
            //
            // Tabela:
            //   StartsDigit(s, prefix)  => s comeca com prefix e o proximo char e digito
            //                              (aceita '-' ou espaco como separador opcional)

            // I/H profiles
            // WT vem antes (StartsDigit("W") nao pegaria "WT9X30" pois char[1] = 'T')
            if (StartsDigit(designation, "WT"))
                return DstvProfileType.T;
            if (StartsDigit(designation, "W"))
                return DstvProfileType.I;

            if (designation.StartsWith("HE", StringComparison.Ordinal) ||  // HEA, HEB, HEM, HE
                designation.StartsWith("IPE", StringComparison.Ordinal) ||
                designation.StartsWith("IPN", StringComparison.Ordinal) ||
                designation.StartsWith("HP", StringComparison.Ordinal) ||
                designation.StartsWith("CS", StringComparison.Ordinal) ||  // brasileiro coluna soldada
                designation.StartsWith("VS", StringComparison.Ordinal) ||  // brasileiro viga soldada
                designation.StartsWith("CVS", StringComparison.Ordinal))
                return DstvProfileType.I;

            // S-shapes (American Standard Beam) e M-shapes (Misc beams)
            if ((StartsDigit(designation, "S") || StartsDigit(designation, "M"))
                && !designation.StartsWith("MC", StringComparison.Ordinal))
                return DstvProfileType.I;

            // U-channels
            if (designation.StartsWith("UPN", StringComparison.Ordinal) ||
                designation.StartsWith("UPE", StringComparison.Ordinal) ||
                StartsDigit(designation, "U") ||
                StartsDigit(designation, "C") ||
                designation.StartsWith("MC", StringComparison.Ordinal))
                return DstvProfileType.U;

            // Angles
            if (StartsDigit(designation, "L"))
                return DstvProfileType.L;

            // Tees
            if (StartsDigit(designation, "T") &&
                !designation.StartsWith("TUBO", StringComparison.Ordinal))
                return DstvProfileType.T;

            // HSS — verificar redondo vs retangular
            if (designation.StartsWith("HSS", StringComparison.Ordinal))
            {
                if (HssRectPattern.IsMatch(designation))
                    return DstvProfileType.M;
                if (HssRoundPattern.IsMatch(designation))
                    return DstvProfileType.RO;
                return DstvProfileType.M; // default HSS = retangular
            }

            // Pipe (PIP) / round tubes
            if (designation.StartsWith("PIPE", StringComparison.Ordinal) ||
                designation.StartsWith("PIP", StringComparison.Ordinal))
                return DstvProfileType.RO;

            // Plate / chapa
            if (designation.StartsWith("PL", StringComparison.Ordinal) ||
                designation.StartsWith("CHAPA", StringComparison.Ordinal))
                return DstvProfileType.B;

            return null;
        }

        private static bool HasDigit(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (char.IsDigit(s[i])) return true;
            return false;
        }

        /// <summary>
        /// True se <paramref name="s"/> comeca com <paramref name="prefix"/> e o proximo
        /// caractere (ignorando '-' ou espaco opcional) e um digito.
        /// Ex: StartsDigit("C310X45","C")=true; StartsDigit("CUSTOM-01","C")=false.
        /// </summary>
        private static bool StartsDigit(string s, string prefix)
        {
            if (!s.StartsWith(prefix, StringComparison.Ordinal)) return false;
            int i = prefix.Length;
            // tolerar 1 separador ('-' ou espaco) antes do digito
            if (i < s.Length && (s[i] == '-' || s[i] == ' ')) i++;
            return i < s.Length && char.IsDigit(s[i]);
        }
    }
}
