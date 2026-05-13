using Autodesk.Revit.UI;
using FerramentaEMT.Views;

namespace FerramentaEMT.Utils
{
    internal enum AppDialogTone
    {
        Info,
        Warning,
        Error
    }

    internal static class AppDialogService
    {
        internal static void ShowInfo(string title, string message, string headline = null)
        {
            Show(AppDialogTone.Info, title, message, headline);
        }

        internal static void ShowWarning(string title, string message, string headline = null)
        {
            Show(AppDialogTone.Warning, title, message, headline);
        }

        internal static void ShowError(string title, string message, string headline = null)
        {
            Show(AppDialogTone.Error, title, message, headline);
        }

        internal static bool ShowConfirmation(
            string title,
            string message,
            string headline = null,
            string confirmText = "Continuar",
            string cancelText = "Cancelar")
        {
            try
            {
                AppMessageWindow window = new AppMessageWindow(
                    title,
                    headline,
                    message,
                    AppDialogTone.Warning,
                    confirmText,
                    cancelText);
                return window.ShowDialog() == true;
            }
            catch
            {
                TaskDialog dialog = new TaskDialog(title ?? "Confirmacao")
                {
                    MainInstruction = string.IsNullOrWhiteSpace(headline) ? (title ?? "Confirmacao") : headline,
                    MainContent = message ?? string.Empty,
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };

                return dialog.Show() == TaskDialogResult.Yes;
            }
        }

        private static void Show(AppDialogTone tone, string title, string message, string headline)
        {
            try
            {
                AppMessageWindow window = new AppMessageWindow(title, headline, message, tone);
                window.ShowDialog();
            }
            catch
            {
                TaskDialog.Show(title ?? "Mensagem", message ?? string.Empty);
            }
        }
    }
}
