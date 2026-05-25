namespace SteelBIM.Core
{
    /// <summary>
    /// v2.7.9 (ADR-003 template): abstracao pra dialogo com usuario via UI,
    /// injetada nos services pra eliminar acoplamento direto com
    /// <c>AppDialogService</c> static. Services que recebem essa interface
    /// se tornam <b>mudos</b> (testaveis sem WPF/Revit) — caller passa um
    /// <see cref="System.IServiceProvider"/>, mock ou adapter real.
    ///
    /// <para>
    /// <b>Implementacoes:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><c>SteelBIM.Utils.AppDialogUIDecisionService</c> — producao (Revit + WPF)</item>
    /// <item><c>Moq&lt;IUIDecisionService&gt;</c> — testes (verify chamadas)</item>
    /// </list>
    ///
    /// <para>
    /// <b>Quando criar um service novo:</b> aceitar
    /// <c>IUIDecisionService? ui = null</c> no construtor; cair em
    /// <c>new AppDialogUIDecisionService()</c> se null (backward-compat com
    /// callers que nao injetam).
    /// </para>
    ///
    /// <para>
    /// <b>Quando NAO usar:</b> em Commands (entry-points Revit) — esses
    /// podem usar <c>AppDialogService</c> direto, pois eles JA sao a
    /// fronteira UI. ADR-003 diz: "services mudos, commands falantes".
    /// </para>
    /// </summary>
    public interface IUIDecisionService
    {
        /// <summary>
        /// Mostra mensagem informativa (geralmente: resumo de sucesso ao final
        /// de operacao longa). Fire-and-forget — nao espera retorno.
        /// </summary>
        void Info(string title, string message, string? headline = null);

        /// <summary>
        /// Mostra aviso de validacao (geralmente: entrada incompleta ou
        /// estado nao-suportado, mas operacao pode continuar/abortar
        /// graciosamente). Fire-and-forget.
        /// </summary>
        void Warn(string title, string message, string? headline = null);

        /// <summary>
        /// Mostra erro critico (geralmente: falha de operacao que ja iniciou,
        /// excecao Revit, dado corrompido). Fire-and-forget.
        /// </summary>
        void Error(string title, string message, string? headline = null);

        /// <summary>
        /// Pergunta confirmacao ao usuario (modal). Retorna <c>true</c> se
        /// usuario clicou em <paramref name="confirmText"/>, <c>false</c> se
        /// clicou em <paramref name="cancelText"/> ou fechou (Esc/X).
        /// </summary>
        bool Confirm(
            string title,
            string message,
            string? headline = null,
            string confirmText = "Continuar",
            string cancelText = "Cancelar");
    }
}
