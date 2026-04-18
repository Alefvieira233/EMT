using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Services
{
    public class NumeracaoItensService
    {
        private const string Titulo = "Numerar Itens";
        private static NumeracaoItensSessao _sessaoAtiva;

        /// <summary>
        /// Resultado do kickoff da sessao de numeracao. ADR-003: o servico nao fala
        /// com o usuario, retorna o que aconteceu e o comando decide a UX.
        /// </summary>
        public sealed class InicioResultado
        {
            /// <summary>Sessao nova foi iniciada (<c>true</c>) ou a chamada foi "no-op"
            /// por conta de uma sessao ja ativa / nenhum elemento elegivel.</summary>
            public bool SessaoIniciada { get; set; }

            /// <summary>Usuario ja tinha uma sessao aberta — a janela dessa sessao foi
            /// trazida pra frente. Comando deve avisar o usuario.</summary>
            public bool JaHaviaSessaoAtiva { get; set; }

            /// <summary>Total de candidatos coletados antes dos filtros de config.</summary>
            public int TotalCandidatos { get; set; }

            /// <summary>Total de elementos que sobraram apos filtros — se zero, a sessao
            /// NAO foi iniciada.</summary>
            public int TotalElegiveis { get; set; }
        }

        /// <summary>
        /// Kickoff de uma sessao interativa de numeracao. ADR-003: retorna
        /// <see cref="Core.Result{T}"/> com o estado — comando faz todos os dialogs
        /// (ShowInfo/ShowWarning). O ShowInfo final de "processamento concluido" fica
        /// dentro da sessao porque pertence ao lifecycle da janela persistente, nao
        /// ao kickoff.
        /// </summary>
        public Core.Result<InicioResultado> IniciarSessao(UIApplication uiapp, UIDocument uidoc, NumeracaoItensConfig config)
        {
            if (uidoc is null)
            {
                Logger.Error("[NumeracaoItens] IniciarSessao recebeu UIDocument nulo.");
                return Core.Result<InicioResultado>.Fail("UIDocument nulo.");
            }

            if (config is null)
            {
                Logger.Warn("[NumeracaoItens] IniciarSessao recebeu config nula.");
                return Core.Result<InicioResultado>.Fail("Configuração inválida.");
            }

            if (_sessaoAtiva != null && !_sessaoAtiva.IsFinalizada)
            {
                _sessaoAtiva.AtivarJanela();
                Logger.Info("[NumeracaoItens] Sessao ja ativa — janela trazida para frente.");
                return Core.Result<InicioResultado>.Ok(new InicioResultado
                {
                    SessaoIniciada = false,
                    JaHaviaSessaoAtiva = true,
                });
            }

            List<NumeracaoElementoInfo> candidatos = NumeracaoItensCatalog.ColetarCandidatos(uidoc, config.Escopo);
            List<NumeracaoElementoInfo> elegiveis = NumeracaoItensCatalog.Filtrar(candidatos, config);

            if (elegiveis.Count == 0)
            {
                Logger.Info($"[NumeracaoItens] Nenhum elemento elegivel ({candidatos.Count} candidatos, filtros descartaram todos).");
                return Core.Result<InicioResultado>.Ok(new InicioResultado
                {
                    SessaoIniciada = false,
                    TotalCandidatos = candidatos.Count,
                    TotalElegiveis = 0,
                });
            }

            _sessaoAtiva = new NumeracaoItensSessao(uidoc, config, elegiveis, LimparSessaoAtiva);
            _sessaoAtiva.Iniciar();
            Logger.Info($"[NumeracaoItens] Sessao iniciada com {elegiveis.Count} elementos elegiveis.");
            return Core.Result<InicioResultado>.Ok(new InicioResultado
            {
                SessaoIniciada = true,
                TotalCandidatos = candidatos.Count,
                TotalElegiveis = elegiveis.Count,
            });
        }

        private static void LimparSessaoAtiva(NumeracaoItensSessao sessao)
        {
            if (ReferenceEquals(_sessaoAtiva, sessao))
                _sessaoAtiva = null;
        }

        internal sealed class NumeracaoItensSessao
        {
            private readonly UIDocument _uidoc;
            private readonly Document _doc;
            private readonly View _view;
            private readonly NumeracaoItensConfig _config;
            private readonly HashSet<long> _idsElegiveis;
            private readonly HashSet<long> _idsProcessados = new HashSet<long>();
            private readonly List<RegistroNumeracao> _historico = new List<RegistroNumeracao>();
            private readonly Dictionary<long, OverrideGraphicSettings> _overridesOriginais =
                new Dictionary<long, OverrideGraphicSettings>();
            private readonly Action<NumeracaoItensSessao> _aoFinalizar;

            private NumeracaoItensControleWindow _janela;
            private NumeracaoItensExternalEventHandler _handler;
            private ExternalEvent _externalEvent;
            private bool _fechandoJanelaProgramaticamente;
            private bool _encerrarAposPausa;
            private int _numeroAtual;
            private string _status = "Preparando sessão...";

            public NumeracaoItensSessao(
                UIDocument uidoc,
                NumeracaoItensConfig config,
                List<NumeracaoElementoInfo> elegiveis,
                Action<NumeracaoItensSessao> aoFinalizar)
            {
                _uidoc = uidoc;
                _doc = uidoc.Document;
                _view = _doc.ActiveView;
                // Guard: sessao de numeracao exige vista ativa (usa overrides graficos + RefreshActiveView).
                // Fail-fast com mensagem clara em vez de NRE em linhas 330/345/539.
                if (_view == null)
                    throw new InvalidOperationException(
                        "Não há vista ativa. Abra uma vista antes de iniciar a numeração de itens.");
                _config = config;
                _idsElegiveis = elegiveis.Select(x => x.Id.Value).ToHashSet();
                _aoFinalizar = aoFinalizar;
                _numeroAtual = config.Inicio;
            }

            public bool IsFinalizada { get; private set; }
            public bool IsSelecionando { get; private set; }
            public bool AutoIncremento { get; private set; } = true;
            public int TotalElegiveis => _idsElegiveis.Count;
            public int TotalProcessados => _historico.Count;

            public void Iniciar()
            {
                _handler = new NumeracaoItensExternalEventHandler(this);
                _externalEvent = ExternalEvent.Create(_handler);
                _janela = new NumeracaoItensControleWindow(this);
                _janela.Show();

                AtualizarStatus("Seleção ativa. Clique nos elementos em sequência. ESC pausa a sessão.");
                SolicitarRetomar();
            }

            public void AtivarJanela()
            {
                if (_janela == null || IsFinalizada)
                    return;

                _janela.Activate();
            }

            public void AjustarNumero(int direcao)
            {
                if (IsFinalizada)
                    return;

                _numeroAtual += direcao * _config.Degrau;
                AtualizarStatus(direcao >= 0
                    ? "Numeração avançada."
                    : "Numeração retrocedida.");
            }

            public void DefinirAutoIncremento(bool valor)
            {
                if (IsFinalizada)
                    return;

                AutoIncremento = valor;
                AtualizarStatus(valor
                    ? "Incremento automático ligado."
                    : "Incremento automático desligado.");
            }

            public void SolicitarRetomar()
            {
                if (IsFinalizada)
                    return;

                if (IsSelecionando)
                {
                    AtualizarStatus("A seleção já está ativa.");
                    return;
                }

                Enfileirar(RequisicaoSessao.RetomarSelecao);
            }

            public void SolicitarDesfazer()
            {
                if (IsFinalizada)
                    return;

                if (IsSelecionando)
                {
                    AtualizarStatus("Pressione ESC para pausar e então desfazer o último item.");
                    return;
                }

                Enfileirar(RequisicaoSessao.DesfazerUltimo);
            }

            public void SolicitarConcluir()
            {
                if (IsFinalizada)
                    return;

                if (IsSelecionando)
                {
                    _encerrarAposPausa = true;
                    AtualizarStatus("Sessão marcada para encerrar. Pressione ESC para concluir imediatamente.");
                    return;
                }

                Enfileirar(RequisicaoSessao.Concluir);
            }

            public void SolicitarFechamentoPelaJanela()
            {
                if (IsFinalizada)
                    return;

                SolicitarConcluir();
            }

            public bool PodeFecharJanela()
            {
                return IsFinalizada || _fechandoJanelaProgramaticamente;
            }

            internal void ExecutarRequisicao(UIApplication uiapp, RequisicaoSessao requisicao)
            {
                switch (requisicao)
                {
                    case RequisicaoSessao.RetomarSelecao:
                        ExecutarSelecaoContinua();
                        break;

                    case RequisicaoSessao.DesfazerUltimo:
                        DesfazerUltimo();
                        break;

                    case RequisicaoSessao.Concluir:
                        FinalizarSessao();
                        break;
                }
            }

            internal void AtualizarJanela()
            {
                if (_janela == null)
                    return;

                string valorAtual = ObterValorPreview(_config, _numeroAtual);
                string proximoAutomatico = ObterValorPreview(_config, _numeroAtual + _config.Degrau);

                _janela.AtualizarEstado(
                    valorAtual,
                    proximoAutomatico,
                    _config.ParametroNome,
                    _status,
                    TotalProcessados,
                    TotalElegiveis,
                    AutoIncremento,
                    IsSelecionando,
                    _historico.Count > 0,
                    !_encerrarAposPausa);
            }

            private void ExecutarSelecaoContinua()
            {
                if (IsFinalizada || IsSelecionando)
                    return;

                if (_idsProcessados.Count >= _idsElegiveis.Count)
                {
                    FinalizarSessao();
                    return;
                }

                IsSelecionando = true;
                AtualizarStatus("Seleção ativa. Clique nos elementos em sequência. ESC pausa a sessão.");

                bool pausadoPorEsc = false;

                try
                {
                    while (!IsFinalizada && !_encerrarAposPausa && _idsProcessados.Count < _idsElegiveis.Count)
                    {
                        Reference referencia = _uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new FiltroSelecaoNumeracao(_idsElegiveis, _idsProcessados),
                            $"Selecione o elemento para '{ObterValorPreview(_config, _numeroAtual)}'. ESC pausa.");

                        Element elemento = _doc.GetElement(referencia.ElementId);
                        if (elemento == null)
                            continue;

                        Parameter parametro = NumeracaoItensCatalog.EncontrarParametro(
                            elemento,
                            _config.ParametroChave,
                            _config.ParametroStorageType);

                        if (parametro == null)
                        {
                            AtualizarStatus("O parâmetro escolhido não foi encontrado no elemento selecionado.");
                            continue;
                        }

                        try
                        {
                            AplicarNumeracao(elemento, parametro, _numeroAtual);
                        }
                        catch (Exception ex)
                        {
                            AtualizarStatus($"Falha ao gravar a numeração: {ex.Message}");
                            continue;
                        }

                        if (AutoIncremento)
                            _numeroAtual += _config.Degrau;

                        AtualizarStatus($"{TotalProcessados} de {TotalElegiveis} itens numerados em '{_config.ParametroNome}'.");
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    pausadoPorEsc = true;
                }
                finally
                {
                    IsSelecionando = false;
                    AtualizarJanela();
                }

                if (_idsProcessados.Count >= _idsElegiveis.Count || _encerrarAposPausa)
                {
                    FinalizarSessao();
                    return;
                }

                if (pausadoPorEsc)
                    AtualizarStatus("Seleção pausada. Ajuste a numeração e clique em Retomar quando quiser continuar.");
            }

            private void AplicarNumeracao(Element elemento, Parameter parametro, int numero)
            {
                string valorAnteriorTexto = parametro.StorageType == StorageType.String
                    ? parametro.AsString() ?? string.Empty
                    : string.Empty;

                int valorAnteriorInteiro = parametro.StorageType == StorageType.Integer
                    ? parametro.AsInteger()
                    : 0;

                using (Transaction t = new Transaction(_doc, "Numerar item"))
                {
                    t.Start();
                    ConfigurarTratamentoDeFalhas(t);

                    if (!_overridesOriginais.ContainsKey(elemento.Id.Value))
                        _overridesOriginais[elemento.Id.Value] = _view.GetElementOverrides(elemento.Id);

                    if (!DefinirValorParametro(parametro, _config, numero))
                    {
                        t.RollBack();
                        throw new InvalidOperationException("o Revit rejeitou a escrita do valor no parâmetro.");
                    }

                    _doc.Regenerate();
                    if (!ParametroRecebeuValorEsperado(parametro, _config, numero))
                    {
                        t.RollBack();
                        throw new InvalidOperationException("o valor não foi confirmado no parâmetro após a gravação.");
                    }

                    _view.SetElementOverrides(elemento.Id, CriarOverrideDestaque());

                    t.Commit();
                }

                _uidoc.RefreshActiveView();

                _historico.Add(new RegistroNumeracao(
                    elemento.Id,
                    numero,
                    valorAnteriorTexto,
                    valorAnteriorInteiro,
                    parametro.StorageType));

                _idsProcessados.Add(elemento.Id.Value);
                AtualizarSelecao(_uidoc, _idsProcessados);
            }

            private void DesfazerUltimo()
            {
                if (IsFinalizada)
                    return;

                if (_historico.Count == 0)
                {
                    AtualizarStatus("Ainda não há itens numerados para desfazer.");
                    return;
                }

                RegistroNumeracao ultimo = _historico[_historico.Count - 1];
                Element elemento = _doc.GetElement(ultimo.ElementId);
                if (elemento == null)
                    return;

                Parameter parametro = NumeracaoItensCatalog.EncontrarParametro(
                    elemento,
                    _config.ParametroChave,
                    ultimo.StorageType);

                if (parametro == null)
                    return;

                using (Transaction t = new Transaction(_doc, "Desfazer numeração"))
                {
                    t.Start();
                    ConfigurarTratamentoDeFalhas(t);

                    if (ultimo.StorageType == StorageType.String)
                    {
                        if (!parametro.Set(ultimo.ValorAnteriorTexto ?? string.Empty))
                        {
                            t.RollBack();
                            AtualizarStatus("O Revit rejeitou a restauração do valor anterior.");
                            return;
                        }
                    }
                    else if (ultimo.StorageType == StorageType.Integer)
                    {
                        if (!parametro.Set(ultimo.ValorAnteriorInteiro))
                        {
                            t.RollBack();
                            AtualizarStatus("O Revit rejeitou a restauração do valor anterior.");
                            return;
                        }
                    }

                    _doc.Regenerate();
                    RestaurarOverride(ultimo.ElementId);
                    t.Commit();
                }

                _uidoc.RefreshActiveView();

                _historico.RemoveAt(_historico.Count - 1);
                _idsProcessados.Remove(ultimo.ElementId.Value);
                _numeroAtual = ultimo.NumeroAplicado;
                AtualizarSelecao(_uidoc, _idsProcessados);
                AtualizarStatus("Último item desfeito.");
            }

            private void FinalizarSessao()
            {
                if (IsFinalizada)
                    return;

                IsFinalizada = true;

                if (!_config.ManterDestaqueAoConcluir && _historico.Count > 0)
                {
                    using (Transaction t = new Transaction(_doc, "Remover destaque da numeração"))
                    {
                        t.Start();
                        foreach (RegistroNumeracao registro in _historico)
                            RestaurarOverride(registro.ElementId);
                        t.Commit();
                    }
                }

                AtualizarSelecao(_uidoc, _idsProcessados);
                FecharJanela();
                _externalEvent?.Dispose();
                _externalEvent = null;
                _aoFinalizar?.Invoke(this);

                AppDialogService.ShowInfo(
                    Titulo,
                    "Processamento concluído." +
                    $"\n\nEscopo: {DescreverEscopo(_config.Escopo)}" +
                    $"\nCategoria: {_config.CategoriaNome}" +
                    $"\nParâmetro: {_config.ParametroNome}" +
                    $"\nElementos numerados: {TotalProcessados}" +
                    $"\nElementos elegíveis: {TotalElegiveis}" +
                    $"\nPróximo valor sugerido: {ObterValorPreview(_config, _numeroAtual)}",
                    "Sessão concluída");
            }

            private void FecharJanela()
            {
                if (_janela == null)
                    return;

                _fechandoJanelaProgramaticamente = true;
                _janela.Close();
                _janela = null;
            }

            private void AtualizarStatus(string mensagem)
            {
                _status = mensagem ?? string.Empty;
                AtualizarJanela();
            }

            private static void AtualizarSelecao(UIDocument uidoc, IEnumerable<long> ids)
            {
                uidoc.Selection.SetElementIds(ids.Select(x => new ElementId(x)).ToList());
            }

            private static bool DefinirValorParametro(Parameter parametro, NumeracaoItensConfig config, int numero)
            {
                if (parametro.StorageType == StorageType.String)
                    return parametro.Set(config.MontarValor(numero));

                if (parametro.StorageType == StorageType.Integer)
                    return parametro.Set(numero);

                throw new InvalidOperationException("O parâmetro selecionado não aceita numeração manual.");
            }

            private static bool ParametroRecebeuValorEsperado(Parameter parametro, NumeracaoItensConfig config, int numero)
            {
                if (parametro.StorageType == StorageType.String)
                    return string.Equals(parametro.AsString() ?? string.Empty, config.MontarValor(numero), StringComparison.Ordinal);

                if (parametro.StorageType == StorageType.Integer)
                    return parametro.AsInteger() == numero;

                return false;
            }

            private static string DescreverEscopo(NumeracaoEscopo escopo)
            {
                return escopo switch
                {
                    NumeracaoEscopo.ModeloInteiro => "Modelo inteiro",
                    NumeracaoEscopo.VistaAtiva => "Vista ativa",
                    NumeracaoEscopo.SelecaoAtual => "Seleção atual",
                    _ => "Desconhecido"
                };
            }

            private static string ObterValorPreview(NumeracaoItensConfig config, int numero)
            {
                return config.ParametroStorageType == StorageType.Integer
                    ? numero.ToString(CultureInfo.CurrentCulture)
                    : config.MontarValor(numero);
            }

            private static OverrideGraphicSettings CriarOverrideDestaque()
            {
                Color cor = new Color(255, 170, 0);
                OverrideGraphicSettings settings = new OverrideGraphicSettings();
                settings.SetProjectionLineColor(cor);
                settings.SetCutLineColor(cor);
                settings.SetSurfaceTransparency(30);
                settings.SetSurfaceForegroundPatternColor(cor);
                settings.SetCutForegroundPatternColor(cor);
                return settings;
            }

            private void RestaurarOverride(ElementId elementId)
            {
                if (!_overridesOriginais.TryGetValue(elementId.Value, out OverrideGraphicSettings original))
                    return;

                _view.SetElementOverrides(elementId, original);
            }

            private void Enfileirar(RequisicaoSessao requisicao)
            {
                _handler.DefinirRequisicao(requisicao);
                _externalEvent.Raise();
            }

            private static void ConfigurarTratamentoDeFalhas(Transaction transaction)
            {
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new SuprimirWarningsPreprocessor());
                transaction.SetFailureHandlingOptions(options);
            }

            private sealed class RegistroNumeracao
            {
                public RegistroNumeracao(
                    ElementId elementId,
                    int numeroAplicado,
                    string valorAnteriorTexto,
                    int valorAnteriorInteiro,
                    StorageType storageType)
                {
                    ElementId = elementId;
                    NumeroAplicado = numeroAplicado;
                    ValorAnteriorTexto = valorAnteriorTexto;
                    ValorAnteriorInteiro = valorAnteriorInteiro;
                    StorageType = storageType;
                }

                public ElementId ElementId { get; }
                public int NumeroAplicado { get; }
                public string ValorAnteriorTexto { get; }
                public int ValorAnteriorInteiro { get; }
                public StorageType StorageType { get; }
            }

            private sealed class FiltroSelecaoNumeracao : ISelectionFilter
            {
                private readonly HashSet<long> _idsElegiveis;
                private readonly HashSet<long> _idsProcessados;

                public FiltroSelecaoNumeracao(HashSet<long> idsElegiveis, HashSet<long> idsProcessados)
                {
                    _idsElegiveis = idsElegiveis ?? new HashSet<long>();
                    _idsProcessados = idsProcessados ?? new HashSet<long>();
                }

                public bool AllowElement(Element elem)
                {
                    return elem != null &&
                           _idsElegiveis.Contains(elem.Id.Value) &&
                           !_idsProcessados.Contains(elem.Id.Value);
                }

                public bool AllowReference(Reference reference, XYZ position)
                {
                    return false;
                }
            }

            private sealed class SuprimirWarningsPreprocessor : IFailuresPreprocessor
            {
                public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
                {
                    IList<FailureMessageAccessor> falhas = failuresAccessor.GetFailureMessages();
                    foreach (FailureMessageAccessor falha in falhas)
                    {
                        if (falha.GetSeverity() == FailureSeverity.Warning)
                            failuresAccessor.DeleteWarning(falha);
                    }

                    return FailureProcessingResult.Continue;
                }
            }
        }

        internal sealed class NumeracaoItensExternalEventHandler : IExternalEventHandler
        {
            private readonly NumeracaoItensSessao _sessao;
            private RequisicaoSessao _requisicao;

            public NumeracaoItensExternalEventHandler(NumeracaoItensSessao sessao)
            {
                _sessao = sessao;
            }

            public void DefinirRequisicao(RequisicaoSessao requisicao)
            {
                _requisicao = requisicao;
            }

            public void Execute(UIApplication app)
            {
                _sessao.ExecutarRequisicao(app, _requisicao);
            }

            public string GetName()
            {
                return "FerramentaEMT.NumeracaoItens";
            }
        }

        internal enum RequisicaoSessao
        {
            RetomarSelecao = 0,
            DesfazerUltimo = 1,
            Concluir = 2
        }
    }
}
