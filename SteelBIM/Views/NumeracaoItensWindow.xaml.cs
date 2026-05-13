using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class NumeracaoItensWindow : Window
    {
        private const string TodasFamilias = "<Todas as famílias>";
        private const string TodosTipos = "<Todos os tipos>";

        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly AppSettings _settings;
        private readonly Dictionary<NumeracaoEscopo, List<NumeracaoElementoInfo>> _cachePorEscopo =
            new Dictionary<NumeracaoEscopo, List<NumeracaoElementoInfo>>();

        private bool _atualizando;

        public NumeracaoItensWindow(UIDocument uidoc, AppSettings settings)
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

        public NumeracaoItensConfig BuildConfig()
        {
            if (!(cmbCategoria.SelectedItem is CategoriaOption categoria) ||
                !(cmbParametro.SelectedItem is ParametroOption parametro))
            {
                return null;
            }

            TextOption familia = cmbFamilia.SelectedItem as TextOption;
            TextOption tipo = cmbTipo.SelectedItem as TextOption;

            int inicio = TentarLerInteiro(txtInicio.Text, 1);
            int degrau = TentarLerInteiro(txtDegrau.Text, 1);

            return new NumeracaoItensConfig
            {
                Escopo = ObterEscopoSelecionado(),
                CategoriaIdValor = categoria.CategoriaId.Value,
                CategoriaNome = categoria.Nome,
                FamiliaNome = familia?.Valor ?? string.Empty,
                TipoNome = tipo?.Valor ?? string.Empty,
                ParametroChave = parametro.Chave,
                ParametroNome = parametro.Nome,
                ParametroStorageType = parametro.StorageType,
                Prefixo = txtPrefixo.Text ?? string.Empty,
                Inicio = inicio,
                Degrau = degrau,
                Sufixo = txtSufixo.Text ?? string.Empty,
                ManterDestaqueAoConcluir = chkManterDestaque.IsChecked == true
            };
        }

        private void CarregarEstadoInicial()
        {
            _atualizando = true;

            txtPrefixo.Text = _settings.LastNumeracaoPrefix ?? string.Empty;
            txtInicio.Text = Math.Max(1, _settings.LastNumeracaoStart).ToString();
            txtDegrau.Text = Math.Max(1, _settings.LastNumeracaoStep).ToString();
            txtSufixo.Text = _settings.LastNumeracaoSuffix ?? string.Empty;
            chkManterDestaque.IsChecked = _settings.LastNumeracaoKeepHighlight;

            NumeracaoEscopo escopo = _uidoc.Selection.GetElementIds().Count > 0
                ? NumeracaoEscopo.SelecaoAtual
                : ParseEscopo(_settings.LastNumeracaoScope);
            rbModeloInteiro.IsChecked = escopo == NumeracaoEscopo.ModeloInteiro;
            rbVistaAtiva.IsChecked = escopo == NumeracaoEscopo.VistaAtiva;
            rbSelecaoAtual.IsChecked = escopo == NumeracaoEscopo.SelecaoAtual;

            _atualizando = false;

            AtualizarCategorias();
            AtualizarPreview();
        }

        private void Scope_Checked(object sender, RoutedEventArgs e)
        {
            if (_atualizando)
                return;

            AtualizarCategorias();
        }

        private void Categoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

            if (!(cmbCategoria.SelectedItem is CategoriaOption))
            {
                AppDialogService.ShowWarning("Numerar Itens", "Selecione uma categoria.", "Dados incompletos");
                return;
            }

            if (!(cmbParametro.SelectedItem is ParametroOption parametro))
            {
                AppDialogService.ShowWarning("Numerar Itens", "Selecione um parâmetro gravável.", "Dados incompletos");
                return;
            }

            if (filtrados.Count == 0)
            {
                AppDialogService.ShowWarning("Numerar Itens", "Nenhum elemento elegível foi encontrado com os filtros atuais.", "Nenhum item encontrado");
                return;
            }

            if (!int.TryParse(txtInicio.Text, out int inicio))
            {
                AppDialogService.ShowWarning("Numerar Itens", "Informe um valor inteiro válido para o início.", "Valor inicial inválido");
                return;
            }

            if (!int.TryParse(txtDegrau.Text, out int degrau) || degrau <= 0)
            {
                AppDialogService.ShowWarning("Numerar Itens", "Informe um degrau inteiro maior que zero.", "Degrau inválido");
                return;
            }

            if (parametro.StorageType == StorageType.Integer &&
                (!string.IsNullOrWhiteSpace(txtPrefixo.Text) || !string.IsNullOrWhiteSpace(txtSufixo.Text)))
            {
                AppDialogService.ShowWarning("Numerar Itens", "Parâmetros inteiros não aceitam prefixo ou sufixo nesta versão.", "Formato incompatível");
                return;
            }

            SalvarPreferencias();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void AtualizarCategorias()
        {
            _atualizando = true;

            List<NumeracaoElementoInfo> candidatos = ObterOuCarregarCandidatos(ObterEscopoSelecionado());
            List<CategoriaOption> categorias = candidatos
                .GroupBy(x => new { x.CategoriaId.Value, x.CategoriaNome })
                .Select(x => new CategoriaOption(new ElementId(x.Key.Value), x.Key.CategoriaNome, x.Count()))
                .OrderBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            cmbCategoria.Items.Clear();
            foreach (CategoriaOption categoria in categorias)
                cmbCategoria.Items.Add(categoria);

            if (!SelecionarCategoriaPreferida(categorias) && cmbCategoria.Items.Count > 0)
                cmbCategoria.SelectedIndex = 0;

            _atualizando = false;
            AtualizarFamilias();
        }

        private void AtualizarFamilias()
        {
            _atualizando = true;

            List<NumeracaoElementoInfo> elementosCategoria = ObterElementosDaCategoriaSelecionada();
            List<TextOption> familias = elementosCategoria
                .GroupBy(x => x.FamiliaNome)
                .Select(x => new TextOption(x.Key, $"{x.Key} ({x.Count()})"))
                .OrderBy(x => x.Texto, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            cmbFamilia.Items.Clear();
            cmbFamilia.Items.Add(new TextOption(string.Empty, TodasFamilias));
            foreach (TextOption familia in familias)
                cmbFamilia.Items.Add(familia);

            if (!SelecionarTextoPreferido(cmbFamilia, _settings.LastNumeracaoFamilyName))
                cmbFamilia.SelectedIndex = cmbFamilia.Items.Count > 0 ? 0 : -1;

            _atualizando = false;
            AtualizarTipos();
        }

        private void AtualizarTipos()
        {
            _atualizando = true;

            List<NumeracaoElementoInfo> elementosFamilia = ObterElementosAteFamiliaSelecionada();
            List<TextOption> tipos = elementosFamilia
                .GroupBy(x => x.TipoNome)
                .Select(x => new TextOption(x.Key, $"{x.Key} ({x.Count()})"))
                .OrderBy(x => x.Texto, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            cmbTipo.Items.Clear();
            cmbTipo.Items.Add(new TextOption(string.Empty, TodosTipos));
            foreach (TextOption tipo in tipos)
                cmbTipo.Items.Add(tipo);

            if (!SelecionarTextoPreferido(cmbTipo, _settings.LastNumeracaoTypeName))
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

            if (!SelecionarParametroPreferido(parametros) && cmbParametro.Items.Count > 0)
                cmbParametro.SelectedIndex = 0;

            _atualizando = false;
            AtualizarPreview();
            AtualizarResumo();
        }

        private void AtualizarResumo()
        {
            List<NumeracaoElementoInfo> candidatos = ObterOuCarregarCandidatos(ObterEscopoSelecionado());
            List<NumeracaoElementoInfo> filtrados = ObterElementosFiltradosDaTela();
            string avisoSelecao = ObterEscopoSelecionado() == NumeracaoEscopo.SelecaoAtual && candidatos.Count == 0
                ? "A seleção atual está vazia.\n"
                : string.Empty;

            string tipoParametro = cmbParametro.SelectedItem is ParametroOption parametro
                ? parametro.StorageType == StorageType.Integer ? "Inteiro" : "Texto"
                : "Nenhum";
            string nomeParametro = cmbParametro.SelectedItem is ParametroOption parametroSelecionado
                ? parametroSelecionado.Nome
                : "Nenhum";

            txtResumo.Text =
                avisoSelecao +
                $"Elementos encontrados no escopo: {candidatos.Count}\n" +
                $"Elementos elegíveis com os filtros: {filtrados.Count}\n" +
                $"Parâmetros graváveis em comum: {cmbParametro.Items.Count}\n" +
                $"Campo que receberá a numeração: {nomeParametro}\n" +
                $"Tipo do parâmetro selecionado: {tipoParametro}\n\n" +
                "Durante a numeração, a ferramenta sempre impede selecionar novamente um item já processado, a menos que você desfaça o último.\n" +
                "Se a tag da vista continuar em '?', confirme se ela está lendo este mesmo campo.";

            AtualizarListaElementos(filtrados);

            btnOk.IsEnabled =
                cmbCategoria.SelectedItem != null &&
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

            txtPreview.Text = $"Próximo valor: {preview}";
        }

        private List<NumeracaoElementoInfo> ObterOuCarregarCandidatos(NumeracaoEscopo escopo)
        {
            if (_cachePorEscopo.TryGetValue(escopo, out List<NumeracaoElementoInfo> candidatos))
                return candidatos;

            candidatos = NumeracaoItensCatalog.ColetarCandidatos(_uidoc, escopo);
            _cachePorEscopo[escopo] = candidatos;
            return candidatos;
        }

        private List<NumeracaoElementoInfo> ObterElementosDaCategoriaSelecionada()
        {
            if (!(cmbCategoria.SelectedItem is CategoriaOption categoria))
                return new List<NumeracaoElementoInfo>();

            return ObterOuCarregarCandidatos(ObterEscopoSelecionado())
                .Where(x => x.CategoriaId.Value == categoria.CategoriaId.Value)
                .ToList();
        }

        private List<NumeracaoElementoInfo> ObterElementosAteFamiliaSelecionada()
        {
            List<NumeracaoElementoInfo> elementos = ObterElementosDaCategoriaSelecionada();
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

        private bool SelecionarCategoriaPreferida(List<CategoriaOption> categorias)
        {
            if (categorias.Count == 0)
                return false;

            CategoriaOption preferida = categorias.FirstOrDefault(x =>
                string.Equals(x.Nome, _settings.LastNumeracaoCategoryName, StringComparison.CurrentCultureIgnoreCase));

            if (preferida == null)
                return false;

            cmbCategoria.SelectedItem = preferida;
            return true;
        }

        private static bool SelecionarTextoPreferido(System.Windows.Controls.ComboBox combo, string valor)
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

        private bool SelecionarParametroPreferido(List<NumeracaoParametroInfo> parametros)
        {
            if (parametros.Count == 0)
                return false;

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
                .FirstOrDefault(x => string.Equals(x.Chave, _settings.LastNumeracaoParameterKey, StringComparison.OrdinalIgnoreCase));

            if (preferido != null)
            {
                cmbParametro.SelectedItem = preferido;
                return true;
            }

            return false;
        }

        private void SalvarPreferencias()
        {
            _settings.LastNumeracaoScope = ObterEscopoSelecionado().ToString();
            _settings.LastNumeracaoCategoryName = (cmbCategoria.SelectedItem as CategoriaOption)?.Nome ?? string.Empty;
            _settings.LastNumeracaoFamilyName = (cmbFamilia.SelectedItem as TextOption)?.Valor ?? string.Empty;
            _settings.LastNumeracaoTypeName = (cmbTipo.SelectedItem as TextOption)?.Valor ?? string.Empty;
            _settings.LastNumeracaoParameterKey = (cmbParametro.SelectedItem as ParametroOption)?.Chave ?? string.Empty;
            _settings.LastNumeracaoPrefix = txtPrefixo.Text ?? string.Empty;
            _settings.LastNumeracaoSuffix = txtSufixo.Text ?? string.Empty;
            _settings.LastNumeracaoStart = TentarLerInteiro(txtInicio.Text, 1);
            _settings.LastNumeracaoStep = TentarLerInteiro(txtDegrau.Text, 1);
            _settings.LastNumeracaoKeepHighlight = chkManterDestaque.IsChecked == true;
            _settings.Save();
        }

        private static NumeracaoEscopo ParseEscopo(string valor)
        {
            return Enum.TryParse(valor, ignoreCase: true, out NumeracaoEscopo escopo)
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
                lstElementos.Items.Add("Nenhum elemento Revit encontrado com os filtros atuais.");
                return;
            }

            foreach (NumeracaoElementoInfo elemento in elementos
                         .OrderBy(x => x.CategoriaNome, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.FamiliaNome, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.TipoNome, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(x => x.Id.Value))
            {
                lstElementos.Items.Add(
                    $"{elemento.CategoriaNome} | {elemento.FamiliaNome} | {elemento.TipoNome} | Id {elemento.Id.Value}");
            }
        }

        private sealed class CategoriaOption
        {
            public CategoriaOption(ElementId categoriaId, string nome, int quantidade)
            {
                CategoriaId = categoriaId;
                Nome = nome;
                Quantidade = quantidade;
            }

            public ElementId CategoriaId { get; }
            public string Nome { get; }
            public int Quantidade { get; }

            public override string ToString()
            {
                return $"{Nome} ({Quantidade})";
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
