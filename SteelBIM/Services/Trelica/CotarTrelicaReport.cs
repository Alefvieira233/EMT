#nullable enable
using System;
using System.Collections.Generic;

namespace FerramentaEMT.Services.Trelica
{
    /// <summary>
    /// Record imutavel com os resultados da execucao de CotarTrelicaService.
    /// Usado para relatar ao usuario o que foi criado (cotas, tags, textos, tempo, avisos).
    /// </summary>
    public sealed record CotarTrelicaReport(
        int CotasCriadas,
        int TagsCriadas,
        int TextosCriados,
        int WarningsCount,
        IReadOnlyList<string> Warnings,
        long TempoMs)
    {
        /// <summary>
        /// Retorna um resumo em texto, pronto para exibir ao usuario.
        /// Inclui contagens e avisos (se houver).
        /// </summary>
        public string Resumo
        {
            get
            {
                var msg = $"Cotas criadas:        {CotasCriadas}\n" +
                          $"Tags de perfil:       {TagsCriadas}\n" +
                          $"Textos de banzo:      {TextosCriados}\n" +
                          $"Tempo de execucao:    {TempoMs} ms";

                if (WarningsCount > 0)
                {
                    msg += $"\n\nAvisos ({WarningsCount}):\n";
                    foreach (var w in Warnings)
                        msg += $"• {w}\n";
                }

                return msg;
            }
        }
    }
}
