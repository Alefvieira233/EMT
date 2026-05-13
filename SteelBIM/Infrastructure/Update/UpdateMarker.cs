using System;
using System.Text.Json;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Marker JSON gravado em
    /// <c>%LocalAppData%\FerramentaEMT\Updates\pending\{version}.marker</c>
    /// quando o download passa nas 6 validacoes. Sera consumido no proximo
    /// App.OnStartup pelo <see cref="UpdateApplier"/>.
    ///
    /// Pure DTO + helpers de serializacao — testavel em xUnit.
    /// </summary>
    public sealed class UpdateMarker
    {
        /// <summary>tag_name do release (ex: "v1.7.0").</summary>
        public string Version { get; set; }

        /// <summary>Caminho absoluto do .zip baixado.</summary>
        public string ZipPath { get; set; }

        /// <summary>SHA256 hex lowercase do .zip — re-validado antes do swap.</summary>
        public string Sha256 { get; set; }

        /// <summary>Quando o download foi concluido (UTC).</summary>
        public DateTime DownloadedAtUtc { get; set; }

        /// <summary>
        /// Quantas tentativas de aplicar este update ja falharam por
        /// "arquivo em uso". Aumentado a cada App.OnStartup que tentar
        /// e falhar. Limite: 3 tentativas, depois descartar.
        /// </summary>
        public int AttemptCount { get; set; }
    }

    /// <summary>
    /// Serializacao pura do <see cref="UpdateMarker"/>.
    /// </summary>
    public static class UpdateMarkerJson
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public static string Serialize(UpdateMarker marker)
        {
            if (marker == null) throw new ArgumentNullException("marker");
            return JsonSerializer.Serialize(marker, WriteOptions);
        }

        /// <summary>
        /// Desserializa. Retorna null em qualquer falha de parse — chamador
        /// deve deletar o marker corrompido e seguir.
        /// </summary>
        public static UpdateMarker DeserializeOrNull(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                UpdateMarker parsed = JsonSerializer.Deserialize<UpdateMarker>(json);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.Version)) return null;
                return parsed;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
