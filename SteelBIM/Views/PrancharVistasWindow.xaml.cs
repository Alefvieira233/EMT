using System.Collections.Generic;
using System.Windows;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class PrancharVistasWindow : Window
    {
        public PrancharVistasWindow(IReadOnlyList<(string Familia, string Tipo)> titleBlocks, int numVistas)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            txtResumoSelecao.Text = $"{numVistas} vista(s) selecionada(s) para a prancha.";

            foreach ((string fam, string tipo) in titleBlocks)
                cmbTitleBlock.Items.Add(new TitleBlockItem(fam, tipo));
            if (cmbTitleBlock.Items.Count > 0)
                cmbTitleBlock.SelectedIndex = 0;

            cmbOrdenar.Items.Add("Nome (TR-2 antes de TR-10)");
            cmbOrdenar.Items.Add("Escala");
            cmbOrdenar.Items.Add("Ordem de seleção");
            cmbOrdenar.SelectedIndex = 0;

            btnCancel.Click += (_, __) => DialogResult = false;
            btnOk.Click += BtnOk_Click;
        }

        public PrancharVistasConfig? BuildConfig()
        {
            if (cmbTitleBlock.SelectedItem is not TitleBlockItem tb)
                return null;

            double margem = 20.0, espac = 10.0, reserva = 0.0;
            NumberParsing.TryParseDouble(txtMargem.Text, out margem);
            NumberParsing.TryParseDouble(txtEspacamento.Text, out espac);
            NumberParsing.TryParseDouble(txtReservaCarimbo.Text, out reserva);

            int? colunas = null;
            if (int.TryParse(txtColunas.Text?.Trim(), out int c) && c > 0)
                colunas = c;

            OrdenacaoVista ord = cmbOrdenar.SelectedIndex switch
            {
                1 => OrdenacaoVista.Escala,
                2 => OrdenacaoVista.Selecao,
                _ => OrdenacaoVista.Nome
            };

            return new PrancharVistasConfig
            {
                FamiliaTitleBlock = tb.Familia,
                TipoTitleBlock = tb.Tipo,
                MargemMm = margem,
                EspacamentoMm = espac,
                ReservaCarimboMm = reserva,
                Colunas = colunas,
                Ordenar = ord,
                NumeroFolha = txtNumero.Text?.Trim() ?? string.Empty,
                NomeFolha = txtNome.Text?.Trim() ?? "PRANCHA"
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTitleBlock.SelectedItem == null)
            {
                AppDialogService.ShowWarning("Pranchar Vistas", "Selecione um carimbo (formato da folha).", "Dados incompletos");
                return;
            }

            DialogResult = true;
        }

        private sealed class TitleBlockItem
        {
            public string Familia { get; }
            public string Tipo { get; }

            public TitleBlockItem(string familia, string tipo)
            {
                Familia = familia;
                Tipo = tipo;
            }

            public override string ToString() => $"{Familia} : {Tipo}";
        }
    }
}
