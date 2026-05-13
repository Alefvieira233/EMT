using System.Globalization;
using System.Threading;
using FerramentaEMT.Models.CncExport;
using FerramentaEMT.Services.CncExport;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Services.CncExport
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
            txt.Should().Contain("\r\n  PRJ-001\r\n");
            txt.Should().Contain("\r\n  V-001\r\n");
            txt.Should().Contain("\r\n  W12X26\r\n");
            txt.Should().Contain("\r\n  I\r\n");        // codigo do perfil
            txt.Should().Contain("\r\n  6000\r\n");     // length
            txt.Should().EndWith("EN\r\n");
        }

        [Fact]
        public void Write_SemFuros_NaoEmiteBlocoBO()
        {
            var f = MinimalFile();
            string txt = DstvFileWriter.Write(f);
            txt.Should().NotContain("BO\r\n");
        }

        [Fact]
        public void Write_ComFuros_EmiteBlocoBOAgrupadoPorFace()
        {
            var f = MinimalFile();
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 100, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = 200, YMm = 50, DiameterMm = 22 });
            f.Holes.Add(new DstvHole { Face = DstvFace.TopFlange, XMm = 150, YMm = 0, DiameterMm = 18 });

            string txt = DstvFileWriter.Write(f);

            // Deve ter dois blocos BO (um para web front, um para top flange)
            int boCount = 0;
            int idx = 0;
            while ((idx = txt.IndexOf("BO\r\n", idx, System.StringComparison.Ordinal)) >= 0)
            {
                boCount++;
                idx += 2;
            }
            boCount.Should().Be(2);

            // Os furos devem aparecer com codigo de face
            txt.Should().Contain(" v 100 50 22");
            txt.Should().Contain(" v 200 50 22");
            txt.Should().Contain(" o 150 0 18");
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

            int p100 = txt.IndexOf(" v 100", System.StringComparison.Ordinal);
            int p150 = txt.IndexOf(" v 150", System.StringComparison.Ordinal);
            int p200 = txt.IndexOf(" v 200", System.StringComparison.Ordinal);

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
