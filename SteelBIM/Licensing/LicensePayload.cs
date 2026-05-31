using System;

namespace SteelBIM.Licensing
{
    /// <summary>
    /// Conteudo de uma chave de licenca (parte "publica" — vai serializada no arquivo
    /// e tambem no campo "key" que o ALEF entrega para o cliente).
    /// A assinatura ASSIMETRICA (ECDsa P-256) garante que so o produtor (que tem a chave
    /// PRIVADA) pode emitir; o plugin so verifica com a chave PUBLICA embarcada. Ver ADR-011.
    /// </summary>
    /// <remarks>
    /// Formato JSON minimo (curto para o usuario digitar/colar):
    /// { "e": "alef@x.com", "i": 1729900800, "x": 1761436800, "v": 1 }
    /// </remarks>
    public sealed class LicensePayload
    {
        /// <summary>Email do cliente (identificador "humano").</summary>
        public string Email { get; set; }

        /// <summary>Quando a licenca foi emitida (Unix timestamp UTC).</summary>
        public long IssuedAtUnix { get; set; }

        /// <summary>Quando a licenca expira (Unix timestamp UTC).</summary>
        public long ExpiresAtUnix { get; set; }

        /// <summary>Versao do schema (1 = inicial). Permite evoluir sem quebrar parses antigos.</summary>
        public int Version { get; set; } = 1;

        public DateTime IssuedAtUtc => DateTimeOffset.FromUnixTimeSeconds(IssuedAtUnix).UtcDateTime;
        public DateTime ExpiresAtUtc => DateTimeOffset.FromUnixTimeSeconds(ExpiresAtUnix).UtcDateTime;

        public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

        public int DiasRestantes(DateTime nowUtc)
        {
            TimeSpan delta = ExpiresAtUtc - nowUtc;
            return delta.TotalDays > 0 ? (int)Math.Ceiling(delta.TotalDays) : 0;
        }
    }
}
