using System;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    /// <summary>
    /// v2.8.10 (Etapa D): testes do mapeamento puro de dimensoes de CHAPA.
    /// O writer NC1 espera (comp, largura, esp, esp, esp, 0) nessa ordem; o
    /// mapper garante que o produtor Revit-bound preenche identico ao golden
    /// CH02/CH03/CH04.
    /// </summary>
    public class DstvChapaDimensionsMapperTests
    {
        // ============================================
        // Map(comp, largura, esp)
        // ============================================

        [Fact]
        public void Map_CH02_ProduzCamposEsperados()
        {
            // CH02: 620 x 520 x 12.70 — replica byte do golden.
            DstvChapaDimensionsMapper.ChapaDimensions d =
                DstvChapaDimensionsMapper.Map(620.0, 520.0, 12.70);

            Assert.Equal(620.0, d.CutLengthMm);
            Assert.Equal(520.0, d.ProfileHeightMm);
            Assert.Equal(12.70, d.FlangeWidthMm);
            Assert.Equal(12.70, d.WebThicknessMm);
            Assert.Equal(12.70, d.FlangeThicknessMm);
            Assert.Equal(0.0, d.FilletRadiusMm);
        }

        [Theory]
        [InlineData(0.0, 100.0, 5.0)]
        [InlineData(100.0, 0.0, 5.0)]
        [InlineData(100.0, 50.0, 0.0)]
        [InlineData(-1.0, 100.0, 5.0)]
        [InlineData(100.0, -1.0, 5.0)]
        [InlineData(100.0, 50.0, -1.0)]
        public void Map_DimensoesInvalidasLancam(double comp, double largura, double esp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DstvChapaDimensionsMapper.Map(comp, largura, esp));
        }

        // ============================================
        // FromBoundingBox(dx, dy, dz)
        // ============================================

        [Theory]
        [InlineData(620.0, 520.0, 12.70)] // comprimento em X
        [InlineData(520.0, 620.0, 12.70)] // comprimento em Y
        [InlineData(12.70, 620.0, 520.0)] // espessura em X
        [InlineData(12.70, 520.0, 620.0)] // ordem invertida
        [InlineData(520.0, 12.70, 620.0)] // espessura em Y
        [InlineData(620.0, 12.70, 520.0)] // largura em Z
        public void FromBoundingBox_QualquerOrdemDoBbox_RetornaDimensoesCorretas(double dx, double dy, double dz)
        {
            // CH02 sempre deve mapear 620 comp / 520 largura / 12.70 esp,
            // independente da orientacao no bbox.
            DstvChapaDimensionsMapper.ChapaDimensions d =
                DstvChapaDimensionsMapper.FromBoundingBox(dx, dy, dz);

            Assert.Equal(620.0, d.CutLengthMm);
            Assert.Equal(520.0, d.ProfileHeightMm);
            Assert.Equal(12.70, d.FlangeWidthMm);
            Assert.Equal(12.70, d.WebThicknessMm);
            Assert.Equal(12.70, d.FlangeThicknessMm);
            Assert.Equal(0.0, d.FilletRadiusMm);
        }

        [Fact]
        public void FromBoundingBox_TodasIguais_TratadoComoCubo()
        {
            // Caso degenerado: cubo. Esp = min = 100, comp = max = 100, largura = sobra = 100.
            // Map nao lanca porque tudo > 0 — caller decide se cubo e' chapa valida.
            DstvChapaDimensionsMapper.ChapaDimensions d =
                DstvChapaDimensionsMapper.FromBoundingBox(100.0, 100.0, 100.0);
            Assert.Equal(100.0, d.CutLengthMm);
            Assert.Equal(100.0, d.ProfileHeightMm);
            Assert.Equal(100.0, d.FlangeWidthMm);
        }

        [Fact]
        public void FromBoundingBox_DimensaoZerada_Lanca()
        {
            // Bbox degenerado real (0 em uma dimensao) — Map rejeita.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DstvChapaDimensionsMapper.FromBoundingBox(0.0, 100.0, 100.0));
        }

        // ============================================
        // FromContorno(contorno, espessura) — dims do plano da face (chapa inclinada)
        // ============================================

        // Contorno do CH02 (8 pontos, bbox 620 x 520), no plano da face.
        private static System.Collections.Generic.List<(double X, double Y, double Raio)> Ch02Contorno() => new()
        {
            (0.0,    380.48, 0.0),
            (238.41, 0.0,    0.0),
            (620.0,  0.0,    0.0),
            (620.0,  349.90, 0.0),
            (362.90, 349.90, 0.0),
            (349.90, 362.90, 0.0),
            (349.90, 520.0,  0.0),
            (0.0,    520.0,  0.0),
        };

        [Fact]
        public void FromContorno_CH02_DerivaCompELarguraDoContorno()
        {
            DstvChapaDimensionsMapper.ChapaDimensions d =
                DstvChapaDimensionsMapper.FromContorno(Ch02Contorno(), 12.70);

            Assert.Equal(620.0, d.CutLengthMm, 6);     // extensao em X
            Assert.Equal(520.0, d.ProfileHeightMm, 6); // extensao em Y
            Assert.Equal(12.70, d.FlangeWidthMm, 6);
            Assert.Equal(12.70, d.WebThicknessMm, 6);
            Assert.Equal(0.0, d.FilletRadiusMm);
        }

        [Fact]
        public void FromContorno_IndependeDeOffset_ChapaInclinadaProjetada()
        {
            // Contorno transladado (como sairia projetado de uma face inclinada):
            // as EXTENSOES nao mudam — e' isso que corrige o caso de chapa inclinada.
            System.Collections.Generic.List<(double X, double Y, double Raio)> deslocado =
                Ch02Contorno().ConvertAll(p => (p.X + 1000.0, p.Y - 500.0, p.Raio));

            DstvChapaDimensionsMapper.ChapaDimensions d =
                DstvChapaDimensionsMapper.FromContorno(deslocado, 8.0);

            Assert.Equal(620.0, d.CutLengthMm, 6);
            Assert.Equal(520.0, d.ProfileHeightMm, 6);
            Assert.Equal(8.0, d.WebThicknessMm, 6);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void FromContorno_MenosDe3Pontos_Lanca(int n)
        {
            System.Collections.Generic.List<(double X, double Y, double Raio)> pts = Ch02Contorno().GetRange(0, n);
            Assert.Throws<ArgumentException>(() => DstvChapaDimensionsMapper.FromContorno(pts, 10.0));
        }

        [Fact]
        public void FromContorno_EspessuraInvalida_Lanca()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DstvChapaDimensionsMapper.FromContorno(Ch02Contorno(), 0.0));
        }
    }
}
