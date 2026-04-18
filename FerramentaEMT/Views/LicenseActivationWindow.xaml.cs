using System;
using System.Runtime.Versioning;
using System.Windows;
using FerramentaEMT.Licensing;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela de ativacao de licenca. Usuario cola a chave, clica Ativar.
    /// Tambem mostra o estado atual e o fingerprint da maquina (para suporte).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class LicenseActivationWindow : Window
    {
        public LicenseActivationWindow()
        {
            InitializeComponent();
            this.InitializeFerramentaWindow();

            txtFingerprint.Text = MachineFingerprint.Current();

            btnAtivar.Click += BtnAtivar_Click;
            btnFechar.Click += (_, __) => { DialogResult = false; };
            btnCopiarFingerprint.Click += BtnCopiarFingerprint_Click;

            AtualizarStatus();
        }

        private void AtualizarStatus()
        {
            LicenseState state = LicenseService.GetCurrentState();
            txtStatusTitle.Text = $"Estado atual: {DescreverStatus(state.Status)}";
            txtStatusBody.Text = state.MensagemAmigavel ?? string.Empty;
        }

        private static string DescreverStatus(LicenseStatus status) => status switch
        {
            LicenseStatus.Valid => "Licença ativa",
            LicenseStatus.Trial => "Período de teste",
            LicenseStatus.Expired => "Licença expirada",
            LicenseStatus.TrialExpired => "Teste expirado",
            LicenseStatus.NotActivated => "Não ativado",
            LicenseStatus.Tampered => "Arquivo corrompido",
            LicenseStatus.WrongMachine => "Máquina diferente",
            _ => status.ToString(),
        };

        private void BtnAtivar_Click(object sender, RoutedEventArgs e)
        {
            string token = txtChave.Text?.Trim();
            LicenseState result = LicenseService.Activate(token);

            ExibirFeedback(result);

            if (result.Status == LicenseStatus.Valid)
            {
                // Pequena pausa visual seria bom, mas como e modal e simples,
                // fechamos direto com sucesso. O caller mostra o aviso.
                DialogResult = true;
            }
            else
            {
                AtualizarStatus();
            }
        }

        private void ExibirFeedback(LicenseState result)
        {
            if (result == null) return;
            txtFeedback.Text = result.MensagemAmigavel ?? string.Empty;
            borderFeedback.Visibility = string.IsNullOrEmpty(txtFeedback.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnCopiarFingerprint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtFingerprint.Text ?? string.Empty);
                btnCopiarFingerprint.Content = "Copiado";
            }
            catch
            {
                // Clipboard pode falhar em sessoes RDP ou se outro app travar a area
                btnCopiarFingerprint.Content = "Falhou";
            }
        }
    }
}
