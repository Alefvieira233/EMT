#nullable enable
using System;

namespace FerramentaEMT.Services.Montagem
{
    /// <summary>
    /// Parser puro (sem dependencia Revit) para extrair numero da etapa de
    /// texto no formato "Etapa:N". Extraido de PlanoMontagemService para
    /// permitir testes unitarios.
    /// </summary>
    /// <remarks>
    /// Formato esperado: "Etapa:N" onde N e um inteiro positivo.
    /// - Case-insensitive ("etapa:", "ETAPA:", "Etapa:" sao equivalentes).
    /// - Se ocorrer multiplas vezes, a primeira ocorrencia valida vence.
    /// - Digitos consecutivos apos os dois-pontos (para tolerar sufixos tipo "Etapa:5; extra").
    /// - Retorna 0 quando nao encontra, N <= 0, ou string invalida.
    /// </remarks>
    public static class EtapaMontagemParser
    {
        private const string Prefixo = "Etapa:";

        /// <summary>
        /// Tenta extrair o numero da etapa do texto.
        /// </summary>
        /// <param name="texto">Texto bruto (e.g. conteudo do parametro Comments).</param>
        /// <returns>Numero da etapa (positivo) ou 0 se nao encontrado/invalido.</returns>
        public static int Parse(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return 0;

            int idx = texto.IndexOf(Prefixo, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return 0;

            string resto = texto.Substring(idx + Prefixo.Length);
            int fim = 0;
            while (fim < resto.Length && char.IsDigit(resto[fim])) fim++;
            if (fim == 0) return 0;

            if (!int.TryParse(resto.Substring(0, fim), out int etapa)) return 0;
            return etapa > 0 ? etapa : 0;
        }

        /// <summary>
        /// Variante TryParse — retorna true se extraiu um numero positivo.
        /// </summary>
        public static bool TryParse(string? texto, out int etapa)
        {
            etapa = Parse(texto);
            return etapa > 0;
        }
    }
}
