using System.Globalization;
using System.Threading;
using FluentAssertions;
using SteelBIM.Models.CncExport;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    public class DstvFileWriterTests
    {
        [Fact]
        public void Write_HeaderMinimo_GeraBlocoStComCamposObrigatorios()
        {
            var f = new DstvFile
            {
                OrderNumber = "PRJ-001",
                DrawingNumber = "DWG-001",
                Phase = "1",
                PieceMark = "V-001",
                SteelQuality = "ASTM A992",
                Quantity = 1,
                ProfileName = "W12X26",
                ProfileType = DstvProfileType.I,
                ProfileHeightMm = 310,
                FlangeWidthMm = 165,
                FlangeThicknessMm = 9.7,
                WebThicknessMm = 5.8,
                FilletRadiusMm = 11.4,
                WeightPerMeter = 38.7,
                CutLengthMm = 6000
            };

            string txt = DstvFileWriter.Write(f);

            txt.Should().StartWith("ST\r\n");
            txt.Should().Contain("** DWG-001.nc1\r\n");  // linha-comentario do nome do arquivo
            txt.Should().Contain("\r\n  PRJ-001\r\n");
            txt.Should().Contain("\r\n  V-001\r\n");
            txt.Should().Contain("\r\n  W12X26\r\n");
            txt.Should().Contain("\r\n  I\r\n");                      // codigo do perfil
            txt.Should().Contain("\r\n    6000.00,6000.00\r\n");      // length (coluna fixa, alma,mesa)
            txt.Should().EndWith("EN\r\n");
        }

        [Fact]
        public void Write_ST_OrdemDSTV_LengthAposCodigoEAntesDaAltura()
        {
            var f = MinimalFile(); // code "I", length 6000, height 310
            string txt = DstvFileWriter.Write(f);

            int posCode = txt.IndexOf("\r\n  I\r\n", System.StringComparison.Ordinal);
            int posLength = txt.IndexOf("\r\n    6000.00,6000.00\r\n", System.StringComparison.Ordinal);
            int posHeight = txt.IndexOf("\r\n     310.00\r\n", System.StringComparison.Ordinal);

            posCode.Should().BeGreaterThan(0);
            posLength.Should().BeGreaterThan(posCode);    // comprimento logo apos o codigo do perfil
            posHeight.Should().BeGreaterThan(posLength);  // altura depois do comprimento (ordem DSTV)
        }

        [Fact]
        public void Write_ContornoAkDesligadoPorPadrao_NaoEmiteBlocoAK()
        {
            var f = MinimalFile(); // IncluirContornoAk = false (default)
            DstvFileWriter.Write(f).Should().NotContain("AK\r\n");
        }

        [Fact]
        public void Write_ContornoAkLigado_EmiteRetanguloDaFaceV()
        {
            var f = MinimalFile(); // L=6000, h=310
            f.IncluirContornoAk = true;

            string txt = DstvFileWriter.Write(f);

            txt.Should().Contain("AK\r\n");
            // Primeiro ponto (0,0): face 'v' + marcador de inicio 'u' em colunas fixas.
            txt.Should().Contain("  v       0.00u      0.00");
            // Cantos com o comprimento L=6000 (coluna largura 11) aparecem no contorno.
            txt.Should().Contain("    6000.00");

            // AK vem depois do cabecalho ST
            int posSt = txt.IndexOf("ST\r\n", System.StringComparison.Ordinal);
            int posAk = txt.IndexOf("AK\r\n", System.StringComparison.Ordinal);
            posAk.Should().BeGreaterThan(posSt);
        }

        [Fact]
        public void Write_SemFuros_NaoEmiteBlocoBO()
        {
            var f = MinimalFile();
            string txt = DstvFileWriter.Write(f);
            txt.Should().NotContain("BO\r\n");
        }

        [Fact]
        public void Write_ComFuros_EmiteBlocoBOUnicoComCodigoDeFacePorLinha()
        {
            var f = MinimalFile();
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 100, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 200, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.TopFlange, XMm = 150, YMm = 0, DiameterMm = 18 });

            string txt = DstvFileWriter.Write(f);

            // Formato DSTV real: um unico bloco BO, com o codigo de face por linha.
            int boCount = 0;
            int idx = 0;
            while ((idx = txt.IndexOf("BO\r\n", idx, System.StringComparison.Ordinal)) >= 0)
            {
                boCount++;
                idx += 2;
            }
            boCount.Should().Be(1);

            // Os furos aparecem com codigo de face + marcador 's' em colunas fixas.
            txt.Should().Contain("  v     100.00s     50.00      22.00");
            txt.Should().Contain("  o     150.00s      0.00      18.00");
            txt.Should().Contain("  v     200.00s     50.00      22.00");
        }

        [Fact]
        public void Write_FurosNaoOrdenados_SaoOrdenadosPorXY()
        {
            var f = MinimalFile();
            // Inserir fora de ordem
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 200, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 100, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 150, YMm = 50, DiameterMm = 22 });

            string txt = DstvFileWriter.Write(f);

            int p100 = txt.IndexOf("  v     100.00s", System.StringComparison.Ordinal);
            int p150 = txt.IndexOf("  v     150.00s", System.StringComparison.Ordinal);
            int p200 = txt.IndexOf("  v     200.00s", System.StringComparison.Ordinal);

            p100.Should().BeGreaterThan(0);
            p100.Should().BeLessThan(p150);
            p150.Should().BeLessThan(p200);
        }

        [Fact]
        public void Write_CortesRetos_NaoEmiteBlocoSC()
        {
            var f = MinimalFile();
            f.CutAngleStartDeg = 90;
            f.CutAngleEndDeg = 90;
            DstvFileWriter.Write(f).Should().NotContain("SC\r\n");
        }

        [Fact]
        public void Write_CortesEmAngulo_EmiteBlocoSC()
        {
            var f = MinimalFile();
            f.CutAngleStartDeg = 45;
            f.CutAngleEndDeg = 90;
            string txt = DstvFileWriter.Write(f);
            txt.Should().Contain("SC\r\n");
            txt.Should().Contain("  45 90\r\n");
        }

        [Fact]
        public void Write_NotasInformadas_EmiteBlocoSI()
        {
            var f = MinimalFile();
            f.Notes = "Linha 1\nLinha 2";
            string txt = DstvFileWriter.Write(f);
            txt.Should().Contain("SI\r\n");
            txt.Should().Contain("  Linha 1\r\n");
            txt.Should().Contain("  Linha 2\r\n");
        }

        [Theory]
        [InlineData(12.5, "12.5")]
        [InlineData(12.0, "12")]
        [InlineData(0.0, "0")]
        [InlineData(310.456, "310.46")]
        [InlineData(7.85, "7.85")]
        [InlineData(double.NaN, "0")]
        [InlineData(double.PositiveInfinity, "0")]
        public void FormatNumber_FormataCorretamente(double valor, string esperado)
        {
            DstvFileWriter.FormatNumber(valor).Should().Be(esperado);
        }

        [Fact]
        public void FormatNumber_UsaPontoComoSeparadorIndependenteDeCultura()
        {
            // Forcar cultura pt-BR (vírgula como decimal)
            var prevCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
                DstvFileWriter.FormatNumber(12.5).Should().Be("12.5");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prevCulture;
            }
        }

        [Fact]
        public void Write_ArquivoFinalizaCom_EN()
        {
            var f = MinimalFile();
            string txt = DstvFileWriter.Write(f);
            txt.Should().EndWith("EN\r\n");
        }

        [Fact]
        public void Write_UsaCRLF_NaoLF()
        {
            var f = MinimalFile();
            string txt = DstvFileWriter.Write(f);
            // Nao deve haver LF sozinho
            txt.Should().NotMatch("*[^\r]\n*");
        }

        [Fact]
        public void Write_LancaArgumentNullException_SeFileNulo()
        {
            System.Action act = () => DstvFileWriter.Write(null);
            act.Should().Throw<System.ArgumentNullException>();
        }

        [Fact]
        public void Write_OutputFileNameSetado_UsaNoHeaderEmVezDoDrawingNumber()
        {
            // v2.8.10 (Etapa D): a linha "** <arquivo>.nc1" precisa bater com o
            // filename real (DstvExportService.Executar seta isso antes do Save).
            var f = MinimalFile();
            f.DrawingNumber = "DWG-OLD";
            f.OutputFileName = "CH02.nc1";

            string txt = DstvFileWriter.Write(f);

            txt.Should().Contain("** CH02.nc1\r\n");
            txt.Should().NotContain("** DWG-OLD.nc1");
        }

        [Fact]
        public void Write_OutputFileNameVazio_DerivaDoDrawingNumber()
        {
            // Comportamento de fallback documentado: quando OutputFileName e' vazio
            // (chamada direta ao writer sem passar pelo Executar), usa DrawingNumber.
            var f = MinimalFile();
            f.DrawingNumber = "DWG-XYZ";
            f.OutputFileName = "";

            string txt = DstvFileWriter.Write(f);

            txt.Should().Contain("** DWG-XYZ.nc1\r\n");
        }

        // ------------ helpers ------------
        private static DstvFile MinimalFile() => new DstvFile
        {
            OrderNumber = "PRJ",
            DrawingNumber = "DWG",
            Phase = "1",
            PieceMark = "V-001",
            SteelQuality = "A36",
            Quantity = 1,
            ProfileName = "W12X26",
            ProfileType = DstvProfileType.I,
            ProfileHeightMm = 310,
            FlangeWidthMm = 165,
            FlangeThicknessMm = 9.7,
            WebThicknessMm = 5.8,
            FilletRadiusMm = 0,
            WeightPerMeter = 38.7,
            CutLengthMm = 6000
        };
    }
}
