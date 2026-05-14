using System.Windows;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class CotasModoWindow : Window
    {
        public CotasModoWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public bool UsarFaces => rbFaces.IsChecked == true;

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
