#nullable enable
using System;
using System.Windows;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela de configuracao para o comando "Tagear Treliça".
    /// Permite ao usuario escolher quais membros tagear e parametros de posicionamento.
    /// </summary>
    public partial class TagearTrelicaWindow : Window
    {
        public TagearTrelicaWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => { DialogResult = false; Close(); };

            // ESC fecha como cancelar
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }

        /// <summary>
        /// Constroi a configuracao a partir dos valores da interface.
        /// </summary>
        public TagearTrelicaConfig BuildConfig()
        {
            double offsetMm = 300.0;
            if (NumberParsing.TryParseDouble(txtOffsetTagMm.Text, out double offsetParsed) && offsetParsed >= 0)
                offsetMm = offsetParsed;

            return new TagearTrelicaConfig
            {
                TagearBanzoSuperior = chkTagearBanzoSuperior.IsChecked == true,
                TagearBanzoInferior = chkTagearBanzoInferior.IsChecked == true,
                TagearMontantes = chkTagearMontantes.IsChecked == true,
                TagearDiagonais = chkTagearDiagonais.IsChecked == true,
                CantoneiraDupla = chkCantoneiraDupla.IsChecked == true,
                CriarRotuloBanzos = chkCriarRotuloBanzos.IsChecked == true,
                OffsetTagMm = offsetMm
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            TagearTrelicaConfig config = BuildConfig();

            if (!config.TemTipoSelecionado())
            {
                AppDialogService.ShowWarning(
                    "Tagear Treliça",
                    "Selecione ao menos um tipo de membro " +
                    "(banzo superior, banzo inferior, montantes ou diagonais).",
                    "Tipo ausente");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
