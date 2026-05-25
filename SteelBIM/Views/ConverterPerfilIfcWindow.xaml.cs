using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Core;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Models.Ifc;
using SteelBIM.Services.Ifc;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    /// <summary>
    /// Window do Conversor IFC. v2.7.1: agora **modeless** (usuario pode
    /// interagir com Revit 3D enquanto dialog aberto) usando 2
    /// <see cref="IExternalEventHandler"/>:
    /// <list type="bullet">
    /// <item><see cref="IfcSelectionHandler"/>: click linha -> highlight + zoom</item>
    /// <item><see cref="IfcConversionHandler"/>: BtnConverter -> service.Executar</item>
    /// </list>
    /// Tambem v2.7.1: filtro <c>chkApenasEstruturais</c> esconde acessorios
    /// IFC nao-conversiveis (armaduras, chapas, ganchos) via
    /// <see cref="ConverterPerfilIfcService.EhPerfilEstruturalLinear"/>.
    /// </summary>
    public partial class ConverterPerfilIfcWindow : Window
    {
        // Estado original (toda a lista) preservado para toggle do filtro
        private readonly List<ElementoIfcViewModel> _todosElementos;
        private List<ElementoIfcViewModel> _elementosVisiveis;
        private readonly List<string> _paramsDisponiveis;
        private readonly List<Level> _niveis;
        private readonly AppSettings _settings;
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        private List<GrupoIfcViewModel> _grupos;
        private bool _carregando;

        // v2.7.1 modeless: 2 ExternalEvents + handlers
        private readonly ExternalEvent _selectionEvent;
        private readonly IfcSelectionHandler _selectionHandler;
        private readonly ExternalEvent _conversionEvent;
        private readonly IfcConversionHandler _conversionHandler;

        // v2.7.1: race-condition guard (window fechada antes do Execute terminar)
        private volatile bool _isClosing;

        // v2.7.10: ProgressWindow + CTS pra UX de progresso/cancel no Conversor IFC.
        // API ja existia desde v2.7.7 (IfcConversionHandler.Progress + .CancellationToken).
        // Esta versao consome a API: ao clicar Converter, abre ProgressWindow modeless
        // (botao Cancel funcional), reporta progresso a cada 25 elementos (throttle do
        // service), e em caso de Cancel rollback automatico via Transaction.Dispose.
        private CancellationTokenSource _cts;
        private ProgressWindow _progressWindow;

        public ConverterPerfilIfcWindow(
            UIDocument uidoc,
            List<ElementoIfcViewModel> elementos,
            List<string> paramsDisponiveis,
            string paramInicial,
            List<Level> niveis,
            AppSettings settings)
        {
            // v2.7.3 HOTFIX: bloqueia handlers durante boot.
            // Sem essa linha, IsChecked="True" do chkApenasEstruturais no XAML
            // dispara Checked event ja durante InitializeComponent, ANTES dos
            // campos (_todosElementos, _doc) serem inicializados nas linhas
            // abaixo. Handler ChkApenasEstruturais_Toggled JA tem guard
            // `if (_carregando) return;` — mas a flag e false (default bool)
            // quando o handler dispara, entao o guard nao protege.
            // Setar true aqui faz o guard funcionar; LoadData seta false no fim.
            _carregando = true;

            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _doc = uidoc?.Document;
            _todosElementos = elementos ?? new List<ElementoIfcViewModel>();
            _elementosVisiveis = _todosElementos.ToList();
            _paramsDisponiveis = paramsDisponiveis;
            _niveis = niveis;
            _settings = settings;

            // v2.7.1 modeless: instanciar handlers + ExternalEvents
            _selectionHandler = new IfcSelectionHandler();
            _selectionEvent = ExternalEvent.Create(_selectionHandler);
            _conversionHandler = new IfcConversionHandler { Doc = _doc };
            _conversionEvent = ExternalEvent.Create(_conversionHandler);

            LoadData(paramInicial);

            btnConverter.Click += BtnConverter_Click;
            btnCancelar.Click += BtnCancelar_Click;
            btnSelecionarTodos.Click += (_, __) => AlterarSelecao(true);
            btnDeselecionarTodos.Click += (_, __) => AlterarSelecao(false);
        }

        private void LoadData(string paramInicial)
        {
            _carregando = true;

            foreach (string param in _paramsDisponiveis)
                cmbParamIfc.Items.Add(param);

            if (!_paramsDisponiveis.Contains(paramInicial))
                cmbParamIfc.Items.Insert(0, paramInicial);

            cmbParamIfc.SelectedItem = paramInicial;
            if (cmbParamIfc.SelectedIndex < 0 && cmbParamIfc.Items.Count > 0)
                cmbParamIfc.SelectedIndex = 0;

            foreach (Level nivel in _niveis)
                cmbNivelPadrao.Items.Add(new LevelItem(nivel));

            if (!string.IsNullOrWhiteSpace(_settings.LastConverterIfcNivelPadrao))
            {
                for (int i = 0; i < cmbNivelPadrao.Items.Count; i++)
                {
                    if (cmbNivelPadrao.Items[i] is LevelItem item &&
                        item.Level.Name == _settings.LastConverterIfcNivelPadrao)
                    {
                        cmbNivelPadrao.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (cmbNivelPadrao.SelectedIndex < 0 && cmbNivelPadrao.Items.Count > 0)
                cmbNivelPadrao.SelectedIndex = 0;

            chkDeletarOriginal.IsChecked = _settings.LastConverterIfcDeletarOriginal;

            _carregando = false;

            // v2.7.1: aplica filtro inicial (chkApenasEstruturais=true por default no XAML)
            AplicarFiltroEstrutural();
            ReConstruirGrupos();
        }

        // ─── v2.7.1: filtro estrutural ────────────────────────────────────────

        private void AplicarFiltroEstrutural()
        {
            if (chkApenasEstruturais?.IsChecked == true && _doc != null)
            {
                _elementosVisiveis = _todosElementos
                    .Where(vm =>
                    {
                        Element e = _doc.GetElement(vm.ElementId);
                        return ConverterPerfilIfcService.EhPerfilEstruturalLinear(e);
                    })
                    .ToList();
            }
            else
            {
                _elementosVisiveis = _todosElementos.ToList();
            }
        }

        private void ChkApenasEstruturais_Toggled(object sender, RoutedEventArgs e)
        {
            if (_carregando)
                return;
            AplicarFiltroEstrutural();
            ReConstruirGrupos();
        }

        // ─── v2.7.1: click linha -> highlight via ExternalEvent ───────────────

        private void GridElementos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isClosing || _selectionEvent == null || _selectionHandler == null)
                return;

            if (gridElementos.SelectedItem is GrupoIfcViewModel grupo && grupo.ElementIds.Count > 0)
            {
                _selectionHandler.PendingIds = new List<ElementId>(grupo.ElementIds);
                _selectionEvent.Raise();
            }
        }

        // ─── Agrupamento (usa _elementosVisiveis em vez de _todosElementos) ───

        private void ReConstruirGrupos()
        {
            IReadOnlyList<SymbolItem> allPerfis = _todosElementos.Count > 0
                ? _todosElementos[0].AllPerfis
                : new List<SymbolItem>();

            // v2.7.0 BUG 3: agrupar por tupla (SecaoSugerida, NomeMaterial)
            // v2.7.1: agora sobre _elementosVisiveis (apos filtro estrutural)
            _grupos = _elementosVisiveis
                .GroupBy(vm => (
                    Secao: vm.SecaoSugerida ?? string.Empty,
                    Material: IfcMaterialParser.ExtrairNomeMaterial(vm.IfcMaterial) ?? string.Empty
                ))
                .OrderBy(g => string.IsNullOrEmpty(g.Key.Secao) ? 1 : 0)
                .ThenBy(g => g.Key.Secao, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Material, System.StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    ElementoIfcViewModel rep = g.First();
                    var grupo = new GrupoIfcViewModel(allPerfis)
                    {
                        SecaoSugerida = g.Key.Secao,
                        ValorIfc = rep.IfcMaterial
                    };
                    foreach (ElementoIfcViewModel vm in g)
                        grupo.ElementIds.Add(vm.ElementId);

                    grupo.PerfilSelecionado = EncontrarMelhorCandidato(g.Key.Secao, allPerfis);
                    return grupo;
                })
                .ToList();

            gridElementos.ItemsSource = null;
            gridElementos.ItemsSource = _grupos;

            AtualizarContagem();
        }

        private SymbolItem EncontrarMelhorCandidato(
            string secao,
            IReadOnlyList<SymbolItem> allPerfis)
        {
            if (string.IsNullOrWhiteSpace(secao))
                return null;

            SymbolItem melhor = null;
            // v2.7.0 BUG 3: 49 -> 59 (alinhado com ScoreMinimo 60 do command)
            int melhorScore = 59;

            foreach (SymbolItem si in allPerfis)
            {
                int score = IfcMaterialParser.CalcularScore(secao, si.Symbol.Name);
                if (score > melhorScore)
                {
                    melhorScore = score;
                    melhor = si;
                }
            }

            return melhor;
        }

        private void CmbParamIfc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregando || cmbParamIfc.SelectedItem is not string nomeParam)
                return;

            // RecalcularSugestao roda sobre _todosElementos (mantem cache pra todos,
            // mesmo escondidos) e em seguida reconstroi grupos pelo filtro vigente.
            foreach (ElementoIfcViewModel vm in _todosElementos)
                vm.RecalcularSugestao(nomeParam);

            AplicarFiltroEstrutural();
            ReConstruirGrupos();
        }

        private void AlterarSelecao(bool selecionar)
        {
            if (_grupos == null)
                return;
            foreach (GrupoIfcViewModel g in _grupos)
                g.Selecionado = selecionar;
            AtualizarContagem();
        }

        private void AtualizarContagem()
        {
            if (_grupos == null)
            { lblContagem.Text = string.Empty; return; }

            int totalGrupos = _grupos.Count;
            int totalElementos = _grupos.Sum(g => g.Quantidade);
            int gruposSel = _grupos.Count(g => g.Selecionado);
            int elemSel = _grupos.Where(g => g.Selecionado).Sum(g => g.Quantidade);
            int comPerfil = _grupos.Count(g => g.Selecionado && g.PerfilSelecionado != null);
            int elemComPerfil = _grupos
                .Where(g => g.Selecionado && g.PerfilSelecionado != null)
                .Sum(g => g.Quantidade);

            // v2.7.1: incluir indicador de filtro ativo no label
            string filtroInfo = chkApenasEstruturais?.IsChecked == true
                ? $" (filtro estrutural ativo: {_elementosVisiveis.Count}/{_todosElementos.Count} elementos)"
                : string.Empty;

            lblContagem.Text =
                $"{gruposSel}/{totalGrupos} grupos selecionados  " +
                $"({elemSel}/{totalElementos} elementos)  -  " +
                $"{comPerfil} grupos com perfil atribuido ({elemComPerfil} elementos)" +
                filtroInfo;
        }

        private ConverterPerfilIfcConfig BuildConfig()
        {
            Level nivelPadrao = (cmbNivelPadrao.SelectedItem as LevelItem)?.Level;

            var config = new ConverterPerfilIfcConfig
            {
                DeletarOriginal = chkDeletarOriginal.IsChecked == true,
                NivelPadrao = nivelPadrao
            };

            foreach (GrupoIfcViewModel grupo in _grupos)
            {
                if (!grupo.Selecionado || grupo.PerfilSelecionado == null)
                    continue;

                foreach (ElementId id in grupo.ElementIds)
                {
                    config.Conversoes.Add(new ConversaoElementoIfc
                    {
                        ElementoOrigem = id,
                        PerfilDestino = grupo.PerfilSelecionado.Symbol
                    });
                }
            }

            return config;
        }

        // ─── v2.7.1: BtnConverter via ExternalEvent (modeless) ────────────────

        private void BtnConverter_Click(object sender, RoutedEventArgs e)
        {
            int elemComPerfil = _grupos
                .Where(g => g.Selecionado && g.PerfilSelecionado != null)
                .Sum(g => g.Quantidade);

            int gruposSemPerfil = _grupos.Count(g => g.Selecionado && g.PerfilSelecionado == null);
            int elemSemPerfil = _grupos
                .Where(g => g.Selecionado && g.PerfilSelecionado == null)
                .Sum(g => g.Quantidade);

            if (elemComPerfil == 0)
            {
                AppDialogService.ShowWarning(
                    "Converter Perfis IFC",
                    "Nenhum grupo selecionado possui um perfil Revit atribuido.\n\n" +
                    "Clique duas vezes nas celulas 'Familia Revit' e 'Tipo Revit' para escolher o perfil.",
                    "Sem perfis atribuidos");
                return;
            }

            if (gruposSemPerfil > 0)
            {
                bool continuar = AppDialogService.ShowConfirmation(
                    "Converter Perfis IFC",
                    $"{gruposSemPerfil} grupo(s) ({elemSemPerfil} elemento(s)) sem perfil serao ignorados.\n\n" +
                    $"Deseja converter apenas os {elemComPerfil} elemento(s) com perfil atribuido?",
                    "Grupos sem perfil",
                    "Continuar",
                    "Cancelar");

                if (!continuar)
                    return;
            }

            if (cmbNivelPadrao.SelectedItem is LevelItem levelItem)
                _settings.LastConverterIfcNivelPadrao = levelItem.Level.Name;

            if (cmbParamIfc.SelectedItem is string paramSelecionado)
                _settings.LastConverterIfcParamIfc = paramSelecionado;

            _settings.LastConverterIfcDeletarOriginal = chkDeletarOriginal.IsChecked == true;
            _settings.Save();

            // v2.7.1: dispara conversao via ExternalEvent — Revit invoca
            // IfcConversionHandler.Execute no thread API, permitindo Transaction.
            ConverterPerfilIfcConfig config = BuildConfig();
            _conversionHandler.Config = config;
            _conversionHandler.OnFinished = OnConversionFinished;

            // v2.7.10: criar CTS + ProgressWindow modeless. CTS cancela a operacao
            // via service.Executar(ct) — service captura OperationCanceledException
            // e Transaction.Dispose faz rollback automatico.
            _cts = new CancellationTokenSource();
            _progressWindow = new ProgressWindow(
                "Converter Perfis IFC",
                $"Convertendo {elemComPerfil} elemento(s)...")
            {
                Owner = this
            };
            _progressWindow.Cancelled += (_, __) =>
            {
                try { _cts?.Cancel(); }
                catch (ObjectDisposedException) { /* ignorado — CTS ja disposed */ }
            };

            // Progress<T> capturado aqui (UI thread) auto-marshalla callbacks pelo
            // SynchronizationContext quando service.Executar (na thread Revit API)
            // chamar progress.Report(...).
            _conversionHandler.Progress = new Progress<ProgressReport>(report =>
            {
                if (_isClosing || _progressWindow == null) return;
                _progressWindow.UpdateProgress(report);
            });
            _conversionHandler.CancellationToken = _cts.Token;

            // Desabilitar botoes pra evitar duplo-disparo enquanto Execute roda
            btnConverter.IsEnabled = false;
            btnCancelar.IsEnabled = false;

            _progressWindow.Show();
            _conversionEvent.Raise();
        }

        /// <summary>
        /// Callback invocado pelo <see cref="IfcConversionHandler.Execute"/>
        /// (thread Revit API). Mostra resultado + fecha ProgressWindow + Window
        /// principal via Dispatcher.
        ///
        /// v2.7.10: detecta cancelamento via flag <c>_cts.IsCancellationRequested</c>
        /// e mostra mensagem distinta (rollback aplicado). Sucesso normal mantem
        /// mensagem original.
        /// </summary>
        private void OnConversionFinished(int convertidos, int ignorados)
        {
            if (_isClosing)
                return;

            // Marshall pra UI thread (Dispatcher) — Execute roda em thread API
            Dispatcher.Invoke(() =>
            {
                if (_isClosing)
                    return;

                // v2.7.10: fechar ProgressWindow + cleanup CTS antes de mostrar dialog
                try
                {
                    if (_progressWindow != null && _progressWindow.IsVisible)
                        _progressWindow.Close();
                }
                catch (InvalidOperationException) { /* progress window ja fechada */ }
                _progressWindow = null;

                bool wasCancelled = _cts?.IsCancellationRequested == true;
                _cts?.Dispose();
                _cts = null;

                if (wasCancelled)
                {
                    AppDialogService.ShowInfo(
                        "Converter Perfis IFC",
                        $"Conversao cancelada pelo usuario.\n\n" +
                        $"Rollback automatico aplicado — nenhum elemento Revit foi criado.\n" +
                        $"Elementos processados antes do cancel: {convertidos + ignorados}",
                        "Operacao cancelada");
                }
                else
                {
                    AppDialogService.ShowInfo(
                        "Converter Perfis IFC",
                        $"Conversao concluida.\n\n" +
                        $"Convertidos: {convertidos}\n" +
                        $"Ignorados (sem eixo ou nivel detectavel): {ignorados}",
                        "Conversao concluida");
                }

                Close();
            });
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ─── v2.7.1: race-condition guard + cleanup dos handlers ──────────────

        protected override void OnClosing(CancelEventArgs e)
        {
            _isClosing = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Limpa referencias pra evitar leak entre sessoes — ExternalEvent
            // nao expoe Dispose publico no Revit API, mas zerar campos dos
            // handlers e suficiente pra liberar ElementIds + Doc + callback.
            if (_selectionHandler != null)
                _selectionHandler.PendingIds = null;
            if (_conversionHandler != null)
            {
                _conversionHandler.Config = null;
                _conversionHandler.Doc = null;
                _conversionHandler.OnFinished = null;
                _conversionHandler.Progress = null;
                _conversionHandler.CancellationToken = default;
            }

            // v2.7.10: cleanup ProgressWindow + CTS se ainda abertos (Window principal
            // fechada antes da conversao terminar — usuario clica X). CTS.Cancel
            // dispara cancelamento do servico em curso; service rollback transaction.
            if (_progressWindow != null && _progressWindow.IsVisible)
            {
                try { _progressWindow.Close(); }
                catch (InvalidOperationException) { /* ignorado */ }
            }
            _progressWindow = null;
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { /* ignorado */ }
            _cts?.Dispose();
            _cts = null;

            base.OnClosed(e);
        }
    }
}
