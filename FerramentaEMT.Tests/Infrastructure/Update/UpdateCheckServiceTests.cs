using System;
using System.Threading;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Privacy;
using FerramentaEMT.Infrastructure.Update;
using FerramentaEMT.Models.Privacy;
using FluentAssertions;
using Moq;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    public class UpdateCheckServiceTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);

        private static UpdateCheckService BuildService(
            Mock<IGitHubReleaseProvider> provider,
            Mock<IPrivacySettingsStore> store,
            string currentVersion = "1.6.0",
            DateTime? now = null)
        {
            return new UpdateCheckService(
                provider.Object,
                store.Object,
                currentVersion,
                () => now ?? FixedNow);
        }

        private static Mock<IPrivacySettingsStore> StoreReturning(PrivacySettings settings)
        {
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            store.Setup(s => s.Load()).Returns(settings);
            return store;
        }

        private static GitHubRelease ReleaseWith(
            string tagName,
            bool draft = false,
            bool preRelease = false,
            string htmlUrl = "https://example.com/release",
            params (string Name, long Size)[] assets)
        {
            GitHubRelease release = new GitHubRelease
            {
                TagName = tagName,
                Draft = draft,
                PreRelease = preRelease,
                HtmlUrl = htmlUrl,
            };
            foreach (var (name, size) in assets)
            {
                release.Assets.Add(new GitHubAsset { Name = name, Size = size });
            }
            return release;
        }

        // ---------- consent gate ----------

        [Fact]
        public async Task ConsentUnset_retorna_ConsentRequired_e_nao_chama_provider()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Unset,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.ConsentRequired);
            provider.Verify(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConsentDenied_retorna_ConsentDenied_e_nao_chama_provider()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Denied,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.ConsentDenied);
            provider.Verify(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- cache 24h ----------

        [Fact]
        public async Task ConsentGranted_LastCheck_recente_retorna_NoUpdate_sem_chamar_provider()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
                LastUpdateCheckUtc = FixedNow.AddHours(-2),  // 2h atras = cache hit
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
            provider.Verify(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ConsentGranted_LastCheck_25h_atras_chama_provider()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("1.6.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
                LastUpdateCheckUtc = FixedNow.AddHours(-25),
            });

            await BuildService(provider, store).CheckAsync(CancellationToken.None);

            provider.Verify(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---------- comparacao SemVer ----------

        [Fact]
        public async Task Versao_remota_maior_retorna_UpdateAvailable()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.7.0", htmlUrl: "https://github.com/x/y/releases/tag/v1.7.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
            result.LatestVersion.Should().Be("v1.7.0");
            result.ReleaseUrl.Should().Be("https://github.com/x/y/releases/tag/v1.7.0");
            result.Release.Should().NotBeNull();
        }

        [Fact]
        public async Task Versao_local_igual_remota_retorna_NoUpdate()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.6.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
        }

        [Fact]
        public async Task Versao_local_maior_que_remota_retorna_NoUpdate()
        {
            // Cenario de downgrade malicioso: alguem republicou v1.5.0 como "latest"
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.5.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
        }

        // ---------- skipped version ----------

        [Fact]
        public async Task Versao_remota_igual_a_SkippedUpdateVersion_retorna_NoUpdate()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.7.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
                SkippedUpdateVersion = "v1.7.0",
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
        }

        [Fact]
        public async Task Versao_remota_diferente_de_SkippedUpdateVersion_ainda_eh_UpdateAvailable()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.7.1"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
                SkippedUpdateVersion = "v1.7.0",  // pulou v1.7.0, mas v1.7.1 saiu
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
        }

        // ---------- draft / pre-release ----------

        [Fact]
        public async Task Release_draft_eh_ignorado_retorna_NoUpdate()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.7.0", draft: true));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
        }

        [Fact]
        public async Task Release_prerelease_eh_ignorado_retorna_NoUpdate()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.7.0-rc.1", preRelease: true));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store, currentVersion: "1.6.0")
                .CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.NoUpdate);
        }

        // ---------- falhas do provider / parsing ----------

        [Fact]
        public async Task Provider_retorna_null_resulta_em_Unknown()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((GitHubRelease)null);

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.Unknown);
        }

        [Fact]
        public async Task Provider_lanca_excecao_resulta_em_Unknown()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("inesperado"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.Unknown);
        }

        [Fact]
        public async Task Tag_nao_SemVer_resulta_em_Unknown()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("nightly-build"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(CancellationToken.None);

            result.Outcome.Should().Be(UpdateCheckOutcome.Unknown);
        }

        [Fact]
        public async Task Cancellation_durante_provider_resulta_em_Unknown()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new OperationCanceledException());

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            UpdateCheckResult result = await BuildService(provider, store).CheckAsync(cts.Token);

            result.Outcome.Should().Be(UpdateCheckOutcome.Unknown);
        }

        // ---------- persistencia de LastCheck ----------

        [Fact]
        public async Task Sucesso_persiste_LastUpdateCheckUtc()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(ReleaseWith("v1.6.0"));

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            await BuildService(provider, store).CheckAsync(CancellationToken.None);

            store.Verify(s => s.Save(It.Is<PrivacySettings>(p => p.LastUpdateCheckUtc == FixedNow)),
                Times.Once);
        }

        [Fact]
        public async Task Falha_de_provider_NAO_persiste_LastUpdateCheckUtc()
        {
            // Importante: Unknown nao deve gastar o cache (usuario pode tentar de novo
            // assim que voltar a internet).
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            provider.Setup(p => p.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((GitHubRelease)null);

            Mock<IPrivacySettingsStore> store = StoreReturning(new PrivacySettings
            {
                AutoUpdate = ConsentState.Granted,
            });

            await BuildService(provider, store).CheckAsync(CancellationToken.None);

            store.Verify(s => s.Save(It.IsAny<PrivacySettings>()), Times.Never);
        }

        // ---------- argumentos invalidos ----------

        [Fact]
        public void Construtor_provider_null_lanca()
        {
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            Action act = () => new UpdateCheckService(null, store.Object, "1.6.0");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Construtor_store_null_lanca()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            Action act = () => new UpdateCheckService(provider.Object, null, "1.6.0");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Construtor_currentVersion_vazia_lanca()
        {
            Mock<IGitHubReleaseProvider> provider = new Mock<IGitHubReleaseProvider>();
            Mock<IPrivacySettingsStore> store = new Mock<IPrivacySettingsStore>();
            Action act = () => new UpdateCheckService(provider.Object, store.Object, "");
            act.Should().Throw<ArgumentException>();
        }
    }
}
