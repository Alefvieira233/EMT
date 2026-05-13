using System;
using System.Windows;

namespace FerramentaEMT.Utils
{
    /// <summary>
    /// Sprint 4: helpers de inicializacao padrao para janelas WPF do FerramentaEMT.
    /// Centraliza configuracoes que antes eram repetidas em cada window
    /// (tema do Revit, atalho ESC para fechar, posicionamento, etc.).
    /// </summary>
    /// <remarks>
    /// Uso (substituir em cada window):
    /// <code>
    /// // ANTES
    /// RevitWindowThemeService.Attach(this);
    ///
    /// // DEPOIS
    /// this.InitializeFerramentaWindow();
    /// </code>
    /// O metodo antigo continua funcionando — esta migracao e opcional e nao quebra.
    /// </remarks>
    internal static class WindowExtensions
    {
        /// <summary>
        /// Aplica configuracoes padrao do FerramentaEMT a uma janela WPF.
        /// Registra tema (light/dark sincronizado com Revit), ESC para fechar,
        /// e posicionamento padrao (centro da tela).
        /// Idempotente — chamadas repetidas nao reaplicam handlers.
        /// </summary>
        /// <param name="window">Janela WPF.</param>
        /// <param name="closeOnEscape">Se true (padrao), fecha a janela ao apertar ESC.
        /// A partir de v1.0.6, ESC e' registrado em RevitWindowThemeService.Attach()
        /// automaticamente. Este parametro e' mantido por compatibilidade.</param>
        public static void InitializeFerramentaWindow(this Window window, bool closeOnEscape = true)
        {
            if (window == null)
                return;

            // 1. Tema sincronizado com Revit (light/dark) + ESC handler
            // (A partir de v1.0.6, RevitWindowThemeService.Attach ja registra ESC automaticamente)
            RevitWindowThemeService.Attach(window);

            // 2. Posicionamento padrao (so se nao tiver sido configurado no XAML)
            if (window.WindowStartupLocation == WindowStartupLocation.Manual)
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}
