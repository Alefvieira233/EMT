using System;
using System.IO;
using System.IO.Compression;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Detecta tentativas de zip-slip em arquivos zip antes da extracao.
    /// Zip-slip = entry com nome contendo ".." ou path absoluto, que escaparia
    /// do diretorio de extracao e sobrescreveria arquivos arbitrarios do sistema.
    ///
    /// Pure C#, sem IO de rede ou logging — testavel em xUnit.
    /// </summary>
    public static class ZipSlipValidator
    {
        /// <summary>
        /// Valida um nome de entry de zip. Retorna true se eh seguro.
        /// Nome seguro: caminho relativo, sem ".." em qualquer segmento,
        /// sem prefixo de drive (ex: "C:\") ou inicio com "/".
        /// </summary>
        public static bool IsSafeEntryName(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return false;

            // Path absoluto (Unix-style)
            if (entryName.StartsWith("/", StringComparison.Ordinal)) return false;
            if (entryName.StartsWith("\\", StringComparison.Ordinal)) return false;

            // Path absoluto (Windows com drive)
            if (entryName.Length >= 2 && entryName[1] == ':') return false;

            // ".." em qualquer segmento (separados por / ou \)
            string normalized = entryName.Replace('\\', '/');
            string[] segments = normalized.Split('/');
            foreach (string seg in segments)
            {
                if (seg == "..") return false;
            }

            return true;
        }

        /// <summary>
        /// Itera todas as entries do <see cref="ZipArchive"/> e retorna true
        /// se todas sao seguras. Retorna false ao encontrar a primeira entry
        /// suspeita (caller deve abortar a extracao).
        /// </summary>
        public static bool AllEntriesSafe(ZipArchive archive)
        {
            if (archive == null) throw new ArgumentNullException("archive");

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!IsSafeEntryName(entry.FullName))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
