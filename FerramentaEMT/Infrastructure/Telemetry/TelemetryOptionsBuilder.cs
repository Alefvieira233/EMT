using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// Constroi a configuracao usada pelo TelemetryReporter — super
    /// properties + scrubbing. Puro: testavel direto sem subir SDK.
    ///
    /// Espelha a estrutura de SentryOptionsBuilder (PR-3). ApplySuperProperties
    /// eh metodo estatico publico — caller injeta as 6 tags em todo evento.
    /// </summary>
    public static class TelemetryOptionsBuilder
    {
        public const string RevitVersionTag = "2025";

        /// <summary>
        /// Aplica scrubbing de PII em todas as values string do
        /// dicionario de properties. Returns dicionario novo (nao muta
        /// o input — Properties em TelemetryEvent eh IReadOnlyDictionary).
        /// </summary>
        public static IReadOnlyDictionary<string, object> ScrubProperties(
            IReadOnlyDictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return new Dictionary<string, object>();

            Dictionary<string, object> result = new Dictionary<string, object>(properties.Count);
            foreach (KeyValuePair<string, object> kv in properties)
            {
                if (kv.Value is string s)
                {
                    result[kv.Key] = PiiScrubber.Scrub(s);
                }
                else
                {
                    result[kv.Key] = kv.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Constroi as 6 super properties que vao em CADA evento.
        /// <paramref name="release"/>, <paramref name="licenseState"/> e
        /// <paramref name="sessionId"/> sao injetados pelo caller.
        /// os e culture sao resolvidos aqui (RuntimeInformation +
        /// CultureInfo.CurrentCulture).
        /// </summary>
        public static IReadOnlyDictionary<string, string> BuildSuperProperties(
            string release,
            string licenseState,
            string sessionId)
        {
            return new Dictionary<string, string>
            {
                { "version", string.IsNullOrEmpty(release) ? "unknown" : release },
                { "revit_version", RevitVersionTag },
                { "os", SafeOsDescription() },
                { "culture", SafeCultureName() },
                { "license_state", string.IsNullOrEmpty(licenseState) ? "Unknown" : licenseState },
                { "session_id", string.IsNullOrEmpty(sessionId) ? "unknown" : sessionId },
            };
        }

        /// <summary>
        /// Aplica super properties + scrubbing num evento. Equivalente
        /// ao SentryOptionsBuilder.ScrubAndTag — mesma filosofia, mesmo
        /// formato de teste.
        /// </summary>
        public static TelemetryEvent ScrubAndTag(
            TelemetryEvent evt,
            string release,
            string licenseState,
            string sessionId)
        {
            if (evt == null) return null;

            IReadOnlyDictionary<string, object> scrubbed = ScrubProperties(evt.Properties);
            Dictionary<string, object> merged = new Dictionary<string, object>(scrubbed);

            IReadOnlyDictionary<string, string> superProps =
                BuildSuperProperties(release, licenseState, sessionId);
            foreach (KeyValuePair<string, string> sp in superProps)
            {
                merged[sp.Key] = sp.Value;
            }

            return new TelemetryEvent(evt.Name, merged);
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
