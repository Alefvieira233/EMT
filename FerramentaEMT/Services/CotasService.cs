#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Core;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Views;

namespace FerramentaEMT.Services
{
    public class CotasService
    {
        private static readonly double OffsetLinha =
            UnitUtils.ConvertToInternalUnits(250, UnitTypeId.Millimeters);

        private static readonly double ExtensaoLinha =
            UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters);

        private static readonly double ToleranciaDeduplicacao =
            UnitUtils.ConvertToInternalUnits(5, UnitTypeId.Millimeters);

        private static readonly double ToleranciaIntervalo =
            UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters);

        private static readonly double ToleranciaAgrupamento =
            UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);

        private static readonly double ToleranciaSeparacaoGrupos =
            UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters);

        /// <summary>
        /// Entry-point chamado por <c>CmdGerarCotasPorAlinhamento</c>.
        /// </summary>
        /// <remarks>
        /// NB (2026-04-29): hoje delega para <see cref="ExecutarCotagemAutomatica"/>,
        /// nao para <see cref="ExecutarCotagemAlinhada"/> — comportamento herdado, possivel
        /// dead-code em <c>ExecutarCotagemAlinhada</c>. Issue separada para revisar.
        /// </remarks>
        public Result<CotagemResumo> Executar(UIDocument uidoc)
        {
            return ExecutarCotagemAutomatica(uidoc);
        }

        /// <summary>Entry-point chamado por <c>CmdGerarCotasPorEixo</c>.</summary>
        public Result<CotagemResumo> ExecutarAutomatico(UIDocument uidoc)
        {
            return ExecutarCotagemAutomatica(uidoc);
        }

        private Result<CotagemResumo> ExecutarCotagemAlinhada(UIDocument uidoc)
        {
            if (uidoc is null)
                return Result<CotagemResumo>.Fail("UIDocument nulo.");

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!VistaSuportada(view))
                return Result<CotagemResumo>.Fail("Este comando funciona em plantas, cortes e elevacoes.");

            List<Element> elementos = ObterElementosSelecionados(uidoc, doc);
            if (elementos.Count == 0)
                elementos = PedirSelecaoDeElementos(uidoc, doc);

            if (elementos.Count == 0)
                return Result<CotagemResumo>.Fail("Nenhum elemento valido selecionado.");

            if (!TentarObterLinhaDeCota(uidoc, elementos, view, out DadosLinhaCota? dadosLinha, out string? erroLinha) || dadosLinha is null)
            {
                // user cancelou (Esc) -> erroLinha == null; trecho invalido -> erroLinha preenchido
                return string.IsNullOrEmpty(erroLinha)
                    ? Result<CotagemResumo>.Ok(CotagemResumo.CanceladoPeloUsuario())
                    : Result<CotagemResumo>.Fail(erroLinha);
            }

            ModoCota? modo = PedirModoCota();
            if (modo is null)
                return Result<CotagemResumo>.Ok(CotagemResumo.CanceladoPeloUsuario());

            return CriarCotaAlinhada(uidoc, doc, view, elementos, dadosLinha, "Cotas por Linha", true, modo.Value);
        }

        // ---------------------------------------------------------
        // Versão automática (não exposta pelo comando principal, mas mantida para reutilização)
        // ---------------------------------------------------------
        private Result<CotagemResumo> ExecutarCotagemAutomatica(UIDocument uidoc)
        {
            if (uidoc is null)
                return Result<CotagemResumo>.Fail("UIDocument nulo.");

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!VistaSuportada(view))
                return Result<CotagemResumo>.Fail("Este comando funciona em plantas, cortes e elevacoes.");

            List<Element> elementos = ObterElementosSelecionados(uidoc, doc);
            if (elementos.Count == 0)
                elementos = PedirSelecaoDeElementos(uidoc, doc);

            if (elementos.Count == 0)
                return Result<CotagemResumo>.Fail("Nenhum elemento valido selecionado.");

            if (!TentarObterPontoDeLado(uidoc, out XYZ? pontoLado) || pontoLado is null)
                return Result<CotagemResumo>.Ok(CotagemResumo.CanceladoPeloUsuario());

            List<Dimension> dimensoesCriadas = new();
            List<string> falhas = new();

            // Gerar cotas nos DOIS eixos da vista (horizontal E vertical).
            // Antes só gerava no eixo principal da seleção, o que omitia as cotas transversais.
            XYZ eixoHorizontalVista = ObterEixoHorizontalDaVista(view);
            XYZ eixoVerticalVista = ObterEixoVerticalDaVista(view);

            TentarCriarCotasAutomaticasPorEixo(doc, view, elementos, pontoLado,
                eixoHorizontalVista, "horizontal", dimensoesCriadas, falhas);

            TentarCriarCotasAutomaticasPorEixo(doc, view, elementos, pontoLado,
                eixoVerticalVista, "vertical", dimensoesCriadas, falhas);

            if (dimensoesCriadas.Count == 0)
            {
                string mensagem = "Não foi possível criar cotas automáticas para a seleção.\n\n" +
                                  "Dicas:\n" +
                                  "• Verifique se os elementos estão visíveis na vista ativa.\n" +
                                  "• Se a seleção tiver muitos grupos desconectados, rode o comando por regiões.\n" +
                                  "• Para ajuste fino manual, continue usando CotasPorLinha.";

                if (falhas.Count > 0)
                    mensagem += "\n\nDetalhes:\n• " + string.Join("\n• ", falhas);

                return Result<CotagemResumo>.Fail(mensagem);
            }

            uidoc.Selection.SetElementIds(dimensoesCriadas.Select(d => d.Id).ToList());

            string resumo = $"Cotas criadas com sucesso!\n" +
                            $"Elementos cotados : {elementos.Count}\n" +
                            $"Cotas geradas     : {dimensoesCriadas.Count}";

            if (falhas.Count > 0)
                resumo += "\n\nObservações:\n• " + string.Join("\n• ", falhas);

            return Result<CotagemResumo>.Ok(new CotagemResumo
            {
                CotasCriadas = dimensoesCriadas.Count,
                ElementosCotados = elementos.Count,
                Avisos = falhas,
                MensagemSucessoFormatada = resumo,
            });
        }

        private Result<CotagemResumo> CriarCotaAlinhada(
            UIDocument uidoc,
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha,
            string titulo,
            bool exibirDicasTrecho,
            ModoCota modoCota)
        {
            List<List<ReferenciaCota>> candidatos = modoCota == ModoCota.Faces
                ? new List<List<ReferenciaCota>> { RemoverDuplicadasPorPosicao(ColetarReferenciasDaLinha(doc, view, elementos, dadosLinha)) }
                : ColetarCandidatosDeReferenciasDosEixos(doc, view, elementos, dadosLinha);

            bool permitirFallbackAuxiliar = modoCota == ModoCota.Eixos && view is ViewSection;
            if ((candidatos.Count == 0 || candidatos.All(r => r.Count < 2)) && !permitirFallbackAuxiliar)
            {
                string mensagem = "Não foram encontradas referências suficientes para criar a cota.\n\n" +
                                  "Dicas:\n" +
                                  "• Verifique se os elementos estão visíveis na vista ativa.\n" +
                                  "• Elementos sem geometria sólida (p.ex. símbolos 2D) não são suportados.";

                mensagem += exibirDicasTrecho
                    ? "\n• Clique nos dois extremos do trecho, alinhado com a direção da cota."
                    : "\n• Se a seleção tiver mais de um alinhamento, rode o comando por grupos.";

                return Result<CotagemResumo>.Fail(mensagem);
            }

            Dimension? dimensaoCriada = null;
            string? ultimoErro = null;
            List<ReferenciaCota>? referenciasUsadas = null;
            string? erroAuxiliar = null;

            foreach (List<ReferenciaCota> candidato in candidatos.Where(r => r.Count >= 2))
            {
                if (TentarCriarDimensao(doc, view, elementos, dadosLinha.Linha, titulo, candidato, out Dimension? dim, out string? erro))
                {
                    dimensaoCriada = dim;
                    referenciasUsadas = candidato;
                    break;
                }

                ultimoErro = erro;
            }

            if (dimensaoCriada is null &&
                modoCota == ModoCota.Eixos &&
                view is ViewSection &&
                TentarCriarDimensaoPorPlanosAuxiliares(doc, view, elementos, dadosLinha, titulo, out Dimension? dimAuxiliar, out erroAuxiliar))
            {
                dimensaoCriada = dimAuxiliar;
                referenciasUsadas = new List<ReferenciaCota>();
            }
            else if (dimensaoCriada is null && modoCota == ModoCota.Eixos && view is ViewSection)
            {
                ultimoErro = erroAuxiliar;
            }

            if (dimensaoCriada is null || referenciasUsadas is null)
            {
                return Result<CotagemResumo>.Fail(
                    $"Falha ao criar a cota:\n{ultimoErro ?? "nenhuma combinacao de referencias foi aceita pela API."}");
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { dimensaoCriada.Id });

            string modoStr = modoCota == ModoCota.Faces ? "Faces" : "Eixos";
            string resumoSucesso =
                $"Cota criada com sucesso!\n" +
                $"Modo              : {modoStr}\n" +
                $"Elementos cotados : {elementos.Count}\n" +
                $"Referencias usadas: {referenciasUsadas.Count}";

            return Result<CotagemResumo>.Ok(new CotagemResumo
            {
                CotasCriadas = 1,
                ElementosCotados = elementos.Count,
                MensagemSucessoFormatada = resumoSucesso,
            });
        }

        private ModoCota? PedirModoCota()
        {
            CotasModoWindow window = new CotasModoWindow();
            bool? result = window.ShowDialog();
            if (result != true)
                return null;

            return window.UsarFaces ? ModoCota.Faces : ModoCota.Eixos;
        }

        private List<Element> ObterElementosSelecionados(UIDocument uidoc, Document doc)
        {
            return uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(e => e is not null && EhElementoCotavel(e))
                .Cast<Element>()
                .ToList();
        }

        private List<Element> PedirSelecaoDeElementos(UIDocument uidoc, Document doc)
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new FiltroSelecaoElementos(),
                    "Selecione os elementos a cotar e pressione Enter");

                return refs
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(e => e is not null && EhElementoCotavel(e))
                    .Cast<Element>()
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
            {
                Logger.Warn(ex, "CotasService.CriarCotaAlinhada: erro silenciado");
                return new List<Element>();
            }
        }

        /// <summary>
        /// Coleta dois pontos do usuario e devolve a linha de cota correspondente.
        /// Retorno: <c>true</c> = linha valida; <c>false</c> = (a) user cancelou via Esc
        /// (<paramref name="erro"/> = null) ou (b) trecho invalido (<paramref name="erro"/>
        /// com mensagem amigavel). O caller distingue cancel vs erro pela flag de mensagem.
        /// </summary>
        private bool TentarObterLinhaDeCota(
            UIDocument uidoc,
            List<Element> elementos,
            View view,
            out DadosLinhaCota? dadosLinha,
            out string? erro)
        {
            dadosLinha = null;
            erro = null;

            try
            {
                XYZ p1 = uidoc.Selection.PickPoint(
                    ObjectSnapTypes.Midpoints | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections,
                    "Clique no PRIMEIRO ponto do trecho a cotar");

                XYZ p2 = uidoc.Selection.PickPoint(
                    ObjectSnapTypes.Midpoints | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections,
                    "Clique no SEGUNDO ponto do trecho a cotar");

                XYZ eixoHorizontal = ObterEixoHorizontalDaVista(view);
                XYZ eixoVertical = ObterEixoVerticalDaVista(view);
                XYZ delta = p2 - p1;
                double deltaHorizontal = ProjetarNoEixo(delta, eixoHorizontal);
                double deltaVertical = ProjetarNoEixo(delta, eixoVertical);

                if (Math.Abs(deltaHorizontal) < 1e-9 && Math.Abs(deltaVertical) < 1e-9)
                {
                    erro = "Os pontos informados sao coincidentes.";
                    return false;
                }

                XYZ eixo;
                XYZ normal;
                double absX = Math.Abs(deltaHorizontal);
                double absY = Math.Abs(deltaVertical);

                if (absX >= absY)
                {
                    eixo = eixoHorizontal;
                    normal = eixoVertical;
                    p2 = p1 + eixo.Multiply(deltaHorizontal);
                }
                else
                {
                    eixo = eixoVertical;
                    normal = eixoHorizontal;
                    p2 = p1 + eixo.Multiply(deltaVertical);
                }

                double proj1 = ProjetarNoEixo(p1, eixo);
                double proj2 = ProjetarNoEixo(p2, eixo);
                double eixoMin = Math.Min(proj1, proj2) - ExtensaoLinha;
                double eixoMax = Math.Max(proj1, proj2) + ExtensaoLinha;

                double sinalLado = DefinirLadoDaCota(elementos, view, p1, p2, normal);
                double extremoNormal = ObterExtremoNaNormal(elementos, view, normal, sinalLado);
                double posicaoLinha = extremoNormal + sinalLado * OffsetLinha;

                XYZ pontoInicial = ConstruirPontoNoPlano(eixoMin, posicaoLinha, eixo, normal, p1);
                XYZ pontoFinal = ConstruirPontoNoPlano(eixoMax, posicaoLinha, eixo, normal, p1);

                if (pontoInicial.DistanceTo(pontoFinal) < 1e-6)
                {
                    erro = "O trecho definido e pequeno demais para criar uma cota.";
                    return false;
                }

                dadosLinha = new DadosLinhaCota(
                    Line.CreateBound(pontoInicial, pontoFinal),
                    eixo, normal, ObterNormalDaVista(view), p1, sinalLado,
                    Math.Min(proj1, proj2) - ToleranciaIntervalo,
                    Math.Max(proj1, proj2) + ToleranciaIntervalo);

                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
            {
                Logger.Warn(ex, "CotasService.TentarObterLinhaDeCota: erro silenciado");
                return false;
            }
        }

        private bool TentarObterPontoDeLado(UIDocument uidoc, out XYZ? pontoLado)
        {
            pontoLado = null;
            try
            {
                pontoLado = uidoc.Selection.PickPoint(
                    ObjectSnapTypes.Points | ObjectSnapTypes.Nearest,
                    "Clique no ponto onde as cotas devem ser posicionadas");
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
            {
                Logger.Warn(ex, "CotasService.TentarObterPontoDeLado: erro silenciado");
                return false;
            }
        }

        private void TentarCriarCotasAutomaticasPorEixo(
            Document doc,
            View view,
            List<Element> elementos,
            XYZ pontoLado,
            XYZ eixo,
            string nomeEixo,
            List<Dimension> dimensoesCriadas,
            List<string> falhas)
        {
            List<List<Element>> grupos = AgruparElementosPorAlinhamento(elementos, view, eixo);
            if (grupos.Count == 0)
            {
                falhas.Add($"Sem grupos válidos para a cota {nomeEixo}.");
                return;
            }

            int indiceGrupo = 1;
            foreach (List<Element> grupo in grupos)
            {
                if (!TentarObterLinhaDeCotaAutomaticaParaEixo(grupo, view, pontoLado, eixo, out DadosLinhaCota? dadosLinha) || dadosLinha is null)
                {
                    falhas.Add($"Cota {nomeEixo}, grupo {indiceGrupo}: sem faixa válida.");
                    indiceGrupo++;
                    continue;
                }

                if (TentarCriarCotaAlinhada(doc, view, grupo, dadosLinha, out Dimension? dimensao, out string? erro))
                {
                    if (dimensao is not null)
                        dimensoesCriadas.Add(dimensao);
                }
                else
                {
                    falhas.Add($"Cota {nomeEixo}, grupo {indiceGrupo}: {erro}");
                }

                indiceGrupo++;
            }
        }

        private bool TentarCriarCotaAlinhada(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha,
            out Dimension? dimensao,
            out string? erro)
        {
            dimensao = null;
            erro = null;

            List<ReferenciaCota> referencias = ColetarReferenciasDaLinha(doc, view, elementos, dadosLinha);
            referencias = RemoverDuplicadasPorPosicao(referencias);

            if (referencias.Count < 2)
            {
                erro = "referências insuficientes";
                return false;
            }

            ReferenceArray refArray = new ReferenceArray();
            foreach (ReferenciaCota ref_ in referencias.OrderBy(r => r.Posicao))
                refArray.Append(ref_.Referencia);

            Line linhaFinal = AjustarZDaLinhaDeCota(dadosLinha.Linha, elementos, view);

            try
            {
                using (Transaction t = new Transaction(doc, "Criar Cota Alinhada"))
                {
                    t.Start();

                    // Workaround para cota invisível: recriar com as References da cota provisória.
                    Dimension dimProvisoria = doc.Create.NewDimension(view, linhaFinal, refArray);
                    ReferenceArray refsValidadas = dimProvisoria.References;
                    doc.Delete(dimProvisoria.Id);
                    dimensao = doc.Create.NewDimension(view, linhaFinal, refsValidadas);

                    t.Commit();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "CotasService.TentarCriarCotaAlinhada: erro ao criar cota alinhada");
                erro = ex.Message;
                return false;
            }
        }

        private bool TentarObterLinhaDeCotaAutomaticaParaEixo(
            List<Element> elementos,
            View view,
            XYZ pontoLado,
            XYZ eixo,
            out DadosLinhaCota? dadosLinha)
        {
            dadosLinha = null;
            eixo = AjustarSentidoDoEixoPelaSelecao(elementos, view, eixo);

            double eixoMin = ObterExtremoNaDirecao(elementos, view, eixo, false);
            double eixoMax = ObterExtremoNaDirecao(elementos, view, eixo, true);

            if (Math.Abs(eixoMax - eixoMin) < 1e-6)
                return false;

            XYZ normal = ObterNormalAoEixoNaVista(view, eixo);
            double sinalLado = DefinirLadoDaCotaPorPonto(elementos, view, pontoLado, normal);
            double extremoNormal = ObterExtremoNaNormal(elementos, view, normal, sinalLado);
            double posicaoLinha = extremoNormal + sinalLado * OffsetLinha;

            XYZ pontoInicial = ConstruirPontoNoPlano(eixoMin - ExtensaoLinha, posicaoLinha, eixo, normal, pontoLado);
            XYZ pontoFinal = ConstruirPontoNoPlano(eixoMax + ExtensaoLinha, posicaoLinha, eixo, normal, pontoLado);

            if (pontoInicial.DistanceTo(pontoFinal) < 1e-6)
                return false;

            dadosLinha = new DadosLinhaCota(
                Line.CreateBound(pontoInicial, pontoFinal),
                eixo, normal, ObterNormalDaVista(view), pontoLado, sinalLado,
                eixoMin - ToleranciaIntervalo,
                eixoMax + ToleranciaIntervalo);

            return true;
        }

        private XYZ AjustarSentidoDoEixoPelaSelecao(List<Element> elementos, View view, XYZ eixoBase)
        {
            if (elementos.Count < 2) return eixoBase;

            XYZ? primeiro = ObterCentroDoElemento(elementos.First(), view);
            XYZ? ultimo = ObterCentroDoElemento(elementos.Last(), view);

            if (primeiro is null || ultimo is null) return eixoBase;

            double delta = ProjetarNoEixo(ultimo - primeiro, eixoBase);
            return delta >= 0 ? eixoBase : eixoBase.Negate();
        }

        private XYZ DeterminarEixoPrincipalDaSelecao(List<Element> elementos, View view)
        {
            BoundingBoxXYZ? caixa = ObterCaixaDosElementos(elementos, view);
            if (caixa is null) return ObterEixoHorizontalDaVista(view);

            double larguraHorizontal = Math.Abs(ProjetarNoEixo(caixa.Max - caixa.Min, ObterEixoHorizontalDaVista(view)));
            double larguraVertical = Math.Abs(ProjetarNoEixo(caixa.Max - caixa.Min, ObterEixoVerticalDaVista(view)));
            return larguraHorizontal >= larguraVertical
                ? ObterEixoHorizontalDaVista(view)
                : ObterEixoVerticalDaVista(view);
        }

        private bool eRepresentaEixoX(XYZ eixo)
        {
            XYZ eixoNormalizado = eixo.Normalize();
            return Math.Abs(eixoNormalizado.DotProduct(XYZ.BasisX)) >=
                   Math.Abs(eixoNormalizado.DotProduct(XYZ.BasisY));
        }

        private List<List<Element>> AgruparElementosPorAlinhamento(
            List<Element> elementos,
            View view,
            XYZ eixo)
        {
            XYZ normal = ObterNormalAoEixoNaVista(view, eixo);

            List<ElementoAgrupado> candidatos = elementos
                .Select(el => CriarElementoAgrupado(el, view, normal))
                .Where(x => x is not null)
                .Cast<ElementoAgrupado>()
                .OrderBy(x => x.CentroNormal)
                .ToList();

            List<List<Element>> grupos = new();
            List<Element> grupoAtual = new();
            double? faixaMaxAtual = null;

            foreach (ElementoAgrupado item in candidatos)
            {
                if (faixaMaxAtual is not null &&
                    item.FaixaMinNormal - faixaMaxAtual.Value > ToleranciaSeparacaoGrupos)
                {
                    if (grupoAtual.Count >= 2)
                        grupos.Add(grupoAtual);
                    grupoAtual = new();
                    faixaMaxAtual = null;
                }

                grupoAtual.Add(item.Elemento);
                faixaMaxAtual = faixaMaxAtual is null
                    ? item.FaixaMaxNormal
                    : Math.Max(faixaMaxAtual.Value, item.FaixaMaxNormal);
            }

            if (grupoAtual.Count >= 2)
                grupos.Add(grupoAtual);

            return grupos;
        }

        private ElementoAgrupado? CriarElementoAgrupado(Element elemento, View view, XYZ normal)
        {
            XYZ? centro = ObterCentroDoElemento(elemento, view);
            BoundingBoxXYZ? bbox = elemento.get_BoundingBox(view);
            if (centro is null || bbox is null)
                return null;

            List<double> projecoes = ObterCantosHorizontais(bbox)
                .Select(canto => ProjetarNoEixo(canto, normal))
                .ToList();

            if (projecoes.Count == 0)
                return null;

            return new ElementoAgrupado(
                elemento,
                centro,
                projecoes.Min(),
                projecoes.Max(),
                ProjetarNoEixo(centro, normal));
        }

        private List<ReferenciaCota> ColetarReferenciasDosEixos(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha)
        {
            List<ReferenciaCota> referencias = new();

            foreach (Element elemento in elementos)
                TentarAdicionarReferenciaDeEixo(doc, view, elemento, dadosLinha, referencias);

            return referencias;
        }

        private List<List<ReferenciaCota>> ColetarCandidatosDeReferenciasDosEixos(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha)
        {
            List<List<ReferenciaCota>> candidatos = new();

            foreach (FamilyInstanceReferenceType tipoReferencia in ObterTiposReferenciaDeEixo(dadosLinha.EixoDirecao, view).Distinct())
            {
                List<ReferenciaCota> referencias = new();
                foreach (Element elemento in elementos)
                    TentarAdicionarReferenciaDeEixo(doc, view, elemento, dadosLinha, referencias, tipoReferencia);

                referencias = RemoverDuplicadasPorPosicao(referencias);
                if (referencias.Count >= 2)
                    candidatos.Add(referencias);
            }

            List<ReferenciaCota> fallbackMisto = RemoverDuplicadasPorPosicao(ColetarReferenciasDosEixos(doc, view, elementos, dadosLinha));
            if (fallbackMisto.Count >= 2)
                candidatos.Add(fallbackMisto);

            List<ReferenciaCota> referenciasPorPonto = RemoverDuplicadasPorPosicao(ColetarReferenciasPorPonto(doc, view, elementos, dadosLinha));
            if (referenciasPorPonto.Count >= 2)
                candidatos.Add(referenciasPorPonto);

            return candidatos;
        }

        private List<ReferenciaCota> ColetarReferenciasPorPonto(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha)
        {
            List<ReferenciaCota> referencias = new();

            foreach (Element elemento in elementos)
                TentarAdicionarReferenciaPorPonto(doc, view, elemento, dadosLinha, referencias);

            return referencias;
        }

        private void TentarAdicionarReferenciaPorPonto(
            Document doc,
            View view,
            Element elemento,
            DadosLinhaCota dadosLinha,
            List<ReferenciaCota> referencias)
        {
            if (elemento is not FamilyInstance instancia)
                return;

            Reference? referencia = ObterReferenciaDePonto(instancia, view);
            if (referencia is null)
                return;

            XYZ? centro = ObterCentroDoElemento(elemento, view);
            if (centro is null)
                return;

            double posicao = ProjetarNoEixo(centro, dadosLinha.EixoDirecao);
            string chave = referencia.ConvertToStableRepresentation(doc);
            referencias.Add(new ReferenciaCota(referencia, posicao, chave, posicao, posicao, 0.0));
        }

        private Reference? ObterReferenciaDePonto(FamilyInstance instancia, View view)
        {
            Options opcoes = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                View = view
            };

            GeometryElement? geometria = instancia.get_Geometry(opcoes);
            return geometria is null
                ? null
                : ObterReferenciaDePontoRecursiva(geometria);
        }

        private Reference? ObterReferenciaDePontoRecursiva(IEnumerable<GeometryObject> objetos)
        {
            foreach (GeometryObject obj in objetos)
            {
                if (obj is Point ponto && ponto.Reference is not null)
                    return ponto.Reference;

                if (obj is GeometryInstance instancia)
                {
                    Reference? referencia = ObterReferenciaDePontoRecursiva(instancia.GetSymbolGeometry());
                    if (referencia is not null)
                        return referencia;
                }
            }

            return null;
        }

        private List<ReferenciaCota> ColetarReferenciasDaLinha(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha)
        {
            Options opcoes = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = view
            };

            List<ReferenciaCota> referencias = new();

            foreach (Element elemento in elementos)
            {
                GeometryElement? geo = elemento.get_Geometry(opcoes);
                if (geo is null)
                {
                    TentarAdicionarReferenciaDeEixo(doc, view, elemento, dadosLinha, referencias);
                    continue;
                }

                List<ReferenciaCota> referenciasDoElemento = new();

                ColetarReferenciasRecursivo(
                    doc,
                    view,
                    geo,
                    Transform.Identity,
                    dadosLinha,
                    referenciasDoElemento);

                if (referenciasDoElemento.Count == 0)
                    TentarAdicionarReferenciaDeEixo(doc, view, elemento, dadosLinha, referenciasDoElemento);

                referencias.AddRange(referenciasDoElemento);
            }

            return referencias;
        }

        private void TentarAdicionarReferenciaDeEixo(
            Document doc,
            View view,
            Element elemento,
            DadosLinhaCota dadosLinha,
            List<ReferenciaCota> referencias)
        {
            if (elemento is not FamilyInstance instancia)
                return;

            Reference? referencia = null;
            foreach (FamilyInstanceReferenceType tipoReferencia in ObterTiposReferenciaDeEixo(dadosLinha.EixoDirecao, view))
            {
                IList<Reference> refs = instancia.GetReferences(tipoReferencia);
                if (refs is not null && refs.Count > 0)
                {
                    referencia = refs[0];
                    break;
                }
            }

            if (referencia is null)
                return;

            XYZ? centro = ObterCentroDoElemento(elemento, view);
            if (centro is null)
                return;

            double posicao = ProjetarNoEixo(centro, dadosLinha.EixoDirecao);
            string chave = referencia.ConvertToStableRepresentation(doc);
            referencias.Add(new ReferenciaCota(referencia, posicao, chave, posicao, posicao, 0.0));
        }

        private void TentarAdicionarReferenciaDeEixo(
            Document doc,
            View view,
            Element elemento,
            DadosLinhaCota dadosLinha,
            List<ReferenciaCota> referencias,
            FamilyInstanceReferenceType tipoReferencia)
        {
            if (elemento is not FamilyInstance instancia)
                return;

            IList<Reference> refs = instancia.GetReferences(tipoReferencia);
            if (refs is null || refs.Count == 0)
                return;

            XYZ? centro = ObterCentroDoElemento(elemento, view);
            if (centro is null)
                return;

            double posicao = ProjetarNoEixo(centro, dadosLinha.EixoDirecao);
            string chave = refs[0].ConvertToStableRepresentation(doc);
            referencias.Add(new ReferenciaCota(refs[0], posicao, chave, posicao, posicao, 0.0));
        }

        private IEnumerable<FamilyInstanceReferenceType> ObterTiposReferenciaDeEixo(XYZ eixoDirecao, View view)
        {
            XYZ eixoNormalizado = eixoDirecao.Normalize();
            XYZ horizontal = ObterEixoHorizontalDaVista(view);
            XYZ vertical = ObterEixoVerticalDaVista(view);

            double alinhamentoHorizontal = Math.Abs(eixoNormalizado.DotProduct(horizontal));
            double alinhamentoVertical = Math.Abs(eixoNormalizado.DotProduct(vertical));

            if (view is ViewPlan)
            {
                if (alinhamentoHorizontal >= alinhamentoVertical)
                {
                    yield return FamilyInstanceReferenceType.CenterLeftRight;
                    yield return FamilyInstanceReferenceType.CenterFrontBack;
                }
                else
                {
                    yield return FamilyInstanceReferenceType.CenterFrontBack;
                    yield return FamilyInstanceReferenceType.CenterLeftRight;
                }

                yield break;
            }

            if (alinhamentoHorizontal >= alinhamentoVertical)
            {
                yield return FamilyInstanceReferenceType.CenterLeftRight;
                yield return FamilyInstanceReferenceType.CenterFrontBack;
                yield return FamilyInstanceReferenceType.CenterElevation;
            }
            else
            {
                yield return FamilyInstanceReferenceType.CenterElevation;
                yield return FamilyInstanceReferenceType.CenterLeftRight;
                yield return FamilyInstanceReferenceType.CenterFrontBack;
            }
        }

        private bool TentarCriarDimensao(
            Document doc,
            View view,
            List<Element> elementos,
            Line linhaOriginal,
            string titulo,
            List<ReferenciaCota> referencias,
            out Dimension? dimensao,
            out string? erro)
        {
            dimensao = null;
            erro = null;

            ReferenceArray refArray = new ReferenceArray();
            foreach (ReferenciaCota ref_ in referencias.OrderBy(r => r.Posicao))
                refArray.Append(ref_.Referencia);

            Line linhaFinal = AjustarZDaLinhaDeCota(linhaOriginal, elementos, view);

            try
            {
                using (Transaction t = new Transaction(doc, titulo))
                {
                    t.Start();

                    Dimension dimProvisoria = doc.Create.NewDimension(view, linhaFinal, refArray);
                    ReferenceArray refsValidadas = dimProvisoria.References;
                    doc.Delete(dimProvisoria.Id);
                    dimensao = doc.Create.NewDimension(view, linhaFinal, refsValidadas);

                    t.Commit();
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "CotasService.TentarCriarDimensao: erro ao criar dimensão");
                erro = ex.Message;
                return false;
            }
        }

        private bool TentarCriarDimensaoPorPlanosAuxiliares(
            Document doc,
            View view,
            List<Element> elementos,
            DadosLinhaCota dadosLinha,
            string titulo,
            out Dimension? dimensao,
            out string? erro)
        {
            dimensao = null;
            erro = null;

            List<(Element Elemento, XYZ Centro)> centros = elementos
                .Select(el => new { Elemento = el, Centro = ObterCentroDoElemento(el, view) })
                .Where(x => x.Centro is not null)
                .Select(x => (x.Elemento, x.Centro!))
                .OrderBy(x => ProjetarNoEixo(x.Item2, dadosLinha.EixoDirecao))
                .ToList();

            if (centros.Count < 2)
            {
                erro = "não foi possível determinar centros suficientes para os elementos.";
                return false;
            }

            double comprimentoAuxiliar = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
            List<ElementId> idsAuxiliares = new();
            ReferenceArray refArray = new ReferenceArray();

            try
            {
                using (Transaction t = new Transaction(doc, $"{titulo} - Eixos Auxiliares"))
                {
                    t.Start();

                    foreach ((Element Elemento, XYZ Centro) item in centros)
                    {
                        XYZ centro = item.Centro;
                        XYZ bubbleEnd = centro - dadosLinha.NormalDirecao.Multiply(comprimentoAuxiliar);
                        XYZ freeEnd = centro + dadosLinha.NormalDirecao.Multiply(comprimentoAuxiliar);
                        XYZ thirdPoint = centro + dadosLinha.NormalVista.Multiply(comprimentoAuxiliar);

                        ReferencePlane plano = doc.Create.NewReferencePlane2(bubbleEnd, freeEnd, thirdPoint, view);
                        plano.Name = $"EMT_EIXO_{item.Elemento.Id.Value}";
                        idsAuxiliares.Add(plano.Id);
                        refArray.Append(plano.GetReference());
                    }

                    if (refArray.Size < 2)
                    {
                        t.RollBack();
                        erro = "as referências auxiliares criadas foram insuficientes.";
                        return false;
                    }

                    Line linhaFinal = AjustarZDaLinhaDeCota(dadosLinha.Linha, elementos, view);
                    Dimension dimProvisoria = doc.Create.NewDimension(view, linhaFinal, refArray);
                    ReferenceArray refsValidadas = dimProvisoria.References;
                    doc.Delete(dimProvisoria.Id);
                    dimensao = doc.Create.NewDimension(view, linhaFinal, refsValidadas);

                    if (idsAuxiliares.Count > 0)
                        view.HideElements(idsAuxiliares);

                    t.Commit();
                }

                return dimensao is not null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "CotasService.TentarCriarDimensaoPorPlanosAuxiliares: erro ao criar dimensão por planos auxiliares");
                erro = ex.Message;
                dimensao = null;
                return false;
            }
        }

        private void ColetarReferenciasRecursivo(
            Document doc,
            View view,
            IEnumerable<GeometryObject> objetos,
            Transform transformAcumulada,
            DadosLinhaCota dadosLinha,
            List<ReferenciaCota> referencias)
        {
            foreach (GeometryObject obj in objetos)
            {
                if (obj is GeometryInstance instancia)
                {
                    // Para dimensionamento, precisamos preservar as referências geométricas originais.
                    // GetInstanceGeometry() retorna cópias da geometria transformada, que podem falhar
                    // no NewDimension. GetSymbolGeometry() mantém as referências válidas; aplicamos a
                    // transformação acumulada manualmente para avaliar posição/direção no modelo.
                    Transform proximoTransform = transformAcumulada.Multiply(instancia.Transform);
                    ColetarReferenciasRecursivo(
                        doc,
                        view,
                        instancia.GetSymbolGeometry(),
                        proximoTransform,
                        dadosLinha, referencias);
                    continue;
                }

                if (obj is Solid solid && !solid.Faces.IsEmpty)
                {
                    ProcessarSolid(doc, view, solid, transformAcumulada, dadosLinha, referencias);
                }
            }
        }

        private void ProcessarSolid(
            Document doc,
            View view,
            Solid solid,
            Transform transform,
            DadosLinhaCota dadosLinha,
            List<ReferenciaCota> referencias)
        {
            foreach (Face face in solid.Faces)
            {
                if (face is not PlanarFace fp)
                    continue;

                if (!FaceServeParaCota(fp, transform, dadosLinha))
                    continue;

                List<XYZ> vertices = ObterVerticesDaFace(fp, transform);
                if (vertices.Count == 0) continue;

                double faceMin = vertices.Min(v => ProjetarNoEixo(v, dadosLinha.EixoDirecao));
                double faceMax = vertices.Max(v => ProjetarNoEixo(v, dadosLinha.EixoDirecao));

                if (faceMax < dadosLinha.EixoMin || faceMin > dadosLinha.EixoMax)
                    continue;

                XYZ normalModelo = transform.OfVector(fp.FaceNormal);
                XYZ normal2D = ProjetarVetornoPlanoDaVista(normalModelo, dadosLinha.NormalVista);
                if (normal2D.GetLength() < 1e-9)
                    continue;

                normal2D = normal2D.Normalize();
                double sentidoNoEixo = normal2D.DotProduct(dadosLinha.EixoDirecao);

                // A API exige REFERENCE_TYPE_LINEAR — face.Reference é REFERENCE_TYPE_SURFACE e é rejeitada.
                // Usamos arestas (Edge.Reference) cujas direções são paralelas à normal da vista,
                // pois essas arestas aparecem como linhas na planta/corte e geram referências lineares válidas.
                foreach (EdgeArray loop in fp.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        if (edge.Reference is null) continue;

                        Curve curva = edge.AsCurve();
                        if (curva is not Line linhaAresta) continue;

                        XYZ direcao = transform.OfVector(linhaAresta.Direction).Normalize();
                        if (!ArestaServeComoReferencia(direcao, dadosLinha))
                            continue;

                        XYZ pontoMedio = transform.OfPoint(curva.Evaluate(0.5, true));
                        double posicao = ProjetarNoEixo(pontoMedio, dadosLinha.EixoDirecao);

                        if (posicao < dadosLinha.EixoMin || posicao > dadosLinha.EixoMax)
                            continue;

                        string chave = edge.Reference.ConvertToStableRepresentation(doc);
                        referencias.Add(new ReferenciaCota(edge.Reference, posicao, chave, faceMin, faceMax, sentidoNoEixo));
                    }
                }
            }
        }

        private bool FaceServeParaCota(PlanarFace face, Transform transform, DadosLinhaCota dadosLinha)
        {
            XYZ normalModelo = transform.OfVector(face.FaceNormal);
            XYZ normal2D = ProjetarVetornoPlanoDaVista(normalModelo, dadosLinha.NormalVista);

            if (normal2D.GetLength() < 1e-9)
                return false;

            normal2D = normal2D.Normalize();
            return Math.Abs(normal2D.DotProduct(dadosLinha.EixoDirecao)) > 0.5;
        }

        private bool ArestaServeComoReferencia(XYZ direcaoAresta, DadosLinhaCota dadosLinha)
        {
            // Em planta, arestas paralelas à normal da vista continuam sendo úteis.
            if (Math.Abs(direcaoAresta.DotProduct(dadosLinha.NormalVista)) >= 0.7)
                return true;

            // Em cortes/elevações, também aceitamos arestas visíveis no plano da vista
            // cuja projeção fique alinhada com a normal da linha de cota.
            XYZ direcaoProjetada = ProjetarVetornoPlanoDaVista(direcaoAresta, dadosLinha.NormalVista);
            if (direcaoProjetada.GetLength() < 1e-9)
                return false;

            direcaoProjetada = direcaoProjetada.Normalize();
            return Math.Abs(direcaoProjetada.DotProduct(dadosLinha.NormalDirecao)) >= 0.7;
        }

        private List<XYZ> ObterVerticesDaFace(PlanarFace face, Transform transform)
        {
            List<XYZ> vertices = new();
            foreach (EdgeArray loop in face.EdgeLoops)
                foreach (Edge edge in loop)
                    foreach (XYZ pt in edge.Tessellate())
                        vertices.Add(transform.OfPoint(pt));
            return vertices;
        }

        private double DefinirLadoDaCota(
            List<Element> elementos,
            View view,
            XYZ p1,
            XYZ p2,
            XYZ normal)
        {
            XYZ centroElementos = ObterCentroDosElementos(elementos, view);
            XYZ meioPonto = (p1 + p2) * 0.5;
            double delta = ProjetarNoEixo(meioPonto - centroElementos, normal);
            return delta >= 0 ? 1.0 : -1.0;
        }

        private double ObterExtremoNaNormal(
            List<Element> elementos,
            View view,
            XYZ normal,
            double sinalLado)
        {
            double? extremo = null;

            foreach (Element el in elementos)
            {
                BoundingBoxXYZ? bbox = el.get_BoundingBox(view);
                if (bbox is null) continue;

                foreach (XYZ canto in ObterCantosHorizontais(bbox))
                {
                    double proj = ProjetarNoEixo(canto, normal);
                    extremo = extremo is null
                        ? proj
                        : sinalLado > 0
                            ? Math.Max(extremo.Value, proj)
                            : Math.Min(extremo.Value, proj);
                }
            }

            return extremo ?? 0.0;
        }

        private double DefinirLadoDaCotaPorPonto(
            List<Element> elementos,
            View view,
            XYZ pontoLado,
            XYZ normal)
        {
            XYZ centroElementos = ObterCentroDosElementos(elementos, view);
            double delta = ProjetarNoEixo(pontoLado - centroElementos, normal);
            return delta >= 0 ? 1.0 : -1.0;
        }

        private double ObterExtremoNaDirecao(
            List<Element> elementos,
            View view,
            XYZ direcao,
            bool pegarMaximo)
        {
            double? extremo = null;

            foreach (Element el in elementos)
            {
                BoundingBoxXYZ? bbox = el.get_BoundingBox(view);
                if (bbox is null) continue;

                foreach (XYZ canto in ObterCantosHorizontais(bbox))
                {
                    double proj = ProjetarNoEixo(canto, direcao);
                    extremo = extremo is null
                        ? proj
                        : pegarMaximo
                            ? Math.Max(extremo.Value, proj)
                            : Math.Min(extremo.Value, proj);
                }
            }

            return extremo ?? 0.0;
        }

        private XYZ ObterCentroDosElementos(List<Element> elementos, View view)
        {
            BoundingBoxXYZ? caixa = ObterCaixaDosElementos(elementos, view);
            if (caixa is null) return XYZ.Zero;

            return new XYZ(
                (caixa.Min.X + caixa.Max.X) * 0.5,
                (caixa.Min.Y + caixa.Max.Y) * 0.5,
                (caixa.Min.Z + caixa.Max.Z) * 0.5);
        }

        private XYZ? ObterCentroDoElemento(Element elemento, View view)
        {
            BoundingBoxXYZ? bbox = elemento.get_BoundingBox(view);
            if (bbox is null) return null;

            return new XYZ(
                (bbox.Min.X + bbox.Max.X) * 0.5,
                (bbox.Min.Y + bbox.Max.Y) * 0.5,
                (bbox.Min.Z + bbox.Max.Z) * 0.5);
        }

        private BoundingBoxXYZ? ObterCaixaDosElementos(List<Element> elementos, View view)
        {
            BoundingBoxXYZ? caixa = null;

            foreach (Element el in elementos)
            {
                BoundingBoxXYZ? bbox = el.get_BoundingBox(view);
                if (bbox is null) continue;

                if (caixa is null)
                {
                    caixa = new BoundingBoxXYZ { Min = bbox.Min, Max = bbox.Max };
                    continue;
                }

                caixa.Min = new XYZ(
                    Math.Min(caixa.Min.X, bbox.Min.X),
                    Math.Min(caixa.Min.Y, bbox.Min.Y),
                    Math.Min(caixa.Min.Z, bbox.Min.Z));

                caixa.Max = new XYZ(
                    Math.Max(caixa.Max.X, bbox.Max.X),
                    Math.Max(caixa.Max.Y, bbox.Max.Y),
                    Math.Max(caixa.Max.Z, bbox.Max.Z));
            }

            return caixa;
        }

        // Vigas (StructuralFraming) são modeladas abaixo do plano de corte da planta.
        // A API exige que a linha de cota tenha o mesmo Z das arestas referenciadas;
        // caso contrário lança "The direction of dimension is invalid".
        // Este método projeta a linha para o Z médio dos elementos de estrutura horizontal.
        private Line AjustarZDaLinhaDeCota(Line linhaOriginal, List<Element> elementos, View view)
        {
            if (view is not ViewPlan)
                return linhaOriginal;

            List<double> zsVigas = elementos
                .Where(el => el.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralFraming)
                .Select(el => el.get_BoundingBox(view))
                .Where(bb => bb is not null)
                .Select(bb => (bb!.Min.Z + bb.Max.Z) * 0.5)
                .ToList();

            if (zsVigas.Count == 0)
                return linhaOriginal;

            double zAlvo = zsVigas.Average();
            XYZ p0 = linhaOriginal.GetEndPoint(0);
            XYZ p1 = linhaOriginal.GetEndPoint(1);

            if (Math.Abs(p0.Z - zAlvo) < 1e-6)
                return linhaOriginal;

            XYZ p0aj = new XYZ(p0.X, p0.Y, zAlvo);
            XYZ p1aj = new XYZ(p1.X, p1.Y, zAlvo);

            return p0aj.DistanceTo(p1aj) < 1e-6 ? linhaOriginal : Line.CreateBound(p0aj, p1aj);
        }

        private double ProjetarNoEixo(XYZ ponto, XYZ eixo) =>
            ponto.DotProduct(eixo);

        private XYZ ConstruirPontoNoPlano(
            double aoLongoDoEixo,
            double aoLongoDaNormal,
            XYZ eixo,
            XYZ normal,
            XYZ origemPlano)
        {
            double origemEixo = ProjetarNoEixo(origemPlano, eixo);
            double origemNormal = ProjetarNoEixo(origemPlano, normal);

            return origemPlano
                + eixo.Multiply(aoLongoDoEixo - origemEixo)
                + normal.Multiply(aoLongoDaNormal - origemNormal);
        }

        private IEnumerable<XYZ> ObterCantosHorizontais(BoundingBoxXYZ bbox)
        {
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Max.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
            yield return new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Max.Z);
        }

        private bool VistaSuportada(View view) =>
            view is ViewPlan || view is ViewSection;

        private XYZ ObterEixoHorizontalDaVista(View view) => view.RightDirection.Normalize();

        private XYZ ObterEixoVerticalDaVista(View view) => view.UpDirection.Normalize();

        private XYZ ObterNormalDaVista(View view) => view.ViewDirection.Normalize();

        private XYZ ObterNormalAoEixoNaVista(View view, XYZ eixo)
        {
            XYZ eixoHorizontal = ObterEixoHorizontalDaVista(view);
            XYZ eixoVertical = ObterEixoVerticalDaVista(view);

            double alinhamentoHorizontal = Math.Abs(eixo.DotProduct(eixoHorizontal));
            double alinhamentoVertical = Math.Abs(eixo.DotProduct(eixoVertical));

            return alinhamentoHorizontal >= alinhamentoVertical ? eixoVertical : eixoHorizontal;
        }

        private XYZ ProjetarVetornoPlanoDaVista(XYZ vetor, XYZ normalVista) =>
            vetor - normalVista.Multiply(vetor.DotProduct(normalVista));

        private List<ReferenciaCota> RemoverDuplicadasPorPosicao(List<ReferenciaCota> todas)
        {
            List<ReferenciaCota> ordenadas = todas.OrderBy(r => r.Posicao).ToList();
            List<ReferenciaCota> unicas = new();
            HashSet<string> chaves = new(StringComparer.Ordinal);

            foreach (ReferenciaCota ref_ in ordenadas)
            {
                if (!chaves.Add(ref_.Chave)) continue;

                if (unicas.Count > 0 &&
                    Math.Abs(unicas[^1].Posicao - ref_.Posicao) <= ToleranciaDeduplicacao)
                    continue;

                unicas.Add(ref_);
            }

            return unicas;
        }

        private bool EhElementoCotavel(Element el) =>
            el.Category is not null &&
            !el.ViewSpecific &&
            el.get_BoundingBox(null) is not null;

        private enum ModoCota { Faces, Eixos }

        private sealed class DadosLinhaCota
        {
            public DadosLinhaCota(
                Line linha,
                XYZ eixoDirecao,
                XYZ normalDirecao,
                XYZ normalVista,
                XYZ origemPlano,
                double sinalLado,
                double eixoMin,
                double eixoMax)
            {
                Linha = linha;
                EixoDirecao = eixoDirecao;
                NormalDirecao = normalDirecao;
                NormalVista = normalVista;
                OrigemPlano = origemPlano;
                SinalLado = sinalLado;
                EixoMin = eixoMin;
                EixoMax = eixoMax;
            }

            public Line Linha { get; }
            public XYZ EixoDirecao { get; }
            public XYZ NormalDirecao { get; }
            public XYZ NormalVista { get; }
            public XYZ OrigemPlano { get; }
            public double SinalLado { get; }
            public double EixoMin { get; }
            public double EixoMax { get; }
        }

        private sealed class ReferenciaCota
        {
            public ReferenciaCota(
                Reference referencia,
                double posicao,
                string chave,
                double faceMin,
                double faceMax,
                double sentidoNoEixo)
            {
                Referencia = referencia;
                Posicao = posicao;
                Chave = chave;
                FaceMin = faceMin;
                FaceMax = faceMax;
                SentidoNoEixo = sentidoNoEixo;
            }

            public Reference Referencia { get; }
            public double Posicao { get; }
            public string Chave { get; }
            public double FaceMin { get; }
            public double FaceMax { get; }
            public double SentidoNoEixo { get; }
        }

        private sealed class ElementoAgrupado
        {
            public ElementoAgrupado(
                Element elemento,
                XYZ centro,
                double faixaMinNormal,
                double faixaMaxNormal,
                double centroNormal)
            {
                Elemento = elemento;
                Centro = centro;
                FaixaMinNormal = faixaMinNormal;
                FaixaMaxNormal = faixaMaxNormal;
                CentroNormal = centroNormal;
            }

            public Element Elemento { get; }
            public XYZ Centro { get; }
            public double FaixaMinNormal { get; }
            public double FaixaMaxNormal { get; }
            public double CentroNormal { get; }
        }

        private sealed class FiltroSelecaoElementos : ISelectionFilter
        {
            public bool AllowElement(Element elem) =>
                elem.Category is not null &&
                !elem.ViewSpecific &&
                elem.get_BoundingBox(null) is not null;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
