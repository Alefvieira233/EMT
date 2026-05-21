using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Models;
using SteelBIM.Models.Ifc;
using SteelBIM.Services.Ifc;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class ConverterPerfilIfcWindow : Window
    {
        private readonly List<ElementoIfcViewModel> _elementos;
        private readonly List<string> _paramsDisponiveis;
        private readonly List<Level> _niveis;
        private readonly AppSettings _settings;
        private List<GrupoIfcViewModel> _grupos;
        private bool _carregando;

        public ConverterPerfilIfcWindow(
            List<ElementoIfcViewModel> elementos,
            List<string> paramsDisponiveis,
            string paramInicial,
            List<Level> niveis,
            AppSettings settings)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _elementos = elementos;
            _paramsDisponiveis = paramsDisponiveis;
            _niveis = niveis;
            _settings = settings;

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

            ReConstruirGrupos();
        }

        private void ReConstruirGrupos()
        {
            IReadOnlyList<SymbolItem> allPerfis = _elementos.Count > 0
                ? _elementos[0].AllPerfis
                : new List<SymbolItem>();

            // v2.7.0 BUG 3: agrupar por tupla (SecaoSugerida, NomeMaterial) — antes
            // agrupava so por secao, juntando galvanizado com pintado num mesmo grupo
            // mesmo quando o usuario quer trata-los como conjuntos distintos.
            _grupos = _elementos
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

            foreach (ElementoIfcViewModel vm in _elementos)
                vm.RecalcularSugestao(nomeParam);

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

            lblContagem.Text =
                $"{gruposSel}/{totalGrupos} grupos selecionados  " +
                $"({elemSel}/{totalElementos} elementos)  -  " +
                $"{comPerfil} grupos com perfil atribuido ({elemComPerfil} elementos)";
        }

        public ConverterPerfilIfcConfig BuildConfig()
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

            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
