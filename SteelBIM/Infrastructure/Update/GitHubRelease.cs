using System.Collections.Generic;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// DTO mapeado da resposta de GET /repos/{owner}/{repo}/releases/latest
    /// da GitHub REST API. Apenas os campos que o auto-update consome.
    ///
    /// Pure C#, sem dependencia de Revit ou IO.
    /// </summary>
    public sealed class GitHubRelease
    {
        /// <summary>Tag name (ex: "v1.7.0"). Comparado com a versao local via SemVerComparer.</summary>
        public string TagName { get; set; }

        /// <summary>Nome legivel do release (ex: "v1.7.0 - Auto-update + Crash reporting").</summary>
        public string Name { get; set; }

        /// <summary>true se for draft (nao deve disparar update).</summary>
        public bool Draft { get; set; }

        /// <summary>true se for pre-release (politica padrao: nao auto-update).</summary>
        public bool PreRelease { get; set; }

        /// <summary>URL HTML para o usuario abrir manualmente.</summary>
        public string HtmlUrl { get; set; }

        /// <summary>Lista de assets (ZIP, checksums.txt, etc).</summary>
        public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
    }
}
