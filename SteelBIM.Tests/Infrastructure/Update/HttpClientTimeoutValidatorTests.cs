using System;
using System.Net.Http;
using SteelBIM.Infrastructure.Update;
using Xunit;

namespace SteelBIM.Tests.Infrastructure.Update
{
    public class HttpClientTimeoutValidatorTests
    {
        [Fact]
        public void Validate_Timeout30s_Aceita()
        {
            // 30s e o valor recomendado na auditoria — passa sem erro.
            HttpClientTimeoutValidator.Validate(TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void Validate_Timeout60s_Aceita_LimiteSuperior()
        {
            // Exatamente no limite superior — aceito.
            HttpClientTimeoutValidator.Validate(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public void Validate_Timeout5s_Aceita_LimiteInferiorPragmatico()
        {
            HttpClientTimeoutValidator.Validate(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Validate_TimeoutInfinito_LancaArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                HttpClientTimeoutValidator.Validate(System.Threading.Timeout.InfiniteTimeSpan));
            Assert.Contains("Infinite", ex.Message);
        }

        [Fact]
        public void Validate_TimeoutZero_LancaArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                HttpClientTimeoutValidator.Validate(TimeSpan.Zero));
            Assert.Contains("> 0", ex.Message);
        }

        [Fact]
        public void Validate_TimeoutNegativo_LancaArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                HttpClientTimeoutValidator.Validate(TimeSpan.FromSeconds(-5)));
        }

        [Fact]
        public void Validate_TimeoutAlemDoMaximo_LancaArgumentException()
        {
            // 61s — 1 segundo acima do limite — rejeitado.
            var ex = Assert.Throws<ArgumentException>(() =>
                HttpClientTimeoutValidator.Validate(TimeSpan.FromSeconds(61)));
            Assert.Contains("60s", ex.Message);
        }

        [Fact]
        public void Validate_TimeoutDefault100s_LancaArgumentException()
        {
            // Default do HttpClient e 100s — exatamente o caso que motivou
            // este fix. Caller que cria `new HttpClient()` sem setar Timeout
            // DEVE receber erro.
            var clientDefault = new HttpClient();
            try
            {
                Assert.Equal(TimeSpan.FromSeconds(100), clientDefault.Timeout); // confirma a expectativa
                Assert.Throws<ArgumentException>(() => HttpClientTimeoutValidator.Validate(clientDefault));
            }
            finally
            {
                clientDefault.Dispose();
            }
        }

        [Fact]
        public void Validate_Null_LancaArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                HttpClientTimeoutValidator.Validate((HttpClient)null));
        }

        [Fact]
        public void Validate_ParamNameCustomizado_AparecseNaException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                HttpClientTimeoutValidator.Validate(TimeSpan.Zero, paramName: "minhaInstancia"));
            Assert.Equal("minhaInstancia", ex.ParamName);
        }

        // ============================================================
        // Regression guard P0 SECURITY-1 (v2.6.1)
        // ============================================================

        [Fact]
        public void Validate_NaoRegredeV261_SECURITY1_DefaultHttpClientRejeitado()
        {
            // Guard P0 SECURITY-1: o bug original era que UpdateDownloader
            // aceitava qualquer HttpClient — incluindo o default com
            // Timeout=100s. Sob ataque slowloris ou rede ruim, download de
            // 50MB congelaria a UI do Revit por minutos.
            //
            // Este teste assegura que mesmo quem cria HttpClient sem
            // configuracao alguma sera bloqueado pelo validator.
            using (var c = new HttpClient())
            {
                Assert.Throws<ArgumentException>(() => HttpClientTimeoutValidator.Validate(c));
            }
        }

        [Fact]
        public void MaxAllowed_NaoRegredeV261_SECURITY1_E60Segundos()
        {
            // Guard: se alguem mudar MaxAllowed pra um valor diferente sem
            // discussao, este teste pega. 60s e o valor decidido na
            // auditoria — qualquer aumento precisa de revisao de seguranca.
            Assert.Equal(TimeSpan.FromSeconds(60), HttpClientTimeoutValidator.MaxAllowed);
        }
    }
}
