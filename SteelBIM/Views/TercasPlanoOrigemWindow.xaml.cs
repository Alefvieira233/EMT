using FerramentaEMT.Utils;
using System.Windows;

namespace FerramentaEMT.Views
{
    public partial class TercasPlanoOrigemWindow : Window
    {
        public TercasPlanoOrigemWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public bool UsarPlanoTrabalhoAtual => rbPlanoTrabalhoAtual.IsChecked == true;

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
