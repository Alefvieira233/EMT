using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Sentry;
using SteelBIM.Infrastructure;

namespace SteelBIM.Infrastructure.CrashReporting
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

            // v2.7.10 (auditoria 2026-05-25 #5.4): scrub PII em breadcrumbs.
            // Hook publico SetBeforeBreadcrumb roda quando o breadcrumb e
            // adicionado (vs BeforeSend que e no envio do evento) — mais
            // cedo, menos chance de leak via path codepath alternativo.
            // Resolve gap LGPD: breadcrumbs nao passavam pelo PiiScrubber,
            // podendo levar filename do cliente (".rvt") ou path de usuario
            // em mensagens de log que viravam breadcrumbs.
            options.SetBeforeBreadcrumb(ScrubBreadcrumb);

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
            if (evt == null)
                return null;

            // 1. Scrubbing de PII em strings que contem mensagens reais.
            ScrubMessage(evt);
            ScrubExceptionValues(evt);
            // v2.6.1 (hotfix P0 SECURITY-2): expandir scrub para mais campos
            // identificados na auditoria 2026-05-19 PHASE 6.3.
            ScrubServerName(evt);
            ScrubExceptionStacktraceFrames(evt);

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
            if (evt.Message == null)
                return;
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
            if (evt.SentryExceptions == null)
                return;
            try
            {
                foreach (Sentry.Protocol.SentryException sex in evt.SentryExceptions)
                {
                    if (sex == null)
                        continue;
                    if (sex.Value != null)
                        sex.Value = PiiScrubber.Scrub(sex.Value);
                }
            }
            catch
            {
                // Defensivo — mesma razao do ScrubMessage.
            }
        }

        /// <summary>
        /// v2.6.1 (hotfix P0 SECURITY-2): Sentry SDK default preenche
        /// evt.ServerName = Environment.MachineName mesmo com SendDefaultPii=false.
        /// Hostname (ex: 'WS-EMTBRASIL-01') identifica usuario/empresa indiretamente.
        /// Substituimos por marker constante.
        /// </summary>
        private static void ScrubServerName(SentryEvent evt)
        {
            try
            {
                // Setar pra null em alguns SDK versions pode ser rejeitado;
                // marker constante e seguro e claramente identificavel em
                // dashboards do Sentry.
                evt.ServerName = "<HOST>";
            }
            catch
            {
                // Property pode ser readonly em alguma versao futura do SDK
                // — defensivo, nao quebrar captura por causa disso.
            }
        }

        /// <summary>
        /// v2.6.1 (hotfix P0 SECURITY-2): scrubba paths de arquivo em stack
        /// frames (auditoria PHASE 6.3). AttachStacktrace=true (linha 56)
        /// faz o SDK anexar frames com AbsoluteFilePath/FileName quando
        /// PDBs estao disponiveis. Esses paths podem incluir nome do
        /// usuario (C:\Users\<x>\...) ou nome do cliente (modelo.rvt).
        /// </summary>
        private static void ScrubExceptionStacktraceFrames(SentryEvent evt)
        {
            if (evt.SentryExceptions == null)
                return;
            try
            {
                foreach (Sentry.Protocol.SentryException sex in evt.SentryExceptions)
                {
                    if (sex == null || sex.Stacktrace == null || sex.Stacktrace.Frames == null)
                        continue;
                    foreach (Sentry.SentryStackFrame frame in sex.Stacktrace.Frames)
                    {
                        if (frame == null)
                            continue;
                        if (frame.AbsolutePath != null)
                            frame.AbsolutePath = PiiScrubber.Scrub(frame.AbsolutePath);
                        if (frame.FileName != null)
                            frame.FileName = PiiScrubber.Scrub(frame.FileName);
                    }
                }
            }
            catch
            {
                // Defensivo — Sentry.SentryStackFrame API surface pode variar.
            }
        }

        /// <summary>
        /// v2.7.10 (auditoria 2026-05-25 #5.4): scrub PII em breadcrumbs
        /// individuais. Hook registrado em <see cref="Build"/> via
        /// SetBeforeBreadcrumb — roda no momento da adicao do breadcrumb.
        ///
        /// Breadcrumb e imutavel em Sentry SDK 5.x (Message/Data sao init-only,
        /// Type/Category/Level/Timestamp sao read-only). Por isso retornamos
        /// uma NOVA instancia construida via ctor publico, preservando
        /// type/category/level/data-keys mas scrubbing Message + values de Data.
        ///
        /// Trade-off: o ctor publico nao aceita timestamp, entao o novo
        /// breadcrumb tem timestamp = "agora". Como o hook roda no momento
        /// da adicao (~ms apos a criacao do original), a perda de precisao
        /// e sub-segundo — irrelevante para ordering cronologico em crash report.
        ///
        /// Retorna null se input for null (defensivo). Nunca filtra (sempre
        /// retorna instancia, nao descarta breadcrumbs).
        /// </summary>
        public static Breadcrumb ScrubBreadcrumb(Breadcrumb original)
        {
            if (original == null)
                return null;

            try
            {
                string scrubbedMessage = PiiScrubber.Scrub(original.Message);
                IReadOnlyDictionary<string, string> scrubbedData = ScrubBreadcrumbData(original.Data);

                return new Breadcrumb(
                    message: scrubbedMessage,
                    type: original.Type,
                    data: scrubbedData,
                    category: original.Category,
                    level: original.Level);
            }
            catch
            {
                // Sentry SDK pode mudar API entre patches. Falha aqui nao
                // deve quebrar a captura do crash — retorna original
                // (PII pode passar, mas crash report continua chegando).
                return original;
            }
        }

        /// <summary>
        /// Helper para scrub do dicionario Data do breadcrumb. Aplica
        /// PiiScrubber em cada value (keys sao identificadores, nao PII).
        /// Null/vazio -> null/vazio (nao aloca dicionario novo).
        /// </summary>
        private static IReadOnlyDictionary<string, string> ScrubBreadcrumbData(IReadOnlyDictionary<string, string> data)
        {
            if (data == null || data.Count == 0)
                return data;

            Dictionary<string, string> scrubbed = new Dictionary<string, string>(data.Count);
            foreach (KeyValuePair<string, string> kv in data)
            {
                scrubbed[kv.Key] = PiiScrubber.Scrub(kv.Value);
            }
            return scrubbed;
        }

        private static string ResolveLicenseStateSafely(Func<string> provider)
        {
            if (provider == null)
                return "Unknown";
            try
            { return provider() ?? "Unknown"; }
            catch { return "Unknown"; }
        }

        private static string SafeOsDescription()
        {
            try
            { return RuntimeInformation.OSDescription; }
            catch { return "unknown"; }
        }

        private static string SafeCultureName()
        {
            try
            { return CultureInfo.CurrentCulture.Name; }
            catch { return "unknown"; }
        }
    }
}
