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
        public DistribuicaoContrav DistribuicaoContravCobertura { get; set; } = DistribuicaoContrav.Extremidades;
        public bool ContravPilares { get; set; }
        public DistribuicaoContrav DistribuicaoContravPilares { get; set; } = DistribuicaoContrav.Extremidades; // mesmo padrão da cobertura
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

            // guarda: precisa de pelo menos 2 porticos, dimensoes positivas (espacamento, vao e
            // altura de pilar) e alturas de cobertura nao-negativas. Entradas degeneradas gerariam
            // pilares de altura zero ou agua invertida — melhor devolver layout vazio.
            bool alturasCoberturaInvalidas =
                e.AlturaExtremidadeMm < 0.0 || e.AlturaCentralMm < 0.0 || e.AlturaCumeeiraMm < 0.0;
            if (n < 2 || s <= Eps || w <= Eps || hp <= Eps || alturasCoberturaInvalidas)
            {
                return new PorticoLayout(pilares, eixosTrelica, vigas, tercas,
                    contravCobertura, contravPilares, linhasCorrente, xPorticos, yEixos);
            }

            // elevacao da terca e um offset cosmetico; valor negativo afundaria a terca no banzo.
            double elevTercas = e.ElevacaoTercasMm < 0.0 ? 0.0 : e.ElevacaoTercasMm;

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
                    double z = ZTopo(e, hp, w, y) + elevTercas;
                    tercas.Add(new Segmento(new Ponto3D(0.0, y, z), new Ponto3D(comprimento, y, z)));

                    double yEspelho = w - y;
                    if (Math.Abs(yEspelho - y) > Eps)
                    {
                        double zEspelho = ZTopo(e, hp, w, yEspelho) + elevTercas;
                        tercas.Add(new Segmento(new Ponto3D(0.0, yEspelho, zEspelho), new Ponto3D(comprimento, yEspelho, zEspelho)));
                    }
                }
            }

            // vaos contraventados dos pilares: pela distribuicao escolhida (mesmo padrao da cobertura).
            int nVaos = n - 1;
            IReadOnlyList<int> vaosPilares = VaosContraventados(n, e.DistribuicaoContravPilares);

            // ===== CONTRAVENTAMENTO DA COBERTURA (1 X a cada N terças, vaos de extremidade) =====
            if (e.ContravCobertura && e.TercasPorXCobertura > 0 && e.EspacamentoTercasMm > Eps)
            {
                IReadOnlyList<double> purlinsMeia = PosicoesTercasMeiaAgua(e, w);
                foreach (int vao in VaosContraventados(n, e.DistribuicaoContravCobertura))
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

            // ===== LINHA DE CORRENTE (sag-rods subindo a agua) =====
            // Divide o COMPRIMENTO da terça (galpao inteiro) em (N+1) partes e poe N linhas nos
            // pontos interiores: 2 -> tercos (L/3, 2L/3), 3 -> quartos, etc. Cada linha sobe a agua
            // do beiral a cumeeira, no nivel das terças (ZTopo + elevacao). "N = N linhas".
            if (e.LancarLinhaCorrente && e.NumeroLinhasCorrente > 0)
            {
                double meia = w / 2.0;
                double elev = elevTercas;
                int nLinhas = e.NumeroLinhasCorrente;
                for (int k = 1; k <= nLinhas; k++)
                {
                    double xPos = comprimento * k / (nLinhas + 1);
                    // agua 1: beiral (y=0) -> cumeeira (y=w/2).
                    linhasCorrente.Add(new Segmento(
                        new Ponto3D(xPos, 0.0, ZTopo(e, hp, w, 0.0) + elev),
                        new Ponto3D(xPos, meia, ZTopo(e, hp, w, meia) + elev)));
                    // agua 2: cumeeira -> beiral oposto (y=w).
                    linhasCorrente.Add(new Segmento(
                        new Ponto3D(xPos, meia, ZTopo(e, hp, w, meia) + elev),
                        new Ponto3D(xPos, w, ZTopo(e, hp, w, w) + elev)));
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

        /// <summary>
        /// Nº TOTAL de paineis (vaos) da treliça ao longo do vao para que o espacamento das tercas
        /// (em planta) fique proximo do alvo (mm). P sempre PAR, garantindo um no' exatamente na
        /// cumeeira (sem painel curto). Usado tanto para os montantes quanto para as tercas, de modo
        /// que cada terça caia exatamente sobre um montante. Minimo 2.
        /// </summary>
        public static int PaineisTrelica(double vaoMm, double espacamentoAlvoMm)
        {
            if (vaoMm <= Eps || espacamentoAlvoMm <= Eps)
                return 2;
            int p = (int)Math.Round(vaoMm / espacamentoAlvoMm);
            if (p < 2)
                p = 2;
            if (p % 2 != 0)   // par => no' natural na cumeeira (montante de king-post), sem painel curto
                p++;
            return p;
        }

        /// <summary>
        /// Rotacao da secao (rad) para a terça assentar na INCLINACAO da agua (banzo superior),
        /// automaticamente: beta = atan2(rise, meia-largura). Agua 1 (y &lt; meia) recebe +beta;
        /// agua 2 (y &gt; meia) recebe -beta; cumeeira (y == meia) e agua plana => 0.
        /// rise = (B - H) na treliça; AlturaCumeeira na viga. Nao exige dado do usuario.
        /// </summary>
        public static double InclinacaoTercaRad(GerarPorticoEntrada e, double y)
        {
            double meia = e.VaoGalpaoMm / 2.0;
            if (meia <= Eps)
                return 0.0;
            double rise = e.UsarTrelica ? e.AlturaCentralMm - e.AlturaExtremidadeMm : e.AlturaCumeeiraMm;
            if (rise <= Eps)
                return 0.0; // agua plana => terça na horizontal
            if (Math.Abs(y - meia) < Eps)
                return 0.0; // terça da cumeeira assenta no pico
            double beta = Math.Atan2(rise, meia);
            return y < meia ? beta : -beta;
        }

        /// <summary>Posicoes Y das tercas na meia-agua (0..w/2). Em treliça, coincidem com os
        /// montantes (divisao uniforme P par); em viga, distribuem pela inclinacao real da agua.</summary>
        private static IReadOnlyList<double> PosicoesTercasMeiaAgua(GerarPorticoEntrada e, double w)
        {
            if (w <= Eps)
                return new List<double>();

            double meia = w / 2.0;

            // Treliça: terças sobre os montantes. P paineis no vao (par), terças em i*w/P na meia-agua.
            if (e.UsarTrelica)
            {
                int p = PaineisTrelica(w, e.EspacamentoTercasMm);
                int meioP = p / 2;
                var ysT = new List<double>(meioP + 1);
                for (int i = 0; i <= meioP; i++)
                    ysT.Add(i * w / p);
                return ysT;
            }

            // Viga: sem montantes, distribui pela inclinacao real (auto-defensivo se espacamento invalido).
            if (e.EspacamentoTercasMm <= Eps)
                return new List<double>();

            double rise = e.AlturaCumeeiraMm;
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

        // Nº de vaos contraventados por modo. Em galpoes curtos (nVaos < o alvo) DistribuirVaos
        // colapsa para menos vaos sem erro (ex.: nVaos=2 + ExtremidadesECentro => [0,1]).
        private const int VaosExtremidades = 2;       // so as duas pontas
        private const int VaosExtremidadesECentro = 4; // pontas + 2 quadrantes centrais

        /// <summary>Vaos (cobertura ou pilares) que recebem contraventamento, conforme a distribuicao escolhida.</summary>
        private static IReadOnlyList<int> VaosContraventados(int n, DistribuicaoContrav modo)
        {
            int nVaos = n - 1;
            if (modo == DistribuicaoContrav.Todos)
                return DistribuirVaos(nVaos, nVaos);
            if (modo == DistribuicaoContrav.ExtremidadesECentro)
                return DistribuirVaos(nVaos, VaosExtremidadesECentro);
            return DistribuirVaos(nVaos, VaosExtremidades);
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
