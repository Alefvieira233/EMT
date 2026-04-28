using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using FerramentaEMT.Models.Privacy;

namespace FerramentaEMT.Infrastructure.Privacy
{
    /// <summary>
    /// Serializacao/desserializacao pura de <see cref="PrivacySettings"/>
    /// para/de JSON. Sem dependencia de IO, Revit ou Logger — testavel em xUnit.
    ///
    /// O formato JSON eh contrato com PR-3 (Sentry) e PR-4 (PostHog), entao
    /// existe como classe propria para garantir round-trip estavel.
    /// </summary>
    public static class PrivacyJson
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Serialize(PrivacySettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            return JsonSerializer.Serialize(settings, WriteOptions);
        }

        /// <summary>
        /// Desserializa. Retorna defaults (todos Unset, ConsentVersion=0) em qualquer
        /// falha de parse — chamador nao precisa lidar com excecao de JSON.
        /// </summary>
        public static PrivacySettings DeserializeOrDefault(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PrivacySettings();
            }

            try
            {
                PrivacySettings parsed = JsonSerializer.Deserialize<PrivacySettings>(json);
                return parsed ?? new PrivacySettings();
            }
            catch (JsonException)
            {
                return new PrivacySettings();
            }
        }
    }
}
