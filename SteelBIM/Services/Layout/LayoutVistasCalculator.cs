#nullable enable
using System.Collections.Generic;

namespace SteelBIM.Services.Layout
{
    /// <summary>Retangulo de uma vista/viewport no papel (mm). Centro = ponto de ancoragem.</summary>
    public readonly record struct CaixaVista(string Id, double LarguraMm, double AlturaMm, double CentroXMm, double CentroYMm)
    {
        public double MeiaLargura => LarguraMm / 2.0;
        public double MeiaAltura => AlturaMm / 2.0;
        public double EsquerdaMm => CentroXMm - MeiaLargura;
        public double DireitaMm => CentroXMm + MeiaLargura;
        public double BaseMm => CentroYMm - MeiaAltura;   // Y cresce para cima (convencao Revit/papel)
        public double TopoMm => CentroYMm + MeiaAltura;
    }

    /// <summary>Resultado de um reposicionamento: so o centro muda.</summary>
    public readonly record struct PosicaoVista(string Id, double CentroXMm, double CentroYMm);

    /// <summary>Area util da folha (carimbo/margens externas ja descontados).</summary>
    public readonly record struct AreaUtil(double X0Mm, double Y0Mm, double X1Mm, double Y1Mm)
    {
        public double LarguraMm => X1Mm - X0Mm;
        public double AlturaMm => Y1Mm - Y0Mm;
        public bool Degenerada => X1Mm <= X0Mm || Y1Mm <= Y0Mm;
    }

    public enum ModoAlinhamento { Esquerda, Direita, Topo, Base, CentroH, CentroV }

    public enum Eixo { Horizontal, Vertical }

    public readonly record struct ResultadoGrade(
        IReadOnlyList<PosicaoVista> Posicionados,
        IReadOnlyList<string> NaoCoube,
        int Colunas,
        int Linhas);

    /// <summary>
    /// v2.8.13: nucleo PURO (sem Revit) do layout de vistas numa folha — alinhar/distribuir
    /// (estilo PowerPoint) e distribuir em grade. Testavel por xUnit; o service Revit converte
    /// viewports (GetBoxOutline) em CaixaVista e aplica os centros via SetBoxCenter.
    /// </summary>
    public static class LayoutVistasCalculator
    {
        private const double Eps = 1e-6;

        // ===== 1. ALINHAR =====
        public static IReadOnlyList<PosicaoVista> Alinhar(IReadOnlyList<CaixaVista> caixas, ModoAlinhamento modo)
        {
            var saida = new List<PosicaoVista>();
            if (caixas == null || caixas.Count == 0)
                return saida;

            double minEsq = double.MaxValue, maxDir = double.MinValue;
            double minBase = double.MaxValue, maxTopo = double.MinValue;
            foreach (CaixaVista c in caixas)
            {
                if (c.EsquerdaMm < minEsq)
                    minEsq = c.EsquerdaMm;
                if (c.DireitaMm > maxDir)
                    maxDir = c.DireitaMm;
                if (c.BaseMm < minBase)
                    minBase = c.BaseMm;
                if (c.TopoMm > maxTopo)
                    maxTopo = c.TopoMm;
            }

            double centroHConjunto = (minEsq + maxDir) / 2.0;
            double centroVConjunto = (minBase + maxTopo) / 2.0;

            foreach (CaixaVista c in caixas)
            {
                double cx = c.CentroXMm;
                double cy = c.CentroYMm;
                switch (modo)
                {
                    case ModoAlinhamento.Esquerda:
                        cx = minEsq + c.MeiaLargura;
                        break;
                    case ModoAlinhamento.Direita:
                        cx = maxDir - c.MeiaLargura;
                        break;
                    case ModoAlinhamento.Topo:
                        cy = maxTopo - c.MeiaAltura;
                        break;
                    case ModoAlinhamento.Base:
                        cy = minBase + c.MeiaAltura;
                        break;
                    case ModoAlinhamento.CentroH:
                        cx = centroHConjunto;
                        break;
                    case ModoAlinhamento.CentroV:
                        cy = centroVConjunto;
                        break;
                }
                saida.Add(new PosicaoVista(c.Id, cx, cy));
            }
            return saida;
        }

        // ===== 2. DISTRIBUIR (espaco igual entre bordas, extremos fixos) =====
        public static IReadOnlyList<PosicaoVista> Distribuir(IReadOnlyList<CaixaVista> caixas, Eixo eixo)
        {
            var saida = new List<PosicaoVista>();
            if (caixas == null || caixas.Count == 0)
                return saida;

            // <3 itens: nada a distribuir, retorna inalterado.
            if (caixas.Count < 3)
            {
                foreach (CaixaVista c in caixas)
                    saida.Add(new PosicaoVista(c.Id, c.CentroXMm, c.CentroYMm));
                return saida;
            }

            bool horizontal = eixo == Eixo.Horizontal;

            // ordem por centro no eixo, estavel por indice de entrada
            var ordem = new List<int>();
            for (int i = 0; i < caixas.Count; i++)
                ordem.Add(i);
            ordem.Sort((a, b) =>
            {
                double ca = horizontal ? caixas[a].CentroXMm : caixas[a].CentroYMm;
                double cb = horizontal ? caixas[b].CentroXMm : caixas[b].CentroYMm;
                int cmp = ca.CompareTo(cb);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            int n = caixas.Count;
            double Tamanho(int idx) => horizontal ? caixas[idx].LarguraMm : caixas[idx].AlturaMm;
            double BordaMin(int idx) => horizontal ? caixas[idx].EsquerdaMm : caixas[idx].BaseMm;
            double BordaMax(int idx) => horizontal ? caixas[idx].DireitaMm : caixas[idx].TopoMm;

            double somaTam = 0.0;
            foreach (int idx in ordem)
                somaTam += Tamanho(idx);

            int primeiro = ordem[0];
            int ultimo = ordem[n - 1];
            double inicio = BordaMin(primeiro);
            double fim = BordaMax(ultimo);
            double gap = ((fim - inicio) - somaTam) / (n - 1);

            // centros novos no eixo, indexados pelo indice original
            var novoCentroEixo = new Dictionary<int, double>();
            double cursor = BordaMax(primeiro); // borda interna do primeiro (fixo)
            for (int k = 1; k < n - 1; k++)
            {
                int idx = ordem[k];
                cursor += gap;
                double novaBordaMin = cursor;
                double meia = Tamanho(idx) / 2.0;
                novoCentroEixo[idx] = novaBordaMin + meia;
                cursor += Tamanho(idx);
            }

            // monta saida na ORDEM DE ENTRADA; extremos e eixo cruzado inalterados
            for (int i = 0; i < n; i++)
            {
                CaixaVista c = caixas[i];
                double cx = c.CentroXMm;
                double cy = c.CentroYMm;
                if (novoCentroEixo.TryGetValue(i, out double novo))
                {
                    if (horizontal)
                        cx = novo;
                    else
                        cy = novo;
                }
                saida.Add(new PosicaoVista(c.Id, cx, cy));
            }
            return saida;
        }

        // ===== 3. GRADE =====
        public static ResultadoGrade Grade(
            IReadOnlyList<CaixaVista> caixas,
            AreaUtil area,
            int? colunas,
            double margemMm,
            double espacamentoMm)
        {
            var posicionados = new List<PosicaoVista>();
            var naoCoube = new List<string>();
            if (caixas == null || caixas.Count == 0)
                return new ResultadoGrade(posicionados, naoCoube, 0, 0);

            double m = margemMm < 0 ? 0.0 : margemMm;
            double esp = espacamentoMm < 0 ? 0.0 : espacamentoMm;

            if (area.Degenerada)
            {
                foreach (CaixaVista c in caixas)
                    naoCoube.Add(c.Id);
                return new ResultadoGrade(posicionados, naoCoube, 0, 0);
            }

            double ix0 = area.X0Mm + m;
            double iy0 = area.Y0Mm + m;
            double ix1 = area.X1Mm - m;
            double iy1 = area.Y1Mm - m;
            if (ix1 <= ix0 || iy1 <= iy0)
            {
                foreach (CaixaVista c in caixas)
                    naoCoube.Add(c.Id);
                return new ResultadoGrade(posicionados, naoCoube, 0, 0);
            }

            double larguraInterna = ix1 - ix0;
            double alturaInterna = iy1 - iy0;

            // pre-filtro: caixa maior que a area interna nunca cabe.
            var restantes = new List<CaixaVista>();
            foreach (CaixaVista c in caixas)
            {
                if (c.LarguraMm > larguraInterna + Eps || c.AlturaMm > alturaInterna + Eps)
                    naoCoube.Add(c.Id);
                else
                    restantes.Add(c);
            }
            if (restantes.Count == 0)
                return new ResultadoGrade(posicionados, naoCoube, 0, 0);

            double larguraMax = 0.0;
            foreach (CaixaVista c in restantes)
            {
                if (c.LarguraMm > larguraMax)
                    larguraMax = c.LarguraMm;
            }

            int nc;
            if (colunas.HasValue && colunas.Value > 0)
            {
                nc = System.Math.Min(colunas.Value, restantes.Count);
            }
            else
            {
                int auto = (int)System.Math.Floor((larguraInterna + esp) / (larguraMax + esp));
                if (auto < 1)
                    auto = 1;
                nc = System.Math.Min(auto, restantes.Count);
            }

            double passoX = larguraMax + esp;
            double yTopo = iy1;
            int linhasPosicionadas = 0;
            int totalLinhas = (int)System.Math.Ceiling((double)restantes.Count / nc);

            for (int r = 0; r < totalLinhas; r++)
            {
                int inicioLinha = r * nc;
                int fimLinha = System.Math.Min(inicioLinha + nc, restantes.Count);

                double alturaLinha = 0.0;
                for (int k = inicioLinha; k < fimLinha; k++)
                {
                    if (restantes[k].AlturaMm > alturaLinha)
                        alturaLinha = restantes[k].AlturaMm;
                }

                // overflow vertical: esta linha (e as seguintes) nao cabem.
                if (yTopo - alturaLinha < iy0 - Eps)
                {
                    for (int k = inicioLinha; k < restantes.Count; k++)
                        naoCoube.Add(restantes[k].Id);
                    break;
                }

                double centroY = yTopo - alturaLinha / 2.0;
                for (int k = inicioLinha; k < fimLinha; k++)
                {
                    int col = k - inicioLinha;
                    double centroX = ix0 + col * passoX + larguraMax / 2.0;
                    posicionados.Add(new PosicaoVista(restantes[k].Id, centroX, centroY));
                }

                yTopo -= alturaLinha + esp;
                linhasPosicionadas++;
            }

            return new ResultadoGrade(posicionados, naoCoube, nc, linhasPosicionadas);
        }
    }
}
