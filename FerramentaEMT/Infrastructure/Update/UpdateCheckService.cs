using System;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Models.Privacy;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Orquestrador da verificacao de atualizacoes. Combina:
    /// - <see cref="IPrivacySettingsStore"/> (consent gate + cache 24h)
    /// - <see cref="IGitHubReleaseProvider"/> (chamada remota)
    /// - <see cref="SemVerComparer"/> (decisao se eh upgrade)
    ///
    /// Pure logic — todas as deps sao injetadas, e portanto testaveis com Moq.
    /// </summary>
    public sealed class UpdateCheckService
    {
        /// <summary>Janela de cache: nao re-consultar GitHub se a ultima verificacao foi ha menos disso.</summary>
        public static readonly TimeSpan CacheWindow = TimeSpan.FromHours(24);

        private static readonly object[] EmptyArgs = new object[0];

        private readonly IGitHubReleaseProvider _provider;
        private readonly IPrivacySettingsStore _settingsStore;
        private readonly Func<DateTime> _utcNow;
        private readonly string _currentVersion;

        public UpdateCheckService(
            IGitHubReleaseProvider provider,
            IPrivacySettingsStore settingsStore,
            string currentVersion,
            Func<DateTime> utcNow = null)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            if (settingsStore == null) throw new ArgumentNullException("settingsStore");
            if (string.IsNullOrWhiteSpace(currentVersion))
                throw new ArgumentException("currentVersion obrigatoria", "currentVersion");

            _provider = provider;
            _settingsStore = settingsStore;
            _currentVersion = currentVersion;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Verifica se ha atualizacao. Honra consent + cache 24h + skip de versao.
        ///
        /// Estrategia:
        /// 1. Le PrivacySettings; se Unset retorna ConsentRequired (caller mostra dialog).
        /// 2. Se Denied retorna ConsentDenied.
        /// 3. Se LastUpdateCheckUtc &lt; 24h e ha cache hit, retorna NoUpdate.
        /// 4. Chama provider; se falhar (null) retorna Unknown.
        /// 5. Valida tag_name parseavel, draft/pre-release, comparacao SemVer.
        /// 6. Persiste LastUpdateCheckUtc apenas em sucesso (Unknown nao gasta o cache).
        /// </summary>
        public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct)
        {
            PrivacySettings settings = _settingsStore.Load();

            if (settings.AutoUpdate == ConsentState.Unset)
            {
                return UpdateCheckResult.ConsentRequired();
            }
            if (settings.AutoUpdate == ConsentState.Denied)
            {
                return UpdateCheckResult.ConsentDenied();
            }

            // Cache 24h: se ja consultou ha menos disso, retorna NoUpdate sem rede
            DateTime now = _utcNow();
            TimeSpan sinceLastCheck = now - settings.LastUpdateCheckUtc;
            if (settings.LastUpdateCheckUtc != DateTime.MinValue && sinceLastCheck < CacheWindow)
            {
                UpdateLog.Debug("[Update] cache hit ({0:F1}h desde ultima verificacao)",
                    new object[] { sinceLastCheck.TotalHours });
                return UpdateCheckResult.NoUpdate(_currentVersion);
            }

            GitHubRelease release;
            try
            {
                release = await _provider.GetLatestReleaseAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                UpdateLog.Info("[Update] verificacao cancelada", EmptyArgs);
                return UpdateCheckResult.Unknown();
            }
            catch (Exception ex)
            {
                UpdateLog.WarnException(ex, "[Update] provider lancou excecao inesperada", EmptyArgs);
                return UpdateCheckResult.Unknown();
            }

            if (release == null)
            {
                return UpdateCheckResult.Unknown();
            }

            if (release.Draft)
            {
                UpdateLog.Info("[Update] release mais recente eh draft — ignorando", EmptyArgs);
                return UpdateCheckResult.NoUpdate(_currentVersion);
            }

            // Politica padrao: nao auto-update para pre-releases.
            // Usuario interessado em rc.X precisa baixar manualmente.
            if (release.PreRelease)
            {
                UpdateLog.Info("[Update] release mais recente eh pre-release — ignorando", EmptyArgs);
                return UpdateCheckResult.NoUpdate(_currentVersion);
            }

            int? cmp = SemVerComparer.CompareStrings(_currentVersion, release.TagName);
            if (cmp == null)
            {
                UpdateLog.Warn("[Update] tag_name nao-SemVer ({0}) ou versao local invalida ({1})",
                    new object[] { release.TagName, _currentVersion });
                return UpdateCheckResult.Unknown();
            }

            // Marca cache hit (mesmo em NoUpdate) — sucesso de rede + parse
            settings.LastUpdateCheckUtc = now;
            _settingsStore.Save(settings);

            if (cmp.Value >= 0)
            {
                if (cmp.Value > 0)
                {
                    UpdateLog.Warn("[Update] versao local ({0}) > remota ({1}) — nada a fazer",
                        new object[] { _currentVersion, release.TagName });
                }
                return UpdateCheckResult.NoUpdate(release.TagName);
            }

            // Versao remota maior: respeitar SkippedUpdateVersion
            if (!string.IsNullOrEmpty(settings.SkippedUpdateVersion)
                && string.Equals(settings.SkippedUpdateVersion, release.TagName, StringComparison.Ordinal))
            {
                UpdateLog.Info("[Update] versao {0} foi pulada pelo usuario",
                    new object[] { release.TagName });
                return UpdateCheckResult.NoUpdate(release.TagName);
            }

            UpdateLog.Info("[Update] nova versao disponivel: {0} -> {1}",
                new object[] { _currentVersion, release.TagName });
            return UpdateCheckResult.Available(release.TagName, release.HtmlUrl, release);
        }
    }
}
