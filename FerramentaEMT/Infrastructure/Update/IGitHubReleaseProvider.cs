using System.Threading;
using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Abstracao sobre a GitHub Releases API. Existe para permitir mockagem
    /// nos testes do <see cref="UpdateCheckService"/> — a impl concreta
    /// (<see cref="GitHubReleaseProvider"/>) usa <c>HttpClient</c> e
    /// nao eh testavel sem rede.
    /// </summary>
    public interface IGitHubReleaseProvider
    {
        /// <summary>
        /// Retorna o ultimo release publicado (nao draft, nao pre-release por padrao).
        /// Retorna null em qualquer condicao de falha — caller decide se eh
        /// "Unknown" ou "NoUpdate".
        /// </summary>
        /// <param name="ct">Cancellation token, respeitado durante o request HTTP.</param>
        Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken ct);
    }
}
