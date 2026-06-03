using System.Windows;
using SteelBIM.Services.Layout;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class AlinharVistasWindow : Window
    {
        public AcaoAlinhamentoVista Acao { get; private set; }

        public AlinharVistasWindow()
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);

            btnEsquerda.Click += (_, __) => Escolher(AcaoAlinhamentoVista.Esquerda);
            btnDireita.Click += (_, __) => Escolher(AcaoAlinhamentoVista.Direita);
            btnTopo.Click += (_, __) => Escolher(AcaoAlinhamentoVista.Topo);
            btnBase.Click += (_, __) => Escolher(AcaoAlinhamentoVista.Base);
            btnCentroH.Click += (_, __) => Escolher(AcaoAlinhamentoVista.CentroH);
            btnCentroV.Click += (_, __) => Escolher(AcaoAlinhamentoVista.CentroV);
            btnDistribuirH.Click += (_, __) => Escolher(AcaoAlinhamentoVista.DistribuirH);
            btnDistribuirV.Click += (_, __) => Escolher(AcaoAlinhamentoVista.DistribuirV);
            btnCancelar.Click += (_, __) => DialogResult = false;
        }

        private void Escolher(AcaoAlinhamentoVista acao)
        {
            Acao = acao;
            DialogResult = true;
        }
    }
}
