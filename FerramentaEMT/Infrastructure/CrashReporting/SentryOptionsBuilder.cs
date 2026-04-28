using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Sentry;

namespace FerramentaEMT.Infrastructure.CrashReporting
{
    /// <summary>
    /// Constroi as <see cref="SentryOptions"/> usadas pelo SentryReporter.
    /// Puro: sem deps de Revit, sem rede, sem Logger. Toda a logica de
    /// scrubbing + tags vive em <see cref="ScrubAndTag"/> — um metodo
    /// estatico testavel direto, que eh o mesmo registrado como
    /// BeforeSend hook em <see cref="Build"/>.
    ///
    /// Sample rates documentados (ADR-007 §Sampling rationale):
    ///   - SampleRate (errors): 1.0 — capturamos 100% dos crashes.
    ///   - TracesSampleRate: 0.0 — performance/transactions desligado.
    ///   - ProfilesSampleRate: 0.0 — profiling desligado.
    ///   - MaxBreadcrumbs: 30 — reducao do default 100 (10x menos
    ///     volume; nao precisamos de breadcrumb log para diagnosticar
    ///     crashes WPF, e o plano free do Sentry tem cota de eventos).
    /// </summary>
    public static class SentryOptionsBuilder
    {
        public const float ErrorsSampleRate = 1.0f;
        public const double TracesSampleRate = 0.0;
        public const double ProfilesSampleRate = 0.0;
        public const int MaxBreadcrumbs = 30;
        public const string RevitVersionTag = "2025";

        /// <summary>
        /// Constroi as options. <paramref name="dsn"/> deve ser nao-vazio
        /// (caller eh responsavel por nao chamar quando vazio).
        /// <paramref name="release"/> eh o AssemblyInformationalVersion.
        /// <paramref name="licenseStateProvider"/> eh chamado em CADA
        /// evento (lazy) — isso garante que a tag reflita o estado
        /// corrente da licenca, nao o de boot. Pode ser null (vira
        /// "Unknown").
        /// </summary>
        public static SentryOptions Build(
            string dsn,
            string release,
            Func<string> licenseStateProvider)
        {
            SentryOptions options = new SentryOptions
            {
                Dsn = dsn,
                Release = release,
                SampleRate = ErrorsSampleRate,
                TracesSampleRate = TracesSampleRate,
                ProfilesSampleRate = ProfilesSampleRate,
                MaxBreadcrumbs = MaxBreadcrumbs,
                AutoSessionTracking = false,
                IsGlobalModeEnabled = false,
                AttachStacktrace = true,
                SendDefaultPii = false,
            };

            options.SetBeforeSend(evt =>
            {
                string licenseState = ResolveLicenseStateSafely(licenseStateProvider);
                return ScrubAndTag(evt, release, licenseState);
            });

            return options;
        }

        /// <summary>
        /// Aplica scrubbing de PII e injeta as 5 tags padronizadas no
        /// evento. Mesma logica que o BeforeSend hook — exposta como
        /// metodo publico para testar diretamente sem precisar extrair
        /// o delegate de SentryOptions.
        /// </summary>
        public static SentryEvent ScrubAndTag(
            SentryEvent evt,
            string release,
            string licenseState)
        {
            if (evt == null) return null;

            // 1. Scrubbing de PII em strings que contem mensagens reais.
            ScrubMessage(evt);
            ScrubExceptionValues(evt);

            // 2. Tags padronizadas. Se ja vierem do scope (smoke test pode
            //    setar 'kind' antes), SetTag sobrescreve — esperado.
            evt.SetTag("version", release ?? "unknown");
            evt.SetTag("revit_version", RevitVersionTag);
            evt.SetTag("os", SafeOsDescription());
            evt.SetTag("culture", SafeCultureName());
            evt.SetTag("license_state", string.IsNullOrEmpty(licenseState) ? "Unknown" : licenseState);

            return evt;
        }

        private static void ScrubMessage(SentryEvent evt)
        {
            if (evt.Message == null) return;
            try
            {
                if (evt.Message.Message != null)
                    evt.Message.Message = PiiScrubber.Scrub(evt.Message.Message);
                if (evt.Message.Formatted != null)
                    evt.Message.Formatted = PiiScrubber.Scrub(evt.Message.Formatted);
            }
            catch
            {
                // SentryMessage pode mudar API entre patches do SDK.
                // Falha aqui nao deve derrubar a captura inteira.
            }
        }

        private static void ScrubExceptionValues(SentryEvent evt)
        {
            if (evt.SentryExceptions == null) return;
            try
            {
                foreach (Sentry.Protocol.SentryException sex in evt.SentryExceptions)
                {
                    if (sex == null) continue;
                    if (sex.Value != null)
                        sex.Value = PiiScrubber.Scrub(sex.Value);
                }
            }
            catch
            {
                // Defensivo — mesma razao do ScrubMessage.
            }
        }

        private static string ResolveLicenseStateSafely(Func<string> provider)
        {
            if (provider == null) return "Unknown";
            try { return provider() ?? "Unknown"; }
            catch { return "Unknown"; }
        }

        private static string SafeOsDescription()
        {
            try { return RuntimeInformation.OSDescription; }
            catch { return "unknown"; }
        }

        private static string SafeCultureName()
        {
            try { return CultureInfo.CurrentCulture.Name; }
            catch { return "unknown"; }
        }
    }
}
