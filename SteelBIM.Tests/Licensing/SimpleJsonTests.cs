using System;
using FluentAssertions;
using SteelBIM.Licensing;
using Xunit;

namespace SteelBIM.Tests.Licensing
{
    /// <summary>
    /// Cobre o serializador/desserializador do payload de licenca. Critico: o formato
    /// precisa ser DETERMINISTICO (mesmos bytes para os mesmos dados) para a assinatura
    /// sempre bater, e o round-trip precisa preservar todos os campos.
    /// </summary>
    public class SimpleJsonTests
    {
        private static LicensePayload P(
            string email = "alef@x.com", long i = 1729900800, long x = 1761436800, int v = 1)
            => new LicensePayload { Email = email, IssuedAtUnix = i, ExpiresAtUnix = x, Version = v };

        [Fact]
        public void Serialize_produz_formato_fixo_com_ordem_estavel()
        {
            SimpleJson.Serialize(P()).Should()
                .Be("{\"e\":\"alef@x.com\",\"i\":1729900800,\"x\":1761436800,\"v\":1}");
        }

        [Fact]
        public void Serialize_eh_deterministico()
        {
            // Invariante que sustenta a assinatura: o mesmo payload sempre gera os mesmos bytes.
            var p = P();
            SimpleJson.Serialize(p).Should().Be(SimpleJson.Serialize(p));
        }

        [Fact]
        public void RoundTrip_preserva_todos_os_campos()
        {
            var orig = P(email: "joao+teste@empresa.com.br", i: 100, x: 999999, v: 3);
            var back = SimpleJson.Deserialize(SimpleJson.Serialize(orig));

            back.Email.Should().Be(orig.Email);
            back.IssuedAtUnix.Should().Be(orig.IssuedAtUnix);
            back.ExpiresAtUnix.Should().Be(orig.ExpiresAtUnix);
            back.Version.Should().Be(orig.Version);
        }

        [Theory]
        [InlineData("a\"b")]   // aspas
        [InlineData("a\\b")]   // barra invertida
        [InlineData("a\nb")]   // newline
        [InlineData("a\tb")]   // tab
        [InlineData("a\rb")]   // carriage return
        public void RoundTrip_escapa_caracteres_especiais_no_email(string email)
        {
            var back = SimpleJson.Deserialize(SimpleJson.Serialize(P(email: email)));
            back.Email.Should().Be(email);
        }

        [Fact]
        public void Serialize_email_null_vira_string_vazia()
        {
            SimpleJson.Serialize(P(email: null)).Should().Contain("\"e\":\"\"");
        }

        [Fact]
        public void Serialize_lanca_em_payload_null()
        {
            Action act = () => SimpleJson.Serialize(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Deserialize_aceita_espacos_em_branco()
        {
            var p = SimpleJson.Deserialize("{ \"e\" : \"x@y.z\" , \"i\" : 1 , \"x\" : 2 , \"v\" : 1 }");
            p.Email.Should().Be("x@y.z");
            p.IssuedAtUnix.Should().Be(1);
            p.ExpiresAtUnix.Should().Be(2);
            p.Version.Should().Be(1);
        }

        [Fact]
        public void Deserialize_ignora_chave_desconhecida()
        {
            var p = SimpleJson.Deserialize("{\"e\":\"a@b.c\",\"extra\":\"zzz\",\"i\":5,\"x\":6,\"v\":2}");
            p.Email.Should().Be("a@b.c");
            p.IssuedAtUnix.Should().Be(5);
            p.Version.Should().Be(2);
        }

        [Fact]
        public void Deserialize_aceita_numero_negativo()
        {
            SimpleJson.Deserialize("{\"e\":\"a@b\",\"i\":-10,\"x\":-5,\"v\":1}")
                .IssuedAtUnix.Should().Be(-10);
        }

        [Fact]
        public void Deserialize_vazio_ou_null_lanca()
        {
            ((Action)(() => SimpleJson.Deserialize(""))).Should().Throw<ArgumentException>();
            ((Action)(() => SimpleJson.Deserialize(null))).Should().Throw<ArgumentException>();
        }
    }
}
