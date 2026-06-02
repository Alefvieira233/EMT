#nullable enable
using System.Collections.Generic;

namespace SteelBIM.Services.CncExport
{
    /// <summary>
    /// v2.8.10 (Etapa D) — Helper puro para construir o contorno externo (AK)
    /// de uma CHAPA no formato esperado pelo <see cref="DstvFileWriter"/>.
    ///
    /// Convencao de Raio (alinhado com <see cref="Models.CncExport.DstvFile.ContornoAk"/>):
    /// <list type="bullet">
    ///   <item>Raio = 0: canto reto (segmento ate' o proximo ponto e' uma linha)</item>
    ///   <item>Raio &gt; 0: arco "para fora" (convexo) ate' o proximo ponto</item>
    ///   <item>Raio &lt; 0: arco "para dentro" (concavo)</item>
    /// </list>
    ///
    /// 100% sem dependencia de Autodesk.Revit.DB — testavel via xUnit.
    /// </summary>
    public static class DstvContornoAkBuilder
    {
        /// <summary>
        /// Garante que o contorno esteja fechado: se o ultimo ponto nao for igual ao
        /// primeiro (dentro da tolerancia), adiciona uma copia do primeiro no final.
        /// Operacao idempotente — chamar duas vezes nao adiciona pontos.
        /// </summary>
        /// <param name="pontos">Lista de pontos (X, Y, Raio) em mm.</param>
        /// <param name="tolMm">Tolerancia para considerar dois pontos iguais (default 0.001 mm).</param>
        /// <returns>Lista NOVA com o contorno fechado. Se a entrada for null/vazia, retorna lista vazia.</returns>
        public static List<(double X, double Y, double Raio)> FecharContorno(
            IReadOnlyList<(double X, double Y, double Raio)>? pontos,
            double tolMm = 0.001)
        {
            if (pontos == null || pontos.Count == 0)
                return new List<(double, double, double)>();

            var saida = new List<(double X, double Y, double Raio)>(pontos.Count + 1);
            for (int i = 0; i < pontos.Count; i++)
                saida.Add(pontos[i]);

            if (pontos.Count >= 1)
            {
                (double X, double Y, double Raio) primeiro = pontos[0];
                (double X, double Y, double Raio) ultimo = pontos[pontos.Count - 1];
                bool fechado =
                    System.Math.Abs(primeiro.X - ultimo.X) <= tolMm &&
                    System.Math.Abs(primeiro.Y - ultimo.Y) <= tolMm;
                if (!fechado)
                {
                    // Adiciona o primeiro como ultimo, com Raio = 0 (segmento de fechamento
                    // sempre reto — o Raio do primeiro ponto e' do segmento que sai dele).
                    saida.Add((primeiro.X, primeiro.Y, 0.0));
                }
            }

            return saida;
        }

        /// <summary>
        /// Contorno retangular fechado (4 cantos + repeticao do primeiro) — fallback
        /// quando nao foi possivel extrair contorno da geometria do Revit.
        /// </summary>
        public static List<(double X, double Y, double Raio)> Retangulo(double larguraMm, double alturaMm)
        {
            return new List<(double X, double Y, double Raio)>
            {
                (0.0,          0.0,        0.0),
                (larguraMm,    0.0,        0.0),
                (larguraMm,    alturaMm,   0.0),
                (0.0,          alturaMm,   0.0),
                (0.0,          0.0,        0.0),
            };
        }

        /// <summary>
        /// Normaliza um contorno extraido da geometria do Revit para a convencao DSTV
        /// do fabricante (validada contra CH02/CH03/CH04):
        /// <list type="number">
        ///   <item>translada para o canto minimo — o bounding box passa a comecar em (0,0);</item>
        ///   <item>orienta a MAIOR extensao no eixo X (transpoe se a extensao em Y for
        ///         maior), alinhando o X ao comprimento (= maior dimensao = CutLengthMm
        ///         do bloco ST);</item>
        ///   <item>garante winding anti-horario (CCW) do contorno externo.</item>
        /// </list>
        /// Recebe um contorno ABERTO (sem o ponto de fechamento repetido); chame
        /// <see cref="FecharContorno"/> em seguida. Idempotente para contornos ja
        /// normalizados (chamar duas vezes nao altera o resultado).
        ///
        /// <para>Observacao sobre arcos: a inversao de winding apenas reordena os pontos.
        /// Como a extracao atual gera todos os raios = 0 (cantos retos), a inversao
        /// preserva a geometria. Quando a extracao de arcos for implementada, a inversao
        /// precisara remapear os raios para o segmento correto.</para>
        /// </summary>
        /// <param name="pontos">Pontos (X, Y, Raio) em mm. Null/vazio retorna lista vazia.</param>
        public static List<(double X, double Y, double Raio)> Normalizar(
            IReadOnlyList<(double X, double Y, double Raio)>? pontos)
        {
            if (pontos == null || pontos.Count == 0)
                return new List<(double, double, double)>();

            // 1. transladar para o canto minimo (bbox comeca em 0,0)
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            foreach ((double X, double Y, double Raio) p in pontos)
            {
                if (p.X < minX)
                    minX = p.X;
                if (p.Y < minY)
                    minY = p.Y;
            }

            var saida = new List<(double X, double Y, double Raio)>(pontos.Count);
            foreach ((double X, double Y, double Raio) p in pontos)
                saida.Add((p.X - minX, p.Y - minY, p.Raio));

            // 2. maior extensao -> X (apos a translacao, min e' 0, entao max == extensao)
            double maxX = 0.0;
            double maxY = 0.0;
            foreach ((double X, double Y, double Raio) p in saida)
            {
                if (p.X > maxX)
                    maxX = p.X;
                if (p.Y > maxY)
                    maxY = p.Y;
            }
            if (maxY > maxX)
            {
                for (int i = 0; i < saida.Count; i++)
                    saida[i] = (saida[i].Y, saida[i].X, saida[i].Raio);
            }

            // 3. winding anti-horario (CCW): area assinada > 0
            if (AreaAssinada(saida) < 0)
                saida.Reverse();

            return saida;
        }

        /// <summary>
        /// Area assinada (shoelace) do poligono formado pelos pontos (fechado
        /// implicitamente). Positiva = anti-horario (CCW), negativa = horario (CW).
        /// </summary>
        private static double AreaAssinada(IReadOnlyList<(double X, double Y, double Raio)> p)
        {
            double soma = 0.0;
            for (int i = 0; i < p.Count; i++)
            {
                (double X, double Y, double Raio) a = p[i];
                (double X, double Y, double Raio) b = p[(i + 1) % p.Count];
                soma += (a.X * b.Y) - (b.X * a.Y);
            }
            return soma / 2.0;
        }
    }
}
