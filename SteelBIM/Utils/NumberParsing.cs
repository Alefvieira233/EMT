#nullable enable
using System.Globalization;

namespace FerramentaEMT.Utils
{
    /// <summary>
    /// Helpers para leitura de numeros a partir de <see cref="System.Windows.Controls.TextBox"/>
    /// vindos do usuario, onde o separador decimal pode vir como ponto (InvariantCulture, ex:
    /// "1.5") ou virgula (pt-BR, ex: "1,5"), dependendo das preferencias do sistema e de como
    /// o usuario digitou.
    ///
    /// A regra de ouro dos WPF do FerramentaEMT e: primeiro tentar <see cref="CultureInfo.InvariantCulture"/>
    /// (ponto), e so em caso de falha cair para <c>pt-BR</c> (virgula). Esta ordem evita
    /// ambiguidade de strings como "1,000" — em Invariant isso vira 1000 em grupo de milhar,
    /// o que quase nunca e o que o usuario quis; em pt-BR vira 1,0, que e o valor esperado.
    /// Nao usamos <see cref="CultureInfo.CurrentCulture"/> porque ele depende da config do
    /// Windows do usuario — em um PC pt-BR, "1.5" digitado deliberadamente quebra.
    ///
    /// Este helper centraliza a logica para todas as janelas (Tercas, Travamento, Escada,
    /// Guarda-Corpo, Trelica, etc.) em vez de cada code-behind ter sua propria implementacao.
    /// </summary>
    public static class NumberParsing
    {
        private static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        /// <summary>
        /// Tenta parsear <paramref name="texto"/> como double, aceitando tanto ponto
        /// (InvariantCulture) quanto virgula (pt-BR) como separador decimal.
        /// </summary>
        public static bool TryParseDouble(string? texto, out double valor)
        {
            if (!string.IsNullOrWhiteSpace(texto) &&
                double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(texto) &&
                double.TryParse(texto, NumberStyles.Float, PtBr, out valor))
            {
                return true;
            }

            valor = 0;
            return false;
        }

        /// <summary>
        /// Parseia <paramref name="texto"/> e retorna o valor, ou <paramref name="padrao"/>
        /// caso o texto esteja vazio ou nao represente um numero valido.
        /// </summary>
        public static double ParseDoubleOrDefault(string? texto, double padrao)
        {
            return TryParseDouble(texto, out double valor) ? valor : padrao;
        }
    }
}
