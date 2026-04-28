using FerramentaEMT.Infrastructure.Update;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure.Update
{
    public class SemVerComparerTests
    {
        // ---------- TryParse: aceita ----------

        [Theory]
        [InlineData("1.6.0", 1, 6, 0, "")]
        [InlineData("v1.6.0", 1, 6, 0, "")]
        [InlineData("V1.6.0", 1, 6, 0, "")]
        [InlineData("0.0.1", 0, 0, 1, "")]
        [InlineData("10.20.30", 10, 20, 30, "")]
        [InlineData("1.7.0-rc.1", 1, 7, 0, "rc.1")]
        [InlineData("v1.7.0-beta", 1, 7, 0, "beta")]
        [InlineData("2.0", 2, 0, 0, "")]
        [InlineData("2", 2, 0, 0, "")]
        public void TryParse_aceita_formatos_validos(string raw, int major, int minor, int patch, string preRelease)
        {
            bool ok = SemVerComparer.TryParse(raw, out SemVer v);
            ok.Should().BeTrue();
            v.Major.Should().Be(major);
            v.Minor.Should().Be(minor);
            v.Patch.Should().Be(patch);
            v.PreRelease.Should().Be(preRelease);
        }

        // ---------- TryParse: rejeita ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("latest")]
        [InlineData("abc")]
        [InlineData("1.2.3.4")]      // 4 partes
        [InlineData("1.a.0")]
        [InlineData("v")]
        [InlineData("-1.0.0")]
        [InlineData("1.-2.0")]
        public void TryParse_rejeita_formatos_invalidos(string raw)
        {
            bool ok = SemVerComparer.TryParse(raw, out SemVer _);
            ok.Should().BeFalse();
        }

        // ---------- Compare: ordens basicas ----------

        [Fact]
        public void Compare_versao_menor_retorna_negativo()
        {
            SemVerComparer.TryParse("1.6.0", out SemVer a);
            SemVerComparer.TryParse("1.7.0", out SemVer b);
            SemVerComparer.Compare(a, b).Should().BeLessThan(0);
        }

        [Fact]
        public void Compare_versao_maior_retorna_positivo()
        {
            SemVerComparer.TryParse("2.0.0", out SemVer a);
            SemVerComparer.TryParse("1.99.99", out SemVer b);
            SemVerComparer.Compare(a, b).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Compare_versoes_iguais_retorna_zero()
        {
            SemVerComparer.TryParse("1.7.0", out SemVer a);
            SemVerComparer.TryParse("v1.7.0", out SemVer b);
            SemVerComparer.Compare(a, b).Should().Be(0);
        }

        // ---------- Compare: pre-release ----------

        [Fact]
        public void Compare_prerelease_eh_menor_que_release_final()
        {
            SemVerComparer.TryParse("1.7.0-rc.1", out SemVer pre);
            SemVerComparer.TryParse("1.7.0", out SemVer rel);
            SemVerComparer.Compare(pre, rel).Should().BeLessThan(0);
        }

        [Fact]
        public void Compare_prereleases_ordem_lexicografica()
        {
            SemVerComparer.TryParse("1.7.0-alpha", out SemVer a);
            SemVerComparer.TryParse("1.7.0-beta", out SemVer b);
            SemVerComparer.Compare(a, b).Should().BeLessThan(0);
        }

        // ---------- CompareStrings: convenience ----------

        [Theory]
        [InlineData("1.6.0", "1.7.0", -1)]
        [InlineData("1.7.0", "1.6.0", 1)]
        [InlineData("v1.7.0", "1.7.0", 0)]
        public void CompareStrings_retorna_resultado_correto_quando_ambos_validos(string a, string b, int expectedSign)
        {
            int? result = SemVerComparer.CompareStrings(a, b);
            result.Should().NotBeNull();
            System.Math.Sign(result.Value).Should().Be(expectedSign);
        }

        [Theory]
        [InlineData("invalid", "1.0.0")]
        [InlineData("1.0.0", "invalid")]
        [InlineData("invalid", "invalid")]
        public void CompareStrings_retorna_null_quando_qualquer_lado_invalido(string a, string b)
        {
            SemVerComparer.CompareStrings(a, b).Should().BeNull();
        }

        // ---------- ToString ----------

        [Fact]
        public void ToString_formato_canonico_sem_prerelease()
        {
            SemVer v = new SemVer(1, 7, 0, "");
            v.ToString().Should().Be("1.7.0");
        }

        [Fact]
        public void ToString_formato_canonico_com_prerelease()
        {
            SemVer v = new SemVer(1, 7, 0, "rc.1");
            v.ToString().Should().Be("1.7.0-rc.1");
        }

        // ---------- Equals/GetHashCode ----------

        [Fact]
        public void Equals_versoes_iguais_retorna_true()
        {
            SemVer a = new SemVer(1, 7, 0, "");
            SemVer b = new SemVer(1, 7, 0, "");
            a.Equals(b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Equals_prerelease_diferente_retorna_false()
        {
            SemVer a = new SemVer(1, 7, 0, "rc.1");
            SemVer b = new SemVer(1, 7, 0, "rc.2");
            a.Equals(b).Should().BeFalse();
        }
    }
}
