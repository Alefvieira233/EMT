#nullable enable
using System;
using System.Collections.Generic;

namespace SteelBIM.Services.Portico
{
    /// <summary>Ponto no espaco do galpao (mm). X=comprimento (porticos), Y=vao/largura, Z=altura.</summary>
    public readonly record struct Ponto3D(double XMm, double YMm, double ZMm);

    /// <summary>Segmento reto entre dois pontos (mm).</summary>
    public readonly record struct Segmento(Ponto3D A, Ponto3D B);

    /// <summary>
    /// Entrada PURA (so numeros/flags, sem tipos Revit) do gerador de portico. O
    /// GerarPorticoService mapeia o GerarPorticoConfig (com FamilySymbol) para esta entrada.
    /// </summary>
    public sealed class GerarPorticoEntrada
    {
        public int NumeroPorticos { get; set; } = 7;
        public double EspacamentoPorticosMm { get; set; } = 5000.0;
        public double VaoGalpaoMm { get; set; } = 15010.0;
        public double AlturaPilarMm { get; set; } = 4000.0;

        public bool UsarTrelica { get; set; } = true;
        public double AlturaExtremidadeMm { get; set; } = 600.0;   // H (treliça, no apoio)
        public double AlturaCentralMm { get; set; } = 1600.0;      // B (treliça, na cumeeira)
        public double AlturaCumeeiraMm { get; set; } = 1500.0;     // elevacao da agua (modo viga)

        public bool LancarTercas { get; set; } = true;
        public double EspacamentoTercasMm { get; set; } = 1500.0;

        public bool ContravCobertura { get; set; }
        public bool ContravPilares { get; set; }
        public bool LancarLinhaCorrente { get; set; }
    }

    /// <summary>Geometria calculada do galpao (todas as listas em mm).</summary>
    public sealed record PorticoLayout(
        IReadOnlyList<Segmento> Pilares,
        IReadOnlyList<Segmento> EixosInferioresTrelica,
        IReadOnlyList<Segmento> Vigas,
        IReadOnlyList<Segmento> Tercas,
        IReadOnlyList<Segmento> ContravCobertura,
        IReadOnlyList<Segmento> ContravPilares,
        IReadOnlyList<Segmento> LinhasCorrente,
        IReadOnlyList<double> XPorticosMm,
        IReadOnlyList<double> YEixosMm);

    /// <summary>
    /// Nucleo PURO (sem Revit) do "Gerar Projeto Completo (Portico)". Converte os numeros da
    /// janela na geometria de todos os membros do galpao. Testavel por xUnit; o
    /// GerarPorticoService converte cada Segmento em FamilyInstance via NewFamilyInstance.
    /// Eixos: X = comprimento (ao longo dos porticos), Y = vao/largura, Z = altura.
    /// </summary>
    public static class PorticoGeometriaCalculator
    {
        private const double Eps = 1e-6;

        public static PorticoLayout Calcular(GerarPorticoEntrada e)
        {
            var pilares = new List<Segmento>();
            var eixosTrelica = new List<Segmento>();
            var vigas = new List<Segmento>();
            var tercas = new List<Segmento>();
            var contravCobertura = new List<Segmento>();
            var contravPilares = new List<Segmento>();
            var linhasCorrente = new List<Segmento>();
            var xPorticos = new List<double>();
            var yEixos = new List<double>();

            int n = e.NumeroPorticos;
            double s = e.EspacamentoPorticosMm;
            double w = e.VaoGalpaoMm;
            double hp = e.AlturaPilarMm;

            // guarda: precisa de pelo menos 2 porticos e dimensoes positivas.
            if (n < 2 || s <= Eps || w <= Eps)
            {
                return new PorticoLayout(pilares, eixosTrelica, vigas, tercas,
                    contravCobertura, contravPilares, linhasCorrente, xPorticos, yEixos);
            }

            double comprimento = (n - 1) * s;
            for (int i = 0; i < n; i++)
                xPorticos.Add(i * s);
            yEixos.Add(0.0);
            yEixos.Add(w);

            // ===== PILARES (2 por portico: y=0 e y=w) =====
            foreach (double x in xPorticos)
            {
                pilares.Add(new Segmento(new Ponto3D(x, 0.0, 0.0), new Ponto3D(x, 0.0, hp)));
                pilares.Add(new Segmento(new Ponto3D(x, w, 0.0), new Ponto3D(x, w, hp)));
            }

            // ===== COBERTURA =====
            if (e.UsarTrelica)
            {
                // banzo inferior horizontal no beiral; o servico levanta o banzo superior.
                foreach (double x in xPorticos)
                    eixosTrelica.Add(new Segmento(new Ponto3D(x, 0.0, hp), new Ponto3D(x, w, hp)));
            }
            else
            {
                double zCume = hp + e.AlturaCumeeiraMm;
                foreach (double x in xPorticos)
                {
                    vigas.Add(new Segmento(new Ponto3D(x, 0.0, hp), new Ponto3D(x, w / 2.0, zCume)));
                    vigas.Add(new Segmento(new Ponto3D(x, w / 2.0, zCume), new Ponto3D(x, w, hp)));
                }
            }

            // ===== TERCAS (distribuidas por comprimento de agua; longitudinais) =====
            if (e.LancarTercas && e.EspacamentoTercasMm > Eps)
            {
                foreach (double y in PosicoesTercasMeiaAgua(e, w))
                {
                    double z = ZTopo(e, hp, w, y);
                    tercas.Add(new Segmento(new Ponto3D(0.0, y, z), new Ponto3D(comprimento, y, z)));

                    double yEspelho = w - y;
                    if (Math.Abs(yEspelho - y) > Eps)
                    {
                        double zEspelho = ZTopo(e, hp, w, yEspelho);
                        tercas.Add(new Segmento(new Ponto3D(0.0, yEspelho, zEspelho), new Ponto3D(comprimento, yEspelho, zEspelho)));
                    }
                }
            }

            // vaos de extremidade: {0} e {n-2} (distintos so quando n >= 3).
            var vaosExtremidade = new List<int> { 0 };
            if (n - 2 > 0)
                vaosExtremidade.Add(n - 2);

            // ===== CONTRAVENTAMENTO DA COBERTURA (X no plano das aguas, vaos de extremidade) =====
            if (e.ContravCobertura)
            {
                double zApoio = ZTopo(e, hp, w, 0.0);
                double zCume = ZTopo(e, hp, w, w / 2.0);
                foreach (int vao in vaosExtremidade)
                {
                    double xa = xPorticos[vao];
                    double xb = xPorticos[vao + 1];
                    // agua 1: apoio em y=0, cumeeira em y=w/2.
                    AddX(contravCobertura,
                        new Ponto3D(xa, 0.0, zApoio), new Ponto3D(xb, w / 2.0, zCume),
                        new Ponto3D(xb, 0.0, zApoio), new Ponto3D(xa, w / 2.0, zCume));
                    // agua 2: apoio em y=w, cumeeira em y=w/2.
                    AddX(contravCobertura,
                        new Ponto3D(xa, w, zApoio), new Ponto3D(xb, w / 2.0, zCume),
                        new Ponto3D(xb, w, zApoio), new Ponto3D(xa, w / 2.0, zCume));
                }
            }

            // ===== CONTRAVENTAMENTO DOS PILARES (X vertical nas paredes y=0 e y=w) =====
            if (e.ContravPilares)
            {
                foreach (int vao in vaosExtremidade)
                {
                    double xa = xPorticos[vao];
                    double xb = xPorticos[vao + 1];
                    foreach (double y in yEixos)
                    {
                        AddX(contravPilares,
                            new Ponto3D(xa, y, 0.0), new Ponto3D(xb, y, hp),
                            new Ponto3D(xb, y, 0.0), new Ponto3D(xa, y, hp));
                    }
                }
            }

            // ===== LINHA DE CORRENTE (tirantes longitudinais: cumeeira + meia-agua) =====
            if (e.LancarLinhaCorrente)
            {
                double[] ysLinha = { w / 2.0, w / 4.0, 3.0 * w / 4.0 };
                foreach (double y in ysLinha)
                {
                    double z = ZTopo(e, hp, w, y);
                    linhasCorrente.Add(new Segmento(new Ponto3D(0.0, y, z), new Ponto3D(comprimento, y, z)));
                }
            }

            return new PorticoLayout(pilares, eixosTrelica, vigas, tercas,
                contravCobertura, contravPilares, linhasCorrente, xPorticos, yEixos);
        }

        /// <summary>Z absoluto (mm) do topo da agua na posicao transversal y. Simetrico em w/2.</summary>
        private static double ZTopo(GerarPorticoEntrada e, double hp, double w, double y)
        {
            double meia = w / 2.0;
            double yEspelhado = y <= meia ? y : w - y;          // espelha em torno da cumeeira
            double frac = meia <= Eps ? 0.0 : yEspelhado / meia; // 0 no apoio, 1 na cumeeira
            if (e.UsarTrelica)
                return hp + e.AlturaExtremidadeMm + (e.AlturaCentralMm - e.AlturaExtremidadeMm) * frac;
            return hp + e.AlturaCumeeiraMm * frac;
        }

        /// <summary>Posicoes Y das tercas na meia-agua (0..w/2), por comprimento de inclinacao.</summary>
        private static IReadOnlyList<double> PosicoesTercasMeiaAgua(GerarPorticoEntrada e, double w)
        {
            double meia = w / 2.0;
            double rise = e.UsarTrelica ? e.AlturaCentralMm - e.AlturaExtremidadeMm : e.AlturaCumeeiraMm;
            double comprimentoAgua = Math.Sqrt(meia * meia + rise * rise);
            int passos = (int)Math.Round(comprimentoAgua / e.EspacamentoTercasMm);
            if (passos < 1)
                passos = 1;

            var ys = new List<double>();
            for (int j = 0; j <= passos; j++)
            {
                double f = (double)j / passos;
                ys.Add(f * meia);
            }
            return ys;
        }

        private static void AddX(List<Segmento> destino, Ponto3D a1, Ponto3D b1, Ponto3D a2, Ponto3D b2)
        {
            destino.Add(new Segmento(a1, b1));
            destino.Add(new Segmento(a2, b2));
        }
    }
}
