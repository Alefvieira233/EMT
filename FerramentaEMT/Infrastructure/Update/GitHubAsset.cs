namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Asset de um GitHub Release (arquivo anexado, ex: o .zip ou checksums.txt).
    /// </summary>
    public sealed class GitHubAsset
    {
        /// <summary>Nome do arquivo (ex: "FerramentaEMT-Revit2025-Release.zip").</summary>
        public string Name { get; set; }

        /// <summary>Tamanho em bytes — usado para sanity check.</summary>
        public long Size { get; set; }

        /// <summary>URL para download direto (com extensao do arquivo).</summary>
        public string BrowserDownloadUrl { get; set; }

        /// <summary>Content-Type (ex: "application/zip").</summary>
        public string ContentType { get; set; }
    }
}
