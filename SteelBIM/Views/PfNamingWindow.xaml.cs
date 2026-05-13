using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using FerramentaEMT.Models;
using FerramentaEMT.Models.PF;
using FerramentaEMT.Services;
using FerramentaEMT.Services.PF;
using FerramentaEMT.Utils;
using RevitUIDocument = Autodesk.Revit.UI.UIDocument;

namespace FerramentaEMT.Views
{
    public partial class PfNamingWindow : Window
    {
        private const string TodasFamilias = "<Todas as familias>";
        private const string TodosTipos = "<Todos os tipos>";

        private readonly RevitUIDocument _uidoc;
        private readonly Document _doc;
        private readonly AppSettings _settings;
        private readonly Dictionary<string, List<NumeracaoElementoInfo>> _cache =
            new Dictionary<string, List<NumeracaoElementoInfo>>(StringComparer.OrdinalIgnoreCase);

        private bool _atualizando;

        public PfNamingWindow(RevitUIDocument uidoc, AppSettings settings)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            _uidoc = uidoc;
            _doc = uidoc.Document;
            _settings = settings ?? new AppSettings();

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;

            CarregarEstadoInicial();
        }

        public PfNamingConfig BuildConfig()
        {
            if (!(cmbAlvo.SelectedItem is AlvoOption alvo) ||
                !(cmbParametro.SelectedItem is ParametroOption parametro))
            {
                return null;
            }

            return new PfNamingConfig
            {
                Alvo = alvo.Alvo,
                Escopo = ObterEscopoSelecionado(),
                FamiliaNome = (cmbFamilia.SelectedItem as TextOption)?.Valor ?? string.Empty,
                TipoNome = (cmbTipo.SelectedItem as TextOption)?.Valor ?? string.Empty,
                ParametroChave = parametro.Chave,
                ParametroNome = parametro.Nome,
                ParametroStorageType = parametro.StorageType,
                Prefixo = txtPrefixo.Text?.Trim() ?? string.Empty,
                Inicio = TentarLerInteiro(txtInicio.Text, 1),
                Degrau = TentarLerInteiro(txtDegrau.Text, 1),
                Sufixo = txtSufixo.Text?.Trim() ?? string.Empty
            };
        }

        private void CarregarEstadoInicial()
        {
            _atualizando = true;

            txtPrefixo.Text = _settings.LastPfNamingPrefix ?? string.Empty;
            txtSufixo.Text = _settings.LastPfNamingSuffix ?? string.Empty;
            txtInicio.Text = Math.Max(1, _settings.LastPfNamingStart).ToString();
            txtDegrau.Text = Math.Max(1, _settings.LastPfNamingStep).ToString();

            NumeracaoEscopo escopo = _uidoc.Selection.GetElementIds().Count > 0
                ? NumeracaoEscopo.SelecaoAtual
                : ParseEscopo(_settings.LastPfNamingScope);

            rbModeloInteiro.IsChecked = escopo == NumeracaoEscopo.ModeloInteiro;
            rbVistaAtiva.IsChecked = escopo == NumeracaoEscopo.VistaAtiva;
            rbSelecaoAtual.IsChecked = escopo == NumeracaoEscopo.SelecaoAtual;

            _atualizando = false;

            AtualizarAlvos();
            AtualizarPreview();
        }

        private void Scope_Checked(object sender, RoutedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarAlvos();
        }

        private void Alvo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarFamilias();
        }

        private void Familia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarTipos();
        }

        private void Tipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarParametrosEResumo();
        }

        private void Parametro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarPreview();
            AtualizarResumo();
        }

        private void Valor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarPreview();
            AtualizarResumo();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            List<NumeracaoElementoInfo> filtrados = ObterElementosFiltradosDaTela();

            if (!(cmbAlvo.SelectedItem is AlvoOption))
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Selecione o alvo da rotina.", "Dados incompletos");
                return;
            }

            if (!(cmbParametro.SelectedItem is ParametroOption parametro))
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Selecione um parametro gravavel.", "Dados incompletos");
                return;
            }

            if (filtrados.Count == 0)
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Nenhum elemento PM elegivel foi encontrado com os filtros atuais.", "Nenhum item encontrado");
                return;
            }

            if (!int.TryParse(txtInicio.Text, out int inicio) || inicio < 1)
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Informe um valor inicial inteiro maior ou igual a 1.", "Valor inicial invalido");
                return;
            }

            if (!int.TryParse(txtDegrau.Text, out int degrau) || degrau <= 0)
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Informe um degrau inteiro maior que zero.", "Degrau invalido");
                return;
            }

            if (parametro.StorageType == StorageType.Integer &&
                (!string.IsNullOrWhiteSpace(txtPrefixo.Text) || !string.IsNullOrWhiteSpace(txtSufixo.Text)))
            {
                AppDialogService.ShowWarning("Numerar Automatico", "Parametros inteiros nao aceitam prefixo ou sufixo nesta versao.", "Formato incompativel");
                return;
            }

            SalvarPreferencias();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void AtualizarAlvos()
        {
            _atualizando = true;

            List<AlvoOption> alvos = Enum.GetValues(typeof(PfNamingTarget))
                .Cast<PfNamingTarget>()
                .Select(x => new AlvoOption(x, ObterOuCarregarCandidatos(x, ObterEscopoSelecionado()).Count))
                .ToList();

            cmbAlvo.Items.Clear();
            foreach (AlvoOption alvo in alvos)
                cmbAlvo.Items.Add(alvo);

            if (!SelecionarAlvoPreferido(alvos) && cmbAlvo.Items.Count > 0)
                cmbAlvo.SelectedIndex = 0;

            _atualizando = false;
            AtualizarFamilias();
        }

        private void AtualizarFamilias()
        {
            _atualizando = true;

            List<TextOption> familias = ObterElementosDoAlvoSelecionado()
                .GroupBy(x => x.FamiliaNome)
                .Select(x => new TextOption(x.Key, $"{x.Key} ({x.Count()})"))
                .OrderBy(x => x.Texto, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            cmbFamilia.Items.Clear();
            cmbFamilia.Items.Add(new TextOption(string.Empty, TodasFamilias));
            foreach (TextOption familia in familias)
                cmbFamilia.Items.Add(familia);

            if (!SelecionarTextoPreferido(cmbFamilia, _settings.LastPfNamingFamilyName))
                cmbFamilia.SelectedIndex = cmbFamilia.Items.Count > 0 ? 0 : -1;

            _atualizando = false;
            AtualizarTipos();
        }

        private void AtualizarTipos()
        {
            _atualizando = true;

            List<TextOption> tipos = ObterElementosAteFamiliaSelecionada()
                .GroupBy(x => x.TipoNome)
                .Select(x => new TextOption(x.Key, $"{x.Key} ({x.Count()})"))
                .OrderBy(x => x.Texto, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            cmbTipo.Items.Clear();
            cmbTipo.Items.Add(new TextOption(string.Empty, TodosTipos));
            foreach (TextOption tipo in tipos)
                cmbTipo.Items.Add(tipo);

            if (!SelecionarTextoPreferido(cmbTipo, _settings.LastPfNamingTypeName))
                cmbTipo.SelectedIndex = cmbTipo.Items.Count > 0 ? 0 : -1;

            _atualizando = false;
            AtualizarParametrosEResumo();
        }

        private void AtualizarParametrosEResumo()
        {
            _atualizando = true;

            List<NumeracaoElementoInfo> filtrados = ObterElementosFiltradosDaTela();
            List<NumeracaoParametroInfo> parametros = NumeracaoItensCatalog.ColetarParametrosGravaveisEmComum(_doc, filtrados);

            cmbParametro.Items.Clear();
            foreach (NumeracaoParametroInfo parametro in parametros)
                cmbParametro.Items.Add(new ParametroOption(parametro));

            if (!SelecionarParametroPreferido() && cmbParametro.Items.Count > 0)
                cmbParametro.SelectedIndex = 0;

            _atualizando = false;
            AtualizarPreview();
            AtualizarResumo();
        }

        private void AtualizarResumo()
        {
            AlvoOption alvo = cmbAlvo.SelectedItem as AlvoOption;
            List<NumeracaoElementoInfo> candidatos = alvo == null
                ? new List<NumeracaoElementoInfo>()
                : ObterOuCarregarCandidatos(alvo.Alvo, ObterEscopoSelecionado());
            List<NumeracaoElementoInfo> filtrados = ObterElementosFiltradosDaTela();

            string avisoSelecao = ObterEscopoSelecionado() == NumeracaoEscopo.SelecaoAtual && candidatos.Count == 0
                ? "A selecao atual esta vazia.\n"
                : string.Empty;

            string tipoParametro = cmbParametro.SelectedItem is ParametroOption parametro
                ? parametro.StorageType == StorageType.Integer ? "Inteiro" : "Texto"
                : "Nenhum";
            string nomeParametro = cmbParametro.SelectedItem is ParametroOption parametroSelecionado
                ? parametroSelecionado.Nome
                : "Nenhum";

            string regraOrdem = alvo?.Alvo == PfNamingTarget.Vigas
                ? "As vigas sao ordenadas primeiro por eixo horizontal/X e depois por eixo vertical/Y."
                : "A ordenacao usa a vista ativa para seguir o fluxo visual do desenho.";

            txtResumo.Text =
                avisoSelecao +
                $"Elementos encontrados no alvo: {candidatos.Count}\n" +
                $"Elementos elegiveis com os filtros: {filtrados.Count}\n" +
                $"Parametros gravaveis em comum: {cmbParametro.Items.Count}\n" +
                $"Campo que recebera a sequencia: {nomeParametro}\n" +
                $"Tipo do parametro selecionado: {tipoParametro}\n\n" +
                regraOrdem;

            AtualizarListaElementos(filtrados);

            btnOk.IsEnabled =
                cmbAlvo.SelectedItem != null &&
                cmbParametro.SelectedItem != null &&
                filtrados.Count > 0;
        }

        private void AtualizarPreview()
        {
            int inicio = TentarLerInteiro(txtInicio.Text, 1);
            string preview = cmbParametro.SelectedItem is ParametroOption parametro &&
                             parametro.StorageType == StorageType.Integer
                ? inicio.ToString()
                : $"{txtPrefixo.Text}{inicio}{txtSufixo.Text}";

            txtPreview.Text = $"Proximo valor: {preview}";
        }

        private List<NumeracaoElementoInfo> ObterOuCarregarCandidatos(PfNamingTarget alvo, NumeracaoEscopo escopo)
        {
            string chave = $"{alvo}:{escopo}";
            if (_cache.TryGetValue(chave, out List<NumeracaoElementoInfo> candidatos))
                return candidatos;

            candidatos = PfNamingCatalog.ColetarCandidatos(_uidoc, escopo, alvo);
            _cache[chave] = candidatos;
            return candidatos;
        }

        private List<NumeracaoElementoInfo> ObterElementosDoAlvoSelecionado()
        {
            if (!(cmbAlvo.SelectedItem is AlvoOption alvo))
                return new List<NumeracaoElementoInfo>();

            return ObterOuCarregarCandidatos(alvo.Alvo, ObterEscopoSelecionado());
        }

        private List<NumeracaoElementoInfo> ObterElementosAteFamiliaSelecionada()
        {
            List<NumeracaoElementoInfo> elementos = ObterElementosDoAlvoSelecionado();
            string familia = (cmbFamilia.SelectedItem as TextOption)?.Valor ?? string.Empty;

            return string.IsNullOrWhiteSpace(familia)
                ? elementos
                : elementos.Where(x => string.Equals(x.FamiliaNome, familia, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        private List<NumeracaoElementoInfo> ObterElementosFiltradosDaTela()
        {
            List<NumeracaoElementoInfo> elementos = ObterElementosAteFamiliaSelecionada();
            string tipo = (cmbTipo.SelectedItem as TextOption)?.Valor ?? string.Empty;

            return string.IsNullOrWhiteSpace(tipo)
                ? elementos
                : elementos.Where(x => string.Equals(x.TipoNome, tipo, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        private NumeracaoEscopo ObterEscopoSelecionado()
        {
            if (rbModeloInteiro.IsChecked == true)
                return NumeracaoEscopo.ModeloInteiro;
            if (rbSelecaoAtual.IsChecked == true)
                return NumeracaoEscopo.SelecaoAtual;

            return NumeracaoEscopo.VistaAtiva;
        }

        private bool SelecionarAlvoPreferido(List<AlvoOption> alvos)
        {
            if (alvos.Count == 0)
                return false;

            PfNamingTarget preferido = ParseAlvo(_settings.LastPfNamingTarget);
            AlvoOption opcao = alvos.FirstOrDefault(x => x.Alvo == preferido) ?? alvos.FirstOrDefault();
            if (opcao == null)
                return false;

            cmbAlvo.SelectedItem = opcao;
            return true;
        }

        private static bool SelecionarTextoPreferido(ComboBox combo, string valor)
        {
            if (combo == null || string.IsNullOrWhiteSpace(valor))
                return false;

            foreach (object item in combo.Items)
            {
                if (item is TextOption opcao &&
                    string.Equals(opcao.Valor, valor, StringComparison.CurrentCultureIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }

        private bool SelecionarParametroPreferido()
        {
            ParametroOption primeiroPreferencial = cmbParametro.Items
                .Cast<object>()
                .OfType<ParametroOption>()
                .FirstOrDefault(x => x.IsPreferencial);

            if (primeiroPreferencial != null)
            {
                cmbParametro.SelectedItem = primeiroPreferencial;
                return true;
            }

            ParametroOption preferido = cmbParametro.Items
                .Cast<object>()
                .OfType<ParametroOption>()
                .FirstOrDefault(x => string.Equals(x.Chave, _settings.LastPfNamingParameterKey, StringComparison.OrdinalIgnoreCase));

            if (preferido != null)
            {
                cmbParametro.SelectedItem = preferido;
                return true;
            }

            return false;
        }

        private void SalvarPreferencias()
        {
            _settings.LastPfNamingTarget = (cmbAlvo.SelectedItem as AlvoOption)?.Alvo.ToString() ?? PfNamingTarget.Pilares.ToString();
            _settings.LastPfNamingScope = ObterEscopoSelecionado().ToString();
            _settings.LastPfNamingFamilyName = (cmbFamilia.SelectedItem as TextOption)?.Valor ?? string.Empty;
            _settings.LastPfNamingTypeName = (cmbTipo.SelectedItem as TextOption)?.Valor ?? string.Empty;
            _settings.LastPfNamingParameterKey = (cmbParametro.SelectedItem as ParametroOption)?.Chave ?? string.Empty;
            _settings.LastPfNamingPrefix = txtPrefixo.Text ?? string.Empty;
            _settings.LastPfNamingSuffix = txtSufixo.Text ?? string.Empty;
            _settings.LastPfNamingStart = TentarLerInteiro(txtInicio.Text, 1);
            _settings.LastPfNamingStep = TentarLerInteiro(txtDegrau.Text, 1);
            _settings.Save();
        }

        private static PfNamingTarget ParseAlvo(string valor)
        {
            return Enum.TryParse(valor, true, out PfNamingTarget alvo)
                ? alvo
                : PfNamingTarget.Pilares;
        }

        private static NumeracaoEscopo ParseEscopo(string valor)
        {
            return Enum.TryParse(valor, true, out NumeracaoEscopo escopo)
                ? escopo
                : NumeracaoEscopo.VistaAtiva;
        }

        private static int TentarLerInteiro(string texto, int valorPadrao)
        {
            return int.TryParse(texto, out int valor) ? valor : valorPadrao;
        }

        private void AtualizarListaElementos(List<NumeracaoElementoInfo> elementos)
        {
            lstElementos.Items.Clear();

            if (elementos == null || elementos.Count == 0)
            {
                lstElementos.Items.Add("Nenhum elemento PM encontrado com os filtros atuais.");
                return;
            }

            foreach (NumeracaoElementoInfo elemento in elementos
                         .OrderBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.Id.Value))
            {
                lstElementos.Items.Add($"{elemento.FamiliaNome} | {elemento.TipoNome} | Id {elemento.Id.Value}");
            }
        }

        private sealed class AlvoOption
        {
            public AlvoOption(PfNamingTarget alvo, int quantidade)
            {
                Alvo = alvo;
                Quantidade = quantidade;
            }

            public PfNamingTarget Alvo { get; }
            public int Quantidade { get; }

            public override string ToString()
            {
                return $"{PfNamingCatalog.GetTargetDisplayName(Alvo)} ({Quantidade})";
            }
        }

        private sealed class TextOption
        {
            public TextOption(string valor, string texto)
            {
                Valor = valor;
                Texto = texto;
            }

            public string Valor { get; }
            public string Texto { get; }

            public override string ToString()
            {
                return Texto;
            }
        }

        private sealed class ParametroOption
        {
            public ParametroOption(NumeracaoParametroInfo parametro)
            {
                Chave = parametro.Chave;
                Nome = parametro.Nome;
                StorageType = parametro.StorageType;
                IsPreferencial = parametro.IsPreferencial;
            }

            public string Chave { get; }
            public string Nome { get; }
            public StorageType StorageType { get; }
            public bool IsPreferencial { get; }

            public override string ToString()
            {
                return StorageType == StorageType.Integer
                    ? $"{Nome} (inteiro)"
                    : $"{Nome} (texto)";
            }
        }
    }
}
