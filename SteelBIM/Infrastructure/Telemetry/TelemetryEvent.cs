using System.Collections.Generic;

namespace FerramentaEMT.Infrastructure.Telemetry
{
    /// <summary>
    /// DTO puro de evento de telemetria. Properties eh um dictionary
    /// de pares (chave, valor) — valor pode ser string, int, bool,
    /// double, etc. PostHog SDK serializa via System.Text.Json.
    ///
    /// Whitelist de chaves permitidas (auditada via teste): nada de
    /// email, paths, machine ID, ElementId.Value, file names, username.
    /// Ver TelemetryEventTests.Properties_does_not_contain_blacklisted_keys.
    /// </summary>
    public sealed class TelemetryEvent
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, object> Properties { get; }

        public TelemetryEvent(string name, IReadOnlyDictionary<string, object> properties)
        {
            Name = name ?? string.Empty;
            Properties = properties ?? new Dictionary<string, object>();
        }
    }
}
