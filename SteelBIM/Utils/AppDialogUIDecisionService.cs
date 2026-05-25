using SteelBIM.Core;

namespace SteelBIM.Utils
{
    /// <summary>
    /// v2.7.9 (ADR-003 template): adapter de producao que implementa
    /// <see cref="IUIDecisionService"/> delegando para o
    /// <see cref="AppDialogService"/> static existente. Permite que
    /// services injetem a interface sem mudar o caminho real de
    /// renderizacao do dialogo (WPF + fallback TaskDialog do Revit).
    ///
    /// <para>
    /// <b>Por que internal:</b> alinhado ao <c>AppDialogService</c> que tambem
    /// e <c>internal static</c>. Acessivel pelos services dentro do mesmo
    /// assembly SteelBIM.dll. Tests usam <c>Mock&lt;IUIDecisionService&gt;</c>
    /// direto via Moq, nao precisam tocar o adapter.
    /// </para>
    /// </summary>
    internal sealed class AppDialogUIDecisionService : IUIDecisionService
    {
        public void Info(string title, string message, string? headline = null)
            => AppDialogService.ShowInfo(title, message, headline);

        public void Warn(string title, string message, string? headline = null)
            => AppDialogService.ShowWarning(title, message, headline);

        public void Error(string title, string message, string? headline = null)
            => AppDialogService.ShowError(title, message, headline);

        public bool Confirm(
            string title,
            string message,
            string? headline = null,
            string confirmText = "Continuar",
            string cancelText = "Cancelar")
            => AppDialogService.ShowConfirmation(title, message, headline, confirmText, cancelText);
    }
}
