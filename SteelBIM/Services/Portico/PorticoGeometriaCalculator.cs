#nullable enable
using System;
using System.Collections.Generic;
using SteelBIM.Models;

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
        public bool PilarCentral { get; set; }                    // pilar no meio do vão (y=w/2), por pórtico

        public bool UsarTrelica { get; set; } = true;
        public double AlturaExtremidadeMm { get; set; } = 600.0;   // H (treliça, no apoio)
        public double AlturaCentralMm { get; set; } = 1600.0;      // B (treliça, na cumeeira)
        public double AlturaCumeeiraMm { get; set; } = 1500.0;     // elevacao da agua (modo viga)

        public bool LancarTercas { get; set; } = true;
        public double EspacamentoTercasMm { get; set; } = 1500.0;
        public double ElevacaoTercasMm { get; set; } = 150.0;     // sobe a terça acima do banzo superior

        public bool ContravCobertura { get; set; }
        public int TercasPorXCobertura { get; set; } = 2;         // 1 X de cobertura a cada N terças
        public DistribuicaoContravCobertura DistribuicaoContravCobertura { get; set; } = DistribuicaoContravCobertura.Extremidades;
        public bool ContravPilares { get; set; }
        public int NumeroXPilares { get; set; } = 2;              // nº de vãos com X vertical (paredes)
        public bool LancarLinhaCorrente { get; set; }
        public int NumeroLinhasCorrente { get; set; } = 3;        // nº de fileiras de linha de corrente
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

            // ===== PILARES (2 por portico: y=0 e y=w; + central opcional em y=w/2) =====
            foreach (double x in xPorticos)
            {
                pilares.Add(new Segmento(new Ponto3D(x, 0.0, 0.0), new Ponto3D(x, 0.0, hp)));
                pilares.Add(new Segmento(new Ponto3D(x, w, 0.0), new Ponto3D(x, w, hp)));
                if (e.PilarCentral)
                {
                    // modo treliça: apoia o banzo inferior (z=hp). modo viga: alcança o ápice (cumeeira).
                    double topoCentral = e.UsarTrelica ? hp : hp + e.AlturaCumeeiraMm;
                    pilares.Add(new Segmento(new Ponto3D(x, w / 2.0, 0.0), new Ponto3D(x, w / 2.0, topoCentral)));
                }
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
                    double z = ZTopo(e, hp, w, y) + e.ElevacaoTercasMm;
                    tercas.Add(new Segmento(new Ponto3D(0.0, y, z), new Ponto3D(comprimento, y, z)));

                    double yEspelho = w - y;
                    if (Math.Abs(yEspelho - y) > Eps)
                    {
                        double zEspelho = ZTopo(e, hp, w, yEspelho) + e.ElevacaoTercasMm;
                        tercas.Add(new Segmento(new Ponto3D(0.0, yEspelho, zEspelho), new Ponto3D(comprimento, yEspelho, zEspelho)));
                    }
                }
            }

            // vaos contraventados dos pilares: K distribuidos uniformemente.
            int nVaos = n - 1;
            IReadOnlyList<int> vaosPilares = DistribuirVaos(nVaos, e.NumeroXPilares);

            // ===== CONTRAVENTAMENTO DA COBERTURA (1 X a cada N terças, vaos de extremidade) =====
            if (e.ContravCobertura && e.TercasPorXCobertura > 0 && e.EspacamentoTercasMm > Eps)
            {
                IReadOnlyList<double> purlinsMeia = PosicoesTercasMeiaAgua(e, w);
                foreach (int vao in VaosContravCobertura(n, e.DistribuicaoContravCobertura))
                {
                    double xa = xPorticos[vao];
                    double xb = xPorticos[vao + 1];
                    AddXsEntrePurlins(contravCobertura, e, hp, w, xa, xb, purlinsMeia, e.TercasPorXCobertura, false); // agua 1
                    AddXsEntrePurlins(contravCobertura, e, hp, w, xa, xb, purlinsMeia, e.TercasPorXCobertura, true);  // agua 2
                }
            }

            // ===== CONTRAVENTAMENTO DOS PILARES (X vertical por vao contraventado) =====
            if (e.ContravPilares)
            {
                foreach (int vao in vaosPilares)
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

            // ===== LINHA DE CORRENTE (sag-rods subindo a agua; N fileiras distribuidas) =====
            // Liga o meio da terça (no meio do vao) ate o meio da terça da cumeeira, por agua.
            // Fica no nivel das terças (ZTopo + elevacao), coplanar com elas.
            if (e.LancarLinhaCorrente && e.NumeroLinhasCorrente > 0)
            {
                double meia = w / 2.0;
                double elev = e.ElevacaoTercasMm;
                foreach (int vao in DistribuirVaos(nVaos, e.NumeroLinhasCorrente))
                {
                    double xMid = (xPorticos[vao] + xPorticos[vao + 1]) / 2.0;
                    // agua 1: beiral (y=0) -> cumeeira (y=w/2).
                    linhasCorrente.Add(new Segmento(
                        new Ponto3D(xMid, 0.0, ZTopo(e, hp, w, 0.0) + elev),
                        new Ponto3D(xMid, meia, ZTopo(e, hp, w, meia) + elev)));
                    // agua 2: cumeeira -> beiral oposto (y=w).
                    linhasCorrente.Add(new Segmento(
                        new Ponto3D(xMid, meia, ZTopo(e, hp, w, meia) + elev),
                        new Ponto3D(xMid, w, ZTopo(e, hp, w, w) + elev)));
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

        /// <summary>Vaos da cobertura que recebem contraventamento, conforme a distribuicao escolhida.</summary>
        private static IReadOnlyList<int> VaosContravCobertura(int n, DistribuicaoContravCobertura modo)
        {
            int nVaos = n - 1;
            if (modo == DistribuicaoContravCobertura.Todos)
                return DistribuirVaos(nVaos, nVaos);
            if (modo == DistribuicaoContravCobertura.ExtremidadesECentro)
                return DistribuirVaos(nVaos, 4);
            return DistribuirVaos(nVaos, 2); // Extremidades
        }

        /// <summary>Coloca um X de contraventamento de cobertura a cada 'passo' terças, ancorado nas
        /// posicoes de terça da meia-agua (espelhado na agua 2). z no plano do banzo (ZTopo).</summary>
        private static void AddXsEntrePurlins(List<Segmento> dest, GerarPorticoEntrada e, double hp,
            double w, double xa, double xb, IReadOnlyList<double> purlinsMeia, int passo, bool espelhar)
        {
            int m = purlinsMeia.Count;
            if (m < 2 || passo < 1)
                return;

            for (int j = 0; j < m - 1; j += passo)
            {
                int j2 = Math.Min(j + passo, m - 1);
                double yA = espelhar ? w - purlinsMeia[j] : purlinsMeia[j];
                double yB = espelhar ? w - purlinsMeia[j2] : purlinsMeia[j2];
                double zA = ZTopo(e, hp, w, yA);
                double zB = ZTopo(e, hp, w, yB);
                AddX(dest,
                    new Ponto3D(xa, yA, zA), new Ponto3D(xb, yB, zB),
                    new Ponto3D(xb, yA, zA), new Ponto3D(xa, yB, zB));
            }
        }

        /// <summary>Ate 'quantidade' vaos (0..nVaos-1) distribuidos uniformemente; extremos
        /// incluidos para quantidade de 2 ou mais. Vazio quando quantidade nao positiva.</summary>
        private static IReadOnlyList<int> DistribuirVaos(int nVaos, int quantidade)
        {
            var ids = new List<int>();
            if (nVaos <= 0 || quantidade <= 0)
                return ids;
            if (quantidade >= nVaos)
            {
                for (int i = 0; i < nVaos; i++)
                    ids.Add(i);
                return ids;
            }
            if (quantidade == 1)
            {
                ids.Add(0);
                return ids;
            }
            for (int i = 0; i < quantidade; i++)
            {
                int idx = (int)Math.Round((double)i * (nVaos - 1) / (quantidade - 1));
                if (!ids.Contains(idx))
                    ids.Add(idx);
            }
            return ids;
        }
    }
}
