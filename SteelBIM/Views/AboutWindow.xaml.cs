using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Licensing;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela "Sobre". Mostra versao, estado de licenca, dados tecnicos.
    /// Tambem oferece atalho para abrir a tela de ativacao/renovacao.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            this.InitializeFerramentaWindow();

            txtVersao.Text = $"Versão {ResolverVersao()}";
            txtMaquina.Text = MachineFingerprint.Current();
            txtPastaLog.Text = Logger.LogDirectory ?? "—";

            AtualizarPainelLicenca();

            btnFechar.Click += (_, __) => Close();
            btnAtivarOuRenovar.Click += BtnAtivarOuRenovar_Click;
        }

        private void AtualizarPainelLicenca()
        {
            LicenseState state = LicenseService.GetCurrentState();
            txtLicencaTitle.Text = "Estado da Licença: " + DescreverStatus(state.Status);
            txtLicencaCorpo.Text = state.MensagemAmigavel ?? string.Empty;

            txtEmail.Text = string.IsNullOrEmpty(state.Email) ? "—" : state.Email;

            if (state.ExpiresAtUtc.HasValue)
            {
                DateTime local = state.ExpiresAtUtc.Value.ToLocalTime();
                txtValidade.Text = state.DiasRestantes > 0
                    ? $"{local:dd/MM/yyyy} ({state.DiasRestantes} dia(s) restante(s))"
                    : $"{local:dd/MM/yyyy} (expirada)";
            }
            else
            {
                txtValidade.Text = "—";
            }
        }

        private static string DescreverStatus(LicenseStatus status) => status switch
        {
            LicenseStatus.Valid => "Ativa",
            LicenseStatus.Trial => "Período de teste",
            LicenseStatus.Expired => "Expirada",
            LicenseStatus.TrialExpired => "Teste expirado",
            LicenseStatus.NotActivated => "Não ativado",
            LicenseStatus.Tampered => "Arquivo corrompido",
            LicenseStatus.WrongMachine => "Máquina diferente",
            _ => status.ToString(),
        };

        private void BtnAtivarOuRenovar_Click(object sender, RoutedEventArgs e)
        {
            var win = new LicenseActivationWindow { Owner = this };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                AtualizarPainelLicenca();
            }
        }

        private static string ResolverVersao()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Version v = asm.GetName().Version;
                return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
    }
}
