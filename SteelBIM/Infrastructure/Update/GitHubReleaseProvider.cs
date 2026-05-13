using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Implementacao real do <see cref="IGitHubReleaseProvider"/> usando HttpClient.
    /// HttpClient eh singleton (campo estatico) — criar um por chamada exhausta sockets.
    ///
    /// Nao testavel via xUnit (precisa rede). Validacao via smoke test manual.
    /// </summary>
    public sealed class GitHubReleaseProvider : IGitHubReleaseProvider
    {
        private static readonly HttpClient _httpClient = CreateClient();
        private readonly string _owner;
        private readonly string _repo;

        public GitHubReleaseProvider(string owner, string repo)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("owner obrigatorio", "owner");
            if (string.IsNullOrWhiteSpace(repo)) throw new ArgumentException("repo obrigatorio", "repo");
            _owner = owner;
            _repo = repo;
        }

        private static HttpClient CreateClient()
        {
            // HttpClient herda proxy do sistema por padrao (HttpClientHandler.UseProxy=true).
            // Em ambientes corporativos com proxy autenticado, o usuario precisa estar
            // logado no Windows com credenciais validas — nao hookamos prompt.
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5), // timeout total do GET /releases/latest
            };

            // GitHub API exige User-Agent. Usamos o nome+versao do assembly do plugin.
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FerramentaEMT/" + version);

            // GitHub API v2022-11-28 (estavel, nao precisa header explicito mas eh boa pratica)
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            return client;
        }

        public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken ct)
        {
            string url = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "https://api.github.com/repos/{0}/{1}/releases/latest",
                _owner, _repo);

            try
            {
                using (HttpResponseMessage response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Logger.Info("[Update] Nenhum release publicado em {Owner}/{Repo}", _owner, _repo);
                        return null;
                    }
                    if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // 429 = rate limit, 403 com X-RateLimit-Remaining=0 tambem.
                        Logger.Warn("[Update] GitHub API rate limit atingido (status {Status})", (int)response.StatusCode);
                        return null;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Warn("[Update] GitHub API retornou status {Status}", (int)response.StatusCode);
                        return null;
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseRelease(json);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Logger.Info("[Update] Verificacao cancelada");
                return null;
            }
            catch (TaskCanceledException)
            {
                // HttpClient lanca TaskCanceledException em timeout (sem ct disparado)
                Logger.Warn("[Update] Timeout na verificacao de novas versoes");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warn(ex, "[Update] Falha de rede ao consultar GitHub API");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "[Update] Falha inesperada ao consultar releases");
                return null;
            }
        }

        /// <summary>
        /// Parse manual via JsonDocument. Internal pra ser testavel por outra
        /// classe se necessario, mas nao expomos nos testes desta camada.
        /// </summary>
        internal static GitHubRelease ParseRelease(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return null;

                    GitHubRelease release = new GitHubRelease
                    {
                        TagName = TryGetString(root, "tag_name"),
                        Name = TryGetString(root, "name"),
                        Draft = TryGetBool(root, "draft"),
                        PreRelease = TryGetBool(root, "prerelease"),
                        HtmlUrl = TryGetString(root, "html_url"),
                    };

                    if (root.TryGetProperty("assets", out JsonElement assetsElem)
                        && assetsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement assetElem in assetsElem.EnumerateArray())
                        {
                            if (assetElem.ValueKind != JsonValueKind.Object) continue;
                            release.Assets.Add(new GitHubAsset
                            {
                                Name = TryGetString(assetElem, "name"),
                                Size = TryGetLong(assetElem, "size"),
                                BrowserDownloadUrl = TryGetString(assetElem, "browser_download_url"),
                                ContentType = TryGetString(assetElem, "content_type"),
                            });
                        }
                    }

                    return release;
                }
            }
            catch (JsonException ex)
            {
                Logger.Warn(ex, "[Update] JSON da GitHub API malformado");
                return null;
            }
        }

        private static string TryGetString(JsonElement parent, string name)
        {
            if (parent.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
            return null;
        }

        private static bool TryGetBool(JsonElement parent, string name)
        {
            if (parent.TryGetProperty(name, out JsonElement el))
            {
                if (el.ValueKind == JsonValueKind.True) return true;
                if (el.ValueKind == JsonValueKind.False) return false;
            }
            return false;
        }

        private static long TryGetLong(JsonElement parent, string name)
        {
            if (parent.TryGetProperty(name, out JsonElement el)
                && el.ValueKind == JsonValueKind.Number
                && el.TryGetInt64(out long val))
                return val;
            return 0;
        }
    }
}
