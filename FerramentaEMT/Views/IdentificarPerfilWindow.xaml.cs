#nullable enable
using System;
using System.Windows;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela de configuracao para o comando "Identificar Perfil em Massa".
    /// Permite ao usuario escolher quais categorias identificar, se substituir tags
    /// existentes, e parametros de posicionamento.
    /// </summary>
    public partial class IdentificarPerfilWindow : Window
    {
        public IdentificarPerfilWindow()
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
        public IdentificarPerfilConfig BuildConfig()
        {
            double offsetMm = 300.0;
            if (double.TryParse(txtOffsetTagMm.Text, out double offsetParsed) && offsetParsed >= 0)
                offsetMm = offsetParsed;

            return new IdentificarPerfilConfig
            {
                IncluirVigas = chkIncluirVigas.IsChecked == true,
                IncluirPilares = chkIncluirPilares.IsChecked == true,
                IncluirContraventos = chkIncluirContraventos.IsChecked == true,
                SubstituirTagsExistentes = chkSubstituirTagsExistentes.IsChecked == true,
                CantoneiraDupla = chkCantoneiraDupla.IsChecked == true,
                OffsetTagMm = offsetMm
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            IdentificarPerfilConfig config = BuildConfig();

            if (!config.TemCategoriaSelecionada())
            {
                AppDialogService.ShowWarning(
                    "Identificar Perfil",
                    "Selecione ao menos uma categoria de elementos " +
                    "(vigas, pilares ou contraventos).",
                    "Categoria ausente");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
