using System.Text;
using FerramentaEMT.Licensing;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Licensing
{
    public class Base64UrlTests
    {
        [Theory]
        [InlineData("hello world")]
        [InlineData("alef@exemplo.com")]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("ab")]
        [InlineData("abc")]
        [InlineData("acentos: ção, é, ú")]
        public void Encode_then_Decode_roundtrips_utf8(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            string encoded = Base64Url.Encode(bytes);
            byte[] decoded = Base64Url.Decode(encoded);
            string back = Encoding.UTF8.GetString(decoded);
            back.Should().Be(input);
        }

        [Fact]
        public void Encode_does_not_use_plus_slash_or_equals()
        {
            // Forçar bytes que normalmente geram +, /, = no Base64 padrao
            byte[] bytes = { 0xff, 0xee, 0xdd, 0xff, 0xee, 0xdd, 0xff };
            string encoded = Base64Url.Encode(bytes);
            encoded.Should().NotContain("+");
            encoded.Should().NotContain("/");
            encoded.Should().NotContain("=");
        }
    }
}
