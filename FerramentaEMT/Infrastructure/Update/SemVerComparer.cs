using System;
using System.Globalization;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Comparador SemVer 2.0.0 simplificado para uso interno do auto-update.
    /// Aceita formatos como "1.6.0", "v1.6.0", "1.7.0-rc.1", "v2.0.0".
    ///
    /// Pure C#, sem dependencia de Revit ou IO — testavel em xUnit.
    /// </summary>
    public static class SemVerComparer
    {
        /// <summary>
        /// Tenta parsear uma string SemVer. Aceita prefixo "v" opcional.
        /// Pre-release tags (ex: "-rc.1") sao reconhecidas mas comparadas
        /// lexicograficamente (suficiente para nosso caso de uso).
        /// </summary>
        /// <returns>true se o parse foi bem-sucedido</returns>
        public static bool TryParse(string raw, out SemVer result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string s = raw.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);
            if (s.Length == 0) return false;

            string preRelease = string.Empty;
            int dashIdx = s.IndexOf('-');
            if (dashIdx >= 0)
            {
                preRelease = s.Substring(dashIdx + 1);
                s = s.Substring(0, dashIdx);
            }

            string[] parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;

            int major = 0, minor = 0, patch = 0;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major))
                return false;
            if (parts.Length >= 2 && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor))
                return false;
            if (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch))
                return false;

            if (major < 0 || minor < 0 || patch < 0) return false;

            result = new SemVer(major, minor, patch, preRelease);
            return true;
        }

        /// <summary>
        /// Compara duas versoes. Retorna negativo se a &lt; b, zero se iguais, positivo se a &gt; b.
        /// Pre-release sempre &lt; release final (ex: 1.7.0-rc.1 &lt; 1.7.0).
        /// </summary>
        public static int Compare(SemVer a, SemVer b)
        {
            int cmp = a.Major.CompareTo(b.Major);
            if (cmp != 0) return cmp;
            cmp = a.Minor.CompareTo(b.Minor);
            if (cmp != 0) return cmp;
            cmp = a.Patch.CompareTo(b.Patch);
            if (cmp != 0) return cmp;

            // Pre-release < release final
            bool aPre = !string.IsNullOrEmpty(a.PreRelease);
            bool bPre = !string.IsNullOrEmpty(b.PreRelease);
            if (aPre && !bPre) return -1;
            if (!aPre && bPre) return 1;
            if (!aPre && !bPre) return 0;

            return string.CompareOrdinal(a.PreRelease, b.PreRelease);
        }

        /// <summary>
        /// Conveniencia: parseia ambos e compara. Retorna null se algum falhar parse.
        /// </summary>
        public static int? CompareStrings(string a, string b)
        {
            if (!TryParse(a, out SemVer va)) return null;
            if (!TryParse(b, out SemVer vb)) return null;
            return Compare(va, vb);
        }
    }

    /// <summary>
    /// Representa uma versao SemVer parseada.
    /// </summary>
    public struct SemVer : IEquatable<SemVer>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string PreRelease { get; }

        public SemVer(int major, int minor, int patch, string preRelease)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease ?? string.Empty;
        }

        public bool Equals(SemVer other) =>
            Major == other.Major && Minor == other.Minor &&
            Patch == other.Patch && PreRelease == other.PreRelease;

        public override bool Equals(object obj) => obj is SemVer other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major;
                hash = (hash * 397) ^ Minor;
                hash = (hash * 397) ^ Patch;
                hash = (hash * 397) ^ (PreRelease?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString()
        {
            string baseVer = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);
            return string.IsNullOrEmpty(PreRelease) ? baseVer : baseVer + "-" + PreRelease;
        }
    }
}
