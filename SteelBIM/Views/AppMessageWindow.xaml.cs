using FerramentaEMT.Utils;
using System.Windows;
using System.Windows.Media;

namespace FerramentaEMT.Views
{
    public partial class AppMessageWindow : Window
    {
        internal AppMessageWindow(
            string title,
            string headline,
            string message,
            AppDialogTone tone,
            string primaryButtonText = "OK",
            string secondaryButtonText = null)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            Title = string.IsNullOrWhiteSpace(title) ? "Mensagem" : title;
            txtHeadline.Text = string.IsNullOrWhiteSpace(headline) ? Title : headline;
            txtMessage.Text = message ?? string.Empty;

            btnPrimary.Content = string.IsNullOrWhiteSpace(primaryButtonText) ? "OK" : primaryButtonText;
            btnPrimary.Click += BtnPrimary_Click;
            btnPrimary.IsDefault = true;

            if (!string.IsNullOrWhiteSpace(secondaryButtonText))
            {
                btnSecondary.Content = secondaryButtonText;
                btnSecondary.Visibility = Visibility.Visible;
                btnSecondary.Click += BtnSecondary_Click;
                btnSecondary.IsCancel = true;
            }

            ApplyTone(tone);
        }

        private void BtnPrimary_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnSecondary_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ApplyTone(AppDialogTone tone)
        {
            switch (tone)
            {
                case AppDialogTone.Warning:
                    toneCard.Style = (Style)FindResource("WarningCardBorder");
                    txtTone.Text = "Aviso";
                    txtTone.Foreground = (Brush)FindResource("WarningTextBrush");
                    break;
                case AppDialogTone.Error:
                    toneCard.Style = (Style)FindResource("WarningCardBorder");
                    txtTone.Text = "Erro";
                    txtTone.Foreground = (Brush)FindResource("WarningTextBrush");
                    break;
                default:
                    toneCard.Style = (Style)FindResource("AccentCardBorder");
                    txtTone.Text = "Informacao";
                    txtTone.Foreground = (Brush)FindResource("AccentCardTextBrush");
                    break;
            }
        }
    }
}
