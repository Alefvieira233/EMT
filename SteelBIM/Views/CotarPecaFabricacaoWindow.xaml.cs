using System.Linq;
using System.Windows;
using FerramentaEMT.Models;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    public partial class CotarPecaFabricacaoWindow : Window
    {
#pragma warning disable CS0649
        private bool _atualizando;
#pragma warning restore CS0649

        public CotarPecaFabricacaoWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnOk.Click += BtnOk_Click;
            btnCancel.Click += (_, __) => DialogResult = false;

            AtualizarResumo();
        }

        public CotarPecaFabricacaoConfig BuildConfig()
        {
            double offset = 250;
            if (NumberParsing.TryParseDouble(txtOffset.Text, out double parsed) && parsed > 0)
                offset = parsed;

            return new CotarPecaFabricacaoConfig
            {
                Escopo = rbVistaAtiva.IsChecked == true
                    ? EscopoCotagem.VistaAtiva
                    : EscopoCotagem.SelecaoManual,
                CotarComprimento = chkComprimento.IsChecked == true,
                CotarAlturaPerfil = chkAlturaPerfil.IsChecked == true,
                CotarLarguraMesa = chkLarguraMesa.IsChecked == true,
                CotarFuros = chkFuros.IsChecked == true,
                CotarDistanciaBorda = chkDistBorda.IsChecked == true,
                OffsetCotaMm = offset,
                UsarCotasCorridas = chkCotasCorridas.IsChecked == true
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            CotarPecaFabricacaoConfig config = BuildConfig();

            if (!config.TemCotaSelecionada())
            {
                AppDialogService.ShowWarning("Cotagem de Fabricação",
                    "Selecione ao menos um tipo de cota para gerar.",
                    "Seleção incompleta");
                return;
            }

            DialogResult = true;
        }

        private void Configuracao_Changed(object sender, RoutedEventArgs e)
        {
            if (_atualizando) return;
            AtualizarResumo();
        }

        private void AtualizarResumo()
        {
            if (txtResumo == null) return;

            string escopo = rbVistaAtiva?.IsChecked == true
                ? "Todos os elementos da vista ativa"
                : "Seleção manual";

            string cotas = string.Join(", ", new[]
            {
                chkComprimento?.IsChecked == true ? "Comprimento" : null,
                chkAlturaPerfil?.IsChecked == true ? "Altura (d)" : null,
                chkLarguraMesa?.IsChecked == true ? "Largura (bf)" : null,
                chkFuros?.IsChecked == true ? "Furos" : null,
                chkDistBorda?.IsChecked == true ? "Dist. borda" : null,
            }.Where(x => x != null));

            if (string.IsNullOrEmpty(cotas))
                cotas = "Nenhuma";

            string corridas = chkCotasCorridas?.IsChecked == true ? "Sim" : "Não";

            txtResumo.Text =
                $"Escopo: {escopo}\n" +
                $"Cotas: {cotas}\n" +
                $"Offset: {txtOffset?.Text ?? "250"} mm\n" +
                $"Cotas corridas: {corridas}\n\n" +
                "As cotas serão criadas automaticamente na vista ativa. " +
                "O comprimento usa as faces de extremidade da peça. " +
                "Altura e largura usam as faces das mesas do perfil.";
        }
    }
}
