using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace FerramentaEMT.SetupBootstrapper;

/// <summary>
/// Dialogo modal de aceitacao dos Termos de Uso (EULA) antes da
/// instalacao. PR-6 (P0.5 da auditoria) deixa esta janela com
/// comportamento DESABILITADO por padrao porque os documentos legais
/// (EULA.draft.md, PRIVACY.draft.md, TOS.draft.md) ainda estao em
/// revisao juridica.
///
/// Quando o advogado revisar e os documentos forem renomeados de
/// EULA.draft.md → EULA.md, basta:
///   1. Mudar <see cref="ShowEulaPrompt"/> para true.
///   2. Atualizar o texto exibido (LoadEulaText) para apontar pra
///      EULA.md final ou embedar como resource.
///   3. Remover o auto-check do checkbox para que o usuario
///      ative manualmente.
///
/// Comportamento atual (rascunho):
///   - Janela NAO eh exibida (ShowEulaPrompt = false). Skipped no
///     InstallerSession.Run.
///   - Caso seja forcada via flag, exibe um aviso curto + checkbox
///     auto-marcado + texto "termos provisorios pendentes de revisao
///     juridica" (defesa em caso de toggle acidental).
///
/// Decisao registrada em ADR-010 §"EULA prompt skeleton".
/// </summary>
internal static class EulaConfirmation
{
    /// <summary>
    /// Quando true, InstallerSession.Run exibe a janela antes da
    /// instalacao. Default false ate revisao juridica.
    /// </summary>
    public const bool ShowEulaPrompt = false;

    /// <summary>
    /// Quando true, checkbox vem auto-marcado (rascunho).
    /// Apos revisao juridica, mudar para false para forcar leitura
    /// + acao consciente do usuario.
    /// </summary>
    public const bool AutoCheckByDefault = true;

    /// <summary>
    /// Mostra o dialogo. Retorna true se aceito, false se cancelado.
    /// Em modo quiet (instalacao silenciosa), aceita automaticamente
    /// (caller eh responsavel por esse fluxo legal — geralmente
    /// invocado por scripts corporativos com aceite previo).
    /// </summary>
    public static bool RequestAcceptance(bool quiet)
    {
        if (!ShowEulaPrompt) return true;
        // CS0162: codigo a partir daqui eh "inacessivel" enquanto
        // ShowEulaPrompt = false (skeleton). Suprimir intencional —
        // quando advogado aprovar, basta flipar ShowEulaPrompt = true.
#pragma warning disable CS0162
        if (quiet) return true;

        using EulaForm form = new EulaForm();
        DialogResult result = form.ShowDialog();
        return result == DialogResult.OK && form.Accepted;
#pragma warning restore CS0162
    }

    private sealed class EulaForm : Form
    {
        private readonly TextBox _textBox;
        private readonly CheckBox _checkBox;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public bool Accepted { get; private set; }

        public EulaForm()
        {
            Text = "FerramentaEMT — Termos de Uso (RASCUNHO)";
            Width = 640;
            Height = 480;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label header = new Label
            {
                Text = "Termos de Uso — RASCUNHO PROVISORIO",
                Font = new System.Drawing.Font(Font.FontFamily, 11, System.Drawing.FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(12, 8, 12, 0),
            };

            _textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = LoadEulaText(),
                Font = new System.Drawing.Font("Segoe UI", 9),
            };

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                Padding = new Padding(12),
            };

            _checkBox = new CheckBox
            {
                Text = "Li e aceito os termos",
                Checked = AutoCheckByDefault,
                Dock = DockStyle.Top,
                Height = 24,
            };

            Label disclaimer = new Label
            {
                Text = "Termos provisorios — pendente revisao juridica.",
                ForeColor = System.Drawing.Color.FromArgb(160, 0, 0),
                Font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Italic),
                Dock = DockStyle.Top,
                Height = 18,
            };

            _btnOk = new Button
            {
                Text = "Instalar",
                DialogResult = DialogResult.OK,
                Width = 100,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Top = 50,
                Left = bottomPanel.Width - 220,
            };
            _btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Top = 50,
                Left = bottomPanel.Width - 110,
            };

            _btnOk.Click += (_, _) => { Accepted = _checkBox.Checked; };

            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(_btnOk);
            bottomPanel.Controls.Add(disclaimer);
            bottomPanel.Controls.Add(_checkBox);

            Controls.Add(_textBox);
            Controls.Add(bottomPanel);
            Controls.Add(header);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private static string LoadEulaText()
        {
            // Texto curto provisorio enquanto rascunho. Quando advogado
            // revisar, embedar EULA.md completo como recurso e carregar
            // via Assembly.GetManifestResourceStream.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FerramentaEMT — TERMOS DE USO (RASCUNHO PROVISORIO)");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine("Os Termos de Uso desta versao estao em revisao juridica.");
            sb.AppendLine("A versao final estara disponivel em release futura.");
            sb.AppendLine();
            sb.AppendLine("O rascunho atual cobre, em resumo:");
            sb.AppendLine();
            sb.AppendLine("  - Licenca nao-exclusiva, intransferivel, 1 maquina por padrao.");
            sb.AppendLine("  - Sem reverse engineering nem revenda.");
            sb.AppendLine("  - Software fornecido 'NO ESTADO EM QUE SE ENCONTRA' (AS-IS).");
            sb.AppendLine("  - O Software NAO substitui a responsabilidade tecnica do");
            sb.AppendLine("    engenheiro estrutural pelo projeto. Revisao final eh");
            sb.AppendLine("    de responsabilidade exclusiva do Licenciado.");
            sb.AppendLine("  - Coleta de dados conforme Politica de Privacidade.");
            sb.AppendLine("  - Lei brasileira aplicavel.");
            sb.AppendLine();
            sb.AppendLine("Documentos completos em:");
            sb.AppendLine("  docs/legal/EULA.draft.md");
            sb.AppendLine("  docs/legal/PRIVACY.draft.md");
            sb.AppendLine("  docs/legal/TOS.draft.md");
            sb.AppendLine();
            sb.AppendLine("Apos revisao juridica, esta tela exibira o EULA completo.");
            sb.AppendLine();
            sb.AppendLine("Ao prosseguir, voce confirma ciencia destes termos provisorios.");
            return sb.ToString();
        }
    }
}
