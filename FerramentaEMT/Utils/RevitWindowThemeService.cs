using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace FerramentaEMT.Utils
{
    internal static class RevitWindowThemeService
    {
        private static readonly Uri LightThemeUri =
            new Uri("/FerramentaEMT;component/Views/Themes/AppTheme.Light.xaml", UriKind.Relative);

        private static readonly Uri DarkThemeUri =
            new Uri("/FerramentaEMT;component/Views/Themes/AppTheme.Dark.xaml", UriKind.Relative);

        private static readonly List<WeakReference<Window>> Windows = new List<WeakReference<Window>>();

        private static UIControlledApplication _application;
        private static UITheme _currentTheme = UIThemeManager.CurrentTheme;
        private static bool _initialized;

        public static void Initialize(UIControlledApplication application)
        {
            if (_initialized || application == null)
                return;

            _application = application;
            _currentTheme = UIThemeManager.CurrentTheme;
            _application.ThemeChanged += OnThemeChanged;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized)
                return;

            _application.ThemeChanged -= OnThemeChanged;
            _application = null;
            Windows.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Anexa a janela ao servico de tema. Aplica o tema corrente e registra
        /// um handler de tecla ESC que fecha a janela (DialogResult = false para dialogs).
        /// Pressionar ESC fecha a janela de forma consistente, melhorando UX.
        /// </summary>
        public static void Attach(Window window)
        {
            if (window == null)
                return;

            PruneDeadReferences();
            Windows.Add(new WeakReference<Window>(window));
            ApplyTheme(window, _currentTheme);
            AttachEscapeHandler(window);
        }

        /// <summary>
        /// Registra um handler PreviewKeyDown que fecha a janela quando ESC e' pressionado.
        /// Usa PreviewKeyDown para capturar a tecla ANTES de qualquer controle filho.
        /// Se a janela foi aberta como ShowDialog(), define DialogResult = false.
        /// Se nao, apenas chama Close().
        /// </summary>
        private static void AttachEscapeHandler(Window window)
        {
            window.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape && !e.Handled)
                {
                    try
                    {
                        // Tenta setar DialogResult (so funciona se a janela foi aberta com ShowDialog)
                        window.DialogResult = false;
                    }
                    catch (InvalidOperationException)
                    {
                        // Janela nao foi aberta como ShowDialog — apenas fecha
                        window.Close();
                    }
                    e.Handled = true;
                }
            };
        }

        private static void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (e == null || e.ThemeChangedType != ThemeType.UITheme)
                return;

            _currentTheme = UIThemeManager.CurrentTheme;

            foreach (Window window in GetLiveWindows())
            {
                window.Dispatcher.BeginInvoke(new Action(() => ApplyTheme(window, _currentTheme)));
            }
        }

        private static void ApplyTheme(Window window, UITheme theme)
        {
            if (window == null)
                return;

            List<ResourceDictionary> themeDictionaries = window.Resources.MergedDictionaries
                .Where(IsThemeDictionary)
                .ToList();

            foreach (ResourceDictionary dictionary in themeDictionaries)
                window.Resources.MergedDictionaries.Remove(dictionary);

            window.Resources.MergedDictionaries.Insert(0, new ResourceDictionary
            {
                Source = theme == UITheme.Dark ? DarkThemeUri : LightThemeUri
            });
        }

        private static bool IsThemeDictionary(ResourceDictionary dictionary)
        {
            string source = dictionary?.Source?.OriginalString ?? string.Empty;
            return source.EndsWith("AppTheme.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                   source.EndsWith("AppTheme.Dark.xaml", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<Window> GetLiveWindows()
        {
            PruneDeadReferences();
            return Windows
                .Select(x => x.TryGetTarget(out Window window) ? window : null)
                .Where(x => x != null)
                .ToList();
        }

        private static void PruneDeadReferences()
        {
            Windows.RemoveAll(x => !x.TryGetTarget(out Window _));
        }
    }
}
