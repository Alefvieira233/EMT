using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using SteelBIM.Core;
using SteelBIM.Infrastructure;
using Xunit;

namespace SteelBIM.Tests.Smoke
{
    /// <summary>
    /// Smoke tests v2.7.7 — substituem o antigo `2 + 2 == 4` que nao validava
    /// NADA do plugin (CI verde com plugin completamente quebrado era possivel).
    ///
    /// **Contexto arquitetural:** SteelBIM.Tests.csproj NAO referencia
    /// SteelBIM.csproj (que depende de RevitAPI/UI nao disponivel em CI).
    /// Em vez disso, inclui ~102 arquivos seletos via &lt;Compile Include&gt;
    /// — toda a superficie pura/testavel sem Revit (Licensing, Core,
    /// Infrastructure de Update/Sentry/Telemetry, helpers Ifc/Trelica/PF/CNC).
    ///
    /// Esses smoke tests validam que essa superficie esta intacta: types
    /// principais carregam, generic Result&lt;T&gt; resolve, PiiScrubber roda
    /// happy path. Se algum desses falha, refactor quebrou algo critico
    /// antes de virar bug em prod.
    ///
    /// Devem rodar em &lt; 100ms. Sem dependencias de IO ou Revit.
    /// </summary>
    public class SmokeTests
    {
        private static readonly Assembly TestAssembly = typeof(SmokeTests).Assembly;

        [Fact]
        public void TestAssembly_Loads_HasVersion()
        {
            TestAssembly.Should().NotBeNull("typeof(SmokeTests).Assembly e' o proprio test DLL");

            Version version = TestAssembly.GetName().Version;
            version.Should().NotBeNull("assembly version configurada via xUnit/SDK default");
        }

        [Fact]
        public void TestAssembly_Includes_SignificantNumberOfSteelBIMTypes()
        {
            // SteelBIM.Tests.csproj inclui ~102 arquivos via Compile Include de
            // ..\SteelBIM\. Cada arquivo pode definir 1+ tipo. Conta tipos
            // publicos no namespace SteelBIM.* (exclui SteelBIM.Tests.*).
            //
            // Threshold conservador: 80. Se cair abaixo, alguem removeu Compile
            // Includes em massa sem revisar (provavel acidente em refactor de
            // csproj).
            int steelBimTypeCount = TestAssembly.GetTypes()
                .Where(t => t.IsPublic
                    && t.Namespace != null
                    && t.Namespace.StartsWith("SteelBIM.", StringComparison.Ordinal)
                    && !t.Namespace.StartsWith("SteelBIM.Tests", StringComparison.Ordinal))
                .Count();

            steelBimTypeCount.Should().BeGreaterThanOrEqualTo(
                80,
                $"v2.7.6 tem ~102 Compile Includes resultando em 80+ tipos publicos; contagem atual: {steelBimTypeCount}");
        }

        [Theory]
        // Licensing — boot dependency
        [InlineData("SteelBIM.Licensing.LicenseSecretProvider")]
        [InlineData("SteelBIM.Licensing.KeySigner")]
        [InlineData("SteelBIM.Licensing.LicensePayload")]
        // Core — pattern ADR-001/004
        [InlineData("SteelBIM.Core.ProgressReport")]
        [InlineData("SteelBIM.Core.ProgressReporter")]
        // Infrastructure — observability (Sentry/PostHog/Update)
        [InlineData("SteelBIM.Infrastructure.PiiScrubber")]
        [InlineData("SteelBIM.Infrastructure.CrashReporting.SentryReporter")]
        [InlineData("SteelBIM.Infrastructure.CrashReporting.SentryOptionsBuilder")]
        [InlineData("SteelBIM.Infrastructure.Telemetry.PostHogHttpTelemetryClient")]
        [InlineData("SteelBIM.Infrastructure.Telemetry.TelemetryReporter")]
        [InlineData("SteelBIM.Infrastructure.Update.UpdateCheckService")]
        [InlineData("SteelBIM.Infrastructure.Update.UpdateApplier")]
        [InlineData("SteelBIM.Infrastructure.Update.ZipSlipValidator")]
        // Privacy — LGPD consent gating
        [InlineData("SteelBIM.Models.Privacy.PrivacySettings")]
        [InlineData("SteelBIM.Models.Privacy.ConsentState")]
        // Conversor IFC (v2.7.x flagship feature)
        [InlineData("SteelBIM.Services.Ifc.IfcMaterialParser")]
        [InlineData("SteelBIM.Services.Ifc.SectionOrientationExtractor")]
        [InlineData("SteelBIM.Services.Ifc.IfcStructuralFilterPure")]
        // PF (modulo critico)
        [InlineData("SteelBIM.Services.PF.PfNbr6118AnchorageService")]
        [InlineData("SteelBIM.Services.PF.PfStirrupHookRules")]
        [InlineData("SteelBIM.Services.PF.PfNamingFormatter")]
        // CNC export
        [InlineData("SteelBIM.Services.CncExport.DstvFileWriter")]
        [InlineData("SteelBIM.Services.CncExport.DstvProfileMapper")]
        // Trelica
        [InlineData("SteelBIM.Services.Trelica.TrelicaGeometria")]
        [InlineData("SteelBIM.Services.Trelica.CotarTrelicaReport")]
        // Conexoes
        [InlineData("SteelBIM.Services.Conexoes.ConexaoCalculator")]
        public void CriticalType_IsPresent_InTestCompilation(string fullName)
        {
            Type type = TestAssembly.GetType(fullName);
            type.Should().NotBeNull(
                $"{fullName} esta na Compile Include list de SteelBIM.Tests.csproj. " +
                $"Se nulo: arquivo renomeado, deletado, ou removido do csproj — refactor quebrou contrato.");
        }

        [Fact]
        public void Result_GenericType_IsUsable_HappyPath()
        {
            // ADR-001 Result pattern — base de todo retorno de service.
            // API real: Result&lt;T&gt;.Ok(value) / Result&lt;T&gt;.Fail(error), property Error.
            // Smoke: criar ambos branchs, ler propriedades. Se quebrar, 100+
            // services dependentes vao quebrar tambem.
            Result<int> ok = Result<int>.Ok(42);
            ok.IsSuccess.Should().BeTrue();
            ok.Value.Should().Be(42);

            Result<int> fail = Result<int>.Fail("erro X");
            fail.IsSuccess.Should().BeFalse();
            fail.Error.Should().Be("erro X");
        }

        [Fact]
        public void PiiScrubber_Scrub_HappyPath()
        {
            // ADR-007 PII scrubbing — base de seguranca/LGPD em Sentry+PostHog.
            // Smoke: confirma que metodo eh chamavel e retorna string nao-nula
            // para input simples. Testes proprios de regex/edge cases ficam
            // em PiiScrubberTests (~50+ tests dedicados).
            string scrubbed = PiiScrubber.Scrub("input qualquer");
            scrubbed.Should().NotBeNull("Scrub() nunca retorna null mesmo pra input bizarro");
        }
    }
}
