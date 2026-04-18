#nullable enable
using System.Globalization;
using System.Threading;
using FerramentaEMT.Services.PF;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.PF
{
    public class PfNamingFormatterTests
    {
        [Theory]
        [InlineData("P", 1, "", "P1")]
        [InlineData("P-", 10, "", "P-10")]
        [InlineData("V", 5, "-A", "V5-A")]
        [InlineData("", 42, "", "42")]
        [InlineData("", 0, "", "0")]
        [InlineData("L-", -3, "", "L--3")]
        public void Formatar_CasosBasicos(string prefixo, int numero, string sufixo, string esperado)
        {
            PfNamingFormatter.Formatar(prefixo, numero, sufixo).Should().Be(esperado);
        }

        [Fact]
        public void Formatar_PrefixoNulo_TratadoComoVazio()
        {
            PfNamingFormatter.Formatar(null!, 7, "x").Should().Be("7x");
        }

        [Fact]
        public void Formatar_SufixoNulo_TratadoComoVazio()
        {
            PfNamingFormatter.Formatar("P", 7, null!).Should().Be("P7");
        }

        [Fact]
        public void Formatar_AmbosNulos_SoNumero()
        {
            PfNamingFormatter.Formatar(null!, 13, null!).Should().Be("13");
        }

        [Theory]
        [InlineData("pt-BR")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        [InlineData("en-US")]
        public void Formatar_CultureInvariant_SemSeparadorDeMilhar(string culture)
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                // 1000 em pt-BR/de-DE com formatacao padrao seria "1.000" — nao pode acontecer aqui.
                PfNamingFormatter.Formatar("P", 1000, "").Should().Be("P1000");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void Formatar_NumeroGrande_SemExponencial()
        {
            PfNamingFormatter.Formatar("", 999_999_999, "").Should().Be("999999999");
        }
    }
}
