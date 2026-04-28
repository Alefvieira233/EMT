using System;
using System.Threading.Tasks;
using Sentry;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Implementacao real da ISentryHubFacade — delega para o SentrySdk
    /// estatico. Esta classe NAO eh testavel via xUnit (o SentrySdk
    /// estatico tem state global e lanca quando re-Init em sequencia
    /// rapida nos cenarios CI). Por isso ela NAO entra no test csproj
    /// (o linker em FerramentaEMT.Tests.csproj omite este arquivo).
    /// Toda a logica testavel mora no SentryOptionsBuilder + PiiScrubber +
    /// SentryReporter (que recebe ISentryHubFacade injetado).
    /// </summary>
    public sealed class SentryHubFacade : ISentryHubFacade
    {
        public bool IsEnabled => SentrySdk.IsEnabled;

        public void Init(SentryOptions options)
        {
            SentrySdk.Init(options);
        }

        public SentryId CaptureException(Exception exception)
        {
            return SentrySdk.CaptureException(exception);
        }

        public void SetTag(string key, string value)
        {
            SentrySdk.ConfigureScope(scope => scope.SetTag(key, value));
        }

        public Task FlushAsync(TimeSpan timeout)
        {
            return SentrySdk.FlushAsync(timeout);
        }
    }
}
