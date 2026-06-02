using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using SteelBIM.Models.CncExport;
using SteelBIM.Services.CncExport;
using Xunit;

namespace SteelBIM.Tests.Services.CncExport
{
    /// <summary>
    /// Golden test byte-a-byte do <see cref="DstvFileWriter"/> para CHAPA (code B).
    ///
    /// Os arquivos CH02/CH03/CH04.nc1 (em Fixtures\Dstv\, embutidos como recurso) sao
    /// .nc1 REAIS do fabricante e servem de oraculo: montamos o <see cref="DstvFile"/>
    /// equivalente a cada um e exigimos que a serializacao bata byte-a-byte (CRLF, ASCII,
    /// colunas de largura fixa). Qualquer regressao no formato NC1 quebra estes testes.
    ///
    /// Escopo: apenas CHAPA. Vigas (I/U/L) terao seu proprio conjunto de oraculos quando
    /// chegarem .nc1 de referencia do fabricante.
    /// </summary>
    public class DstvWriterGoldenChapaTests
    {
        [Theory]
        [MemberData(nameof(Casos))]
        public void Write_Chapa_BateByteAByteComOraculoDoFabricante(string nome, DstvFile file)
        {
            byte[] esperado = LerFixture(nome);
            string textoEsperado = Encoding.ASCII.GetString(esperado);

            string textoGerado = DstvFileWriter.Write(file);
            byte[] gerado = Encoding.ASCII.GetBytes(textoGerado);

            // Comparacao textual primeiro: diff por linha legivel em caso de falha.
            textoGerado.Should().Be(textoEsperado,
                $"o NC1 gerado para {nome} deve ser identico ao oraculo do fabricante");

            // Guarda byte-exata (CRLF, ASCII, sem BOM).
            gerado.Should().Equal(esperado);
        }

        public static IEnumerable<object[]> Casos()
        {
            yield return new object[] { "CH02", Ch02() };
            yield return new object[] { "CH03", Ch03() };
            yield return new object[] { "CH04", Ch04() };
        }

        // ----------------------------------------------------------------
        //  Montagem dos DstvFile equivalentes a cada .nc1 de referencia
        // ----------------------------------------------------------------

        private static DstvFile BaseChapa(string marca, string fase, int qtd, double altura)
        {
            return new DstvFile
            {
                OrderNumber = "0",
                DrawingNumber = marca,
                Phase = fase,
                PieceMark = marca,
                SteelQuality = "A36",
                Quantity = qtd,
                ProfileName = "CH12.7X520",
                ProfileType = DstvProfileType.B,
                CutLengthMm = 620.0,        // comprimento
                ProfileHeightMm = altura,   // "largura" da chapa (campo altura)
                FlangeWidthMm = 12.70,      // espessura
                WebThicknessMm = 12.70,     // espessura
                FlangeThicknessMm = 12.70,  // espessura
                FilletRadiusMm = 0.0,
                WeightPerMeter = 99.695,
                PaintingSurfacePerMeter = 2.116,
                IncluirContornoAk = true,
            };
        }

        private static void AddContorno(DstvFile f, params (double X, double Y, double R)[] pts)
        {
            foreach (var p in pts)
                f.ContornoAk.Add((p.X, p.Y, p.R));
        }

        private static void AddFuros(DstvFile f, params (double X, double Y)[] furos)
        {
            foreach (var h in furos)
                f.Holes.Add(new DstvHole { Face = DstvFace.WebFront, XMm = h.X, YMm = h.Y, DiameterMm = 21.0 });
        }

        private static DstvFile Ch02()
        {
            var f = BaseChapa("CH02", "109", 18, 520.0);
            AddContorno(f,
                (0, 380.48, 0), (238.41, 0, 0), (620, 0, 0), (620, 349.90, 0),
                (362.90, 349.90, -13), (349.90, 362.90, 0), (349.90, 520, 0),
                (0, 520, 0), (0, 380.48, 0));
            AddFuros(f,
                (74.90, 481.20), (140.28, 231.93), (144.90, 481.20), (199.60, 269.10),
                (214.90, 481.20), (258.92, 306.27), (284.90, 481.20), (581.20, 74.90),
                (581.20, 144.90), (581.20, 214.90), (581.20, 284.90));
            return f;
        }

        private static DstvFile Ch03()
        {
            var f = BaseChapa("CH03", "104", 72, 520.0);
            AddContorno(f,
                (0, 366.06, 0), (248.12, 0, 0), (620, 0, 0), (620, 349.40, 0),
                (362.90, 349.40, -13), (349.90, 362.40, 0), (349.90, 520, 0),
                (0, 520, 0), (0, 366.06, 0));
            AddFuros(f,
                (74.90, 481.20), (144.90, 481.20), (158.10, 204.10), (214.90, 481.20),
                (216.05, 243.37), (273.99, 282.65), (284.90, 481.20), (581.20, 74.40),
                (581.20, 144.40), (581.20, 214.40), (581.20, 284.40));
            return f;
        }

        private static DstvFile Ch04()
        {
            var f = BaseChapa("CH04", "101", 36, 520.0);
            AddContorno(f,
                (0, 366.56, 0), (248.46, 0, 0), (620, 0, 0), (620, 349.90, 0),
                (362.90, 349.90, -13), (349.90, 362.90, 0), (349.90, 520, 0),
                (0, 520, 0), (0, 366.56, 0));
            AddFuros(f,
                (74.90, 481.20), (144.90, 481.20), (151.48, 214.37), (209.43, 253.64),
                (214.90, 481.20), (267.37, 292.92), (284.90, 481.20), (581.20, 74.90),
                (581.20, 144.90), (581.20, 214.90), (581.20, 284.90));
            return f;
        }

        // ----------------------------------------------------------------
        //  Leitura do oraculo embutido
        // ----------------------------------------------------------------

        private static byte[] LerFixture(string nome)
        {
            Assembly asm = typeof(DstvWriterGoldenChapaTests).Assembly;
            string sufixo = "." + nome + ".nc1";
            string? recurso = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(sufixo, StringComparison.Ordinal));

            recurso.Should().NotBeNull(
                $"a fixture {nome}.nc1 deve estar embutida como EmbeddedResource no SteelBIM.Tests.csproj");

            using Stream stream = asm.GetManifestResourceStream(recurso!)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
