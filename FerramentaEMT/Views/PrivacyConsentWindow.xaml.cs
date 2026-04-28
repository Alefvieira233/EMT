using System;
using System.Runtime.Versioning;
using System.Windows;
using FerramentaEMT.Models.Privacy;
using FerramentaEMT.Utils;

namespace FerramentaEMT.Views
{
    /// <summary>
    /// Janela generica de consentimento de privacidade.
    ///
    /// Estruturada com N toggles (1 ativo na PR-2: auto-update;
    /// 2 dormentes preparados para PR-3: Sentry e PR-4: PostHog —
    /// comentados no XAML, prontos para descomentar sem refazer layout).
    ///
    /// Mostrada UMA vez quando ConsentVersion local &lt; codigo (ver
    /// CurrentConsentVersion) — o caller decide quando exibir.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class PrivacyConsentWindow : Window
    {
        /// <summary>
        /// Versao do modelo de consentimento desta release.
        /// PR-2: 1 (auto-update apenas).
        /// PR-3: 2 (auto-update + crash reports).  ← atual
        /// PR-4: 3 (auto-update + crash + telemetry).
        ///
        /// Quando incrementar, PrivacyConsentWindow eh reaberta no proximo
        /// boot (ConsentVersion persistido &lt; CurrentConsentVersion do codigo).
        /// Em v1.7.0 todos os usuarios da PR-2 (ConsentVersion=1) veem o
        /// dialog uma vez no primeiro Idling do Revit pos-upgrade.
        /// </summary>
        public const int CurrentConsentVersion = 2;

        /// <summary>
        /// Settings escolhidos pelo usuario, ou null se ele fechou o dialog
        /// sem decidir (X). Caller trata null como Denied (conservador).
        /// </summary>
        public PrivacySettings Result { get; private set; }

        public PrivacyConsentWindow(PrivacySettings current)
        {
            InitializeComponent();
            this.InitializeFerramentaWindow();

            if (current != null)
            {
                cbAutoUpdate.IsChecked = (current.AutoUpdate == ConsentState.Granted);
                cbCrashReports.IsChecked = (current.CrashReports == ConsentState.Granted);
            }

            btnSalvar.Click += BtnSalvar_Click;
            btnNegarTudo.Click += BtnNegarTudo_Click;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            PrivacySettings settings = new PrivacySettings
            {
                ConsentVersion = CurrentConsentVersion,
                AutoUpdate = (cbAutoUpdate.IsChecked == true) ? ConsentState.Granted : ConsentState.Denied,
                CrashReports = (cbCrashReports.IsChecked == true) ? ConsentState.Granted : ConsentState.Denied,
                // PR-4 (Telemetry) ainda Unset: fica pra proxima ConsentVersion
                Telemetry = ConsentState.Unset,
                LastUpdateCheckUtc = DateTime.MinValue,
                SkippedUpdateVersion = string.Empty,
            };
            Result = settings;
            DialogResult = true;
        }

        private void BtnNegarTudo_Click(object sender, RoutedEventArgs e)
        {
            PrivacySettings settings = new PrivacySettings
            {
                ConsentVersion = CurrentConsentVersion,
                AutoUpdate = ConsentState.Denied,
                CrashReports = ConsentState.Denied,
                Telemetry = ConsentState.Denied,
                LastUpdateCheckUtc = DateTime.MinValue,
                SkippedUpdateVersion = string.Empty,
            };
            Result = settings;
            DialogResult = true;
        }
    }
}
