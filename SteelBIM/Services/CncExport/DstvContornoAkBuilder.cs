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
    }
}
