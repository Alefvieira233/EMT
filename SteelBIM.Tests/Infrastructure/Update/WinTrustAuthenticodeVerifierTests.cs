using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using SteelBIM.Infrastructure.Update;
using Xunit;

namespace SteelBIM.Tests.Infrastructure.Update
{
    /// <summary>
    /// v2.7.10 (auditoria 2026-05-25 §5.3) — F4 tests do verifier WinTrust.
    ///
    /// Estrategia:
    /// - Comportamento de input invalido (null, vazio, arquivo inexistente) testado direto.
    /// - Mapeamento de HResults testado via metodo internal estatico (sem dependencia de OS).
    /// - End-to-end com WinVerifyTrust real: usa um DLL sem assinatura (xUnit.runner.console
    ///   ou o proprio SteelBIM.dll de Debug — Debug nao eh assinado pelo signtool).
    ///   Em Windows espera NoSignature; em Linux CI (sem wintrust.dll) espera NotSupported.
    ///   Ambos sao validos pra "nao Verified" — assertamos lista de aceitos.
    /// </summary>
    public class WinTrustAuthenticodeVerifierTests
    {
        [Fact]
        public void Null_path_retorna_Error()
        {
            WinTrustAuthenticodeVerifier verifier = new WinTrustAuthenticodeVerifier();
            verifier.VerifyFile(null).Should().Be(AuthenticodeVerifyResult.Error);
        }

        [Fact]
        public void Empty_path_retorna_Error()
        {
            WinTrustAuthenticodeVerifier verifier = new WinTrustAuthenticodeVerifier();
            verifier.VerifyFile(string.Empty).Should().Be(AuthenticodeVerifyResult.Error);
        }

        [Fact]
        public void Whitespace_path_retorna_Error()
        {
            WinTrustAuthenticodeVerifier verifier = new WinTrustAuthenticodeVerifier();
            verifier.VerifyFile("   ").Should().Be(AuthenticodeVerifyResult.Error);
        }

        [Fact]
        public void Arquivo_inexistente_retorna_Error()
        {
            WinTrustAuthenticodeVerifier verifier = new WinTrustAuthenticodeVerifier();
            string ghost = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".dll");
            verifier.VerifyFile(ghost).Should().Be(AuthenticodeVerifyResult.Error);
        }

        [Fact]
        public void Dll_nao_assinado_retorna_NoSignature_ou_NotSupported()
        {
            // Pega o caminho do proprio SteelBIM.dll referenciado pelos testes
            // — Debug nao eh assinado pelo signtool, entao deve retornar
            // NoSignature em Windows. Em Linux CI (sem wintrust.dll), retorna
            // NotSupported. Ambos sao "nao-Verified" — o que prova que o
            // verifier nao acidentalmente fala "Verified" pra qualquer coisa.
            string dllPath = typeof(WinTrustAuthenticodeVerifier).Assembly.Location;
            File.Exists(dllPath).Should().BeTrue("o DLL do plugin deve existir pra teste rodar");

            WinTrustAuthenticodeVerifier verifier = new WinTrustAuthenticodeVerifier();
            AuthenticodeVerifyResult result = verifier.VerifyFile(dllPath);

            // Verified seria FALSA POSITIVA. Qualquer outro estado eh aceitavel.
            result.Should().NotBe(AuthenticodeVerifyResult.Verified);
        }

        // ---------- MapHResult — exercita o switch sem precisar de Win32 ----------

        [Theory]
        [InlineData(0x00000000u, AuthenticodeVerifyResult.Verified)]
        [InlineData(0x800B0100u, AuthenticodeVerifyResult.NoSignature)]
        [InlineData(0x800B0001u, AuthenticodeVerifyResult.NoSignature)]
        [InlineData(0x800B0003u, AuthenticodeVerifyResult.NoSignature)]
        [InlineData(0x800B0004u, AuthenticodeVerifyResult.UntrustedRoot)]
        [InlineData(0x800B0109u, AuthenticodeVerifyResult.UntrustedRoot)]
        [InlineData(0x800B010Au, AuthenticodeVerifyResult.UntrustedRoot)]
        [InlineData(0x800B010Cu, AuthenticodeVerifyResult.UntrustedRoot)]
        [InlineData(0x800B0101u, AuthenticodeVerifyResult.Expired)]
        [InlineData(0x80096010u, AuthenticodeVerifyResult.SignatureBroken)]
        [InlineData(0xDEADBEEFu, AuthenticodeVerifyResult.Error)]
        public void MapHResult_cobre_codigos_documentados(uint hr, AuthenticodeVerifyResult expected)
        {
            // Reflection porque MapHResult eh internal — testes precisam vasculhar
            // os HResults conhecidos sem expor o switch como API publica.
            MethodInfo map = typeof(WinTrustAuthenticodeVerifier).GetMethod(
                "MapHResult",
                BindingFlags.NonPublic | BindingFlags.Static);
            map.Should().NotBeNull("MapHResult deve existir como internal static");

            AuthenticodeVerifyResult actual = (AuthenticodeVerifyResult)map.Invoke(null, new object[] { hr });
            actual.Should().Be(expected);
        }
    }
}
