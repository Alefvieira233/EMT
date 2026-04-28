using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FerramentaEMT.Infrastructure.Telemetry;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Telemetry
{
    /// <summary>
    /// Cobre o SessionIdProvider — UUID v4 anonimo, persistencia,
    /// reuso entre boots, e o invariante CRITICO de privacy:
    /// session_id NUNCA contem fragmento de Environment.MachineName
    /// nem Environment.UserName.
    /// </summary>
    [Collection("PostHogApiKeySerial")]
    public class SessionIdProviderTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly string _origEnv;

        public SessionIdProviderTests()
        {
            _origEnv = Environment.GetEnvironmentVariable(SessionIdProvider.TestPathOverrideEnvVar);
            _tempPath = Path.Combine(Path.GetTempPath(),
                "FerramentaEMT-Tests", Guid.NewGuid().ToString("N"), "session-id.json");
            Environment.SetEnvironmentVariable(SessionIdProvider.TestPathOverrideEnvVar, _tempPath);
            SessionIdProvider.ResetCacheForTests();
        }

        public void Dispose()
        {
            SessionIdProvider.ResetCacheForTests();
            Environment.SetEnvironmentVariable(SessionIdProvider.TestPathOverrideEnvVar, _origEnv);
            try
            {
                string dir = Path.GetDirectoryName(_tempPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { /* tmp dir cleanup eh best-effort */ }
        }

        [Fact]
        public void First_call_creates_uuid_v4_and_persists_json_file()
        {
            string id = SessionIdProvider.GetOrCreate();

            id.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(id, out _).Should().BeTrue("session_id deve ser UUID parseavel");
            File.Exists(_tempPath).Should().BeTrue("arquivo deve ser criado no path do override");

            string json = File.ReadAllText(_tempPath);
            json.Should().Contain(id);
            json.Should().Contain("created_at_utc");
        }

        [Fact]
        public void Second_call_reuses_same_id_in_memory()
        {
            string first = SessionIdProvider.GetOrCreate();
            string second = SessionIdProvider.GetOrCreate();

            second.Should().Be(first);
        }

        [Fact]
        public void Reread_from_disk_after_reset_returns_same_id()
        {
            string first = SessionIdProvider.GetOrCreate();

            // Simula novo boot: limpa cache em memoria. Arquivo no disco fica.
            SessionIdProvider.ResetCacheForTests();
            string second = SessionIdProvider.GetOrCreate();

            second.Should().Be(first, "id persistido no disco deve ser reusado");
        }

        [Fact]
        public void Deleting_file_externally_creates_new_id_on_next_call()
        {
            string first = SessionIdProvider.GetOrCreate();
            File.Delete(_tempPath);
            SessionIdProvider.ResetCacheForTests();

            string second = SessionIdProvider.GetOrCreate();

            second.Should().NotBe(first, "arquivo apagado deve gerar id novo");
            Guid.TryParse(second, out _).Should().BeTrue();
        }

        [Fact]
        public void GetShortPrefix_returns_first_eight_chars()
        {
            string id = SessionIdProvider.GetOrCreate();
            string prefix = SessionIdProvider.GetShortPrefix();

            prefix.Should().HaveLength(8);
            id.StartsWith(prefix).Should().BeTrue();
        }

        [Fact]
        public void Session_id_does_not_contain_machine_name_or_username()
        {
            string id = SessionIdProvider.GetOrCreate();

            string machine = Environment.MachineName;
            string user = Environment.UserName;

            // Lower-case match: id eh sempre lower (Guid "D" format), e fragments
            // de 4+ chars do MachineName/UserName seriam coincidencia improvavel.
            // Teste de invariante: o RNG do CLR nao deriva desses valores, entao
            // collision aleatoria > 4 chars eh ~1 em milhoes — aceitavel se
            // assumirmos que o tester nao tem MachineName "0000-..." rs.
            if (!string.IsNullOrEmpty(machine) && machine.Length >= 4)
            {
                id.ToLowerInvariant().Should()
                    .NotContain(machine.ToLowerInvariant().Substring(0, Math.Min(machine.Length, 8)),
                        "session_id NUNCA pode derivar de Environment.MachineName");
            }
            if (!string.IsNullOrEmpty(user) && user.Length >= 4)
            {
                id.ToLowerInvariant().Should()
                    .NotContain(user.ToLowerInvariant().Substring(0, Math.Min(user.Length, 8)),
                        "session_id NUNCA pode derivar de Environment.UserName");
            }
        }

        [Fact]
        public void Concurrent_calls_return_the_same_id()
        {
            var ids = new ConcurrentBag<string>();
            Parallel.For(0, 64, _ =>
            {
                ids.Add(SessionIdProvider.GetOrCreate());
            });

            ids.Should().NotBeEmpty();
            ids.Should().OnlyContain(s => !string.IsNullOrEmpty(s));
            // Todos devem ser o mesmo valor (lock interno)
            ids.Distinct().Should().HaveCount(1, "concorrencia deve retornar mesmo id");
        }

        [Fact]
        public void FilePath_resolves_via_env_override_when_set()
        {
            SessionIdProvider.FilePath.Should().Be(_tempPath);
        }
    }
}
