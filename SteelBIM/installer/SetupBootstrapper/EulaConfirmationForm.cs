using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace SteelBIM.SetupBootstrapper;

/// <summary>
/// Dialogo modal de aceitacao do EULA antes da instalacao.
///
/// v1.7.0 (pre-release soft launch): EULA prompt ATIVADO. O texto eh
/// carregado a partir do recurso embutido <c>SteelBIM.SetupBootstrapper.EULA.md</c>
/// (incluido via &lt;EmbeddedResource&gt; em SetupBootstrapper.csproj a partir
/// de <c>docs/legal/EULA.md</c>). O checkbox de aceite vem desmarcado
/// por padrao, forcando acao explicita do usuario.
///
/// Em modo quiet (/quiet ou --quiet), o aceite eh automatico — caller
/// (geralmente scripts corporativos) eh responsavel pelo aceite legal.
/// </summary>
internal static class EulaConfirmation
{
    private const string EulaResourceName = "SteelBIM.SetupBootstrapper.EULA.md";

    /// <summary>
    /// Quando true, InstallerSession.Run exibe a janela antes da
    /// instalacao. Ativado em v1.7.0.
    /// </summary>
    public const bool ShowEulaPrompt = true;

    /// <summary>
    /// Quando true, checkbox vem auto-marcado. Em v1.7.0 vem
    /// desmarcado: usuario precisa marcar conscientemente.
    /// </summary>
    public const bool AutoCheckByDefault = false;

    /// <summary>
    /// Mostra o dialogo. Retorna true se aceito, false se cancelado.
    /// Em modo quiet (instalacao silenciosa), aceita automaticamente
    /// (caller eh responsavel por esse fluxo legal — geralmente
    /// invocado por scripts corporativos com aceite previo).
    /// </summary>
    public static bool RequestAcceptance(bool quiet)
    {
        // ShowEulaPrompt eh const; quando false, o codigo abaixo eh
        // intencionalmente inacessivel (skeleton pra desativar prompt
        // sem remover a UI). Suprimir CS0162 enquanto v1.7.0+ tem
        // ShowEulaPrompt = true.
#pragma warning disable CS0162
        if (!ShowEulaPrompt) return true;
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
            Text = "SteelBIM — Contrato de Licenca de Usuario Final";
            Width = 720;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label header = new Label
            {
                Text = "Contrato de Licenca de Usuario Final (EULA)",
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
                Text = "Li e aceito os termos do EULA",
                Checked = AutoCheckByDefault,
                Dock = DockStyle.Top,
                Height = 24,
            };

            Label disclaimer = new Label
            {
                Text = "Versao 1.7.0 — pre-release. Documentos legais sujeitos a revisao em versoes futuras.",
                ForeColor = System.Drawing.Color.FromArgb(80, 80, 80),
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
            try
            {
                using Stream? stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(EulaResourceName);
                if (stream is null)
                    return FallbackEulaText();

                using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return FallbackEulaText();
            }
        }

        private static string FallbackEulaText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SteelBIM — Contrato de Licenca de Usuario Final");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine("O texto completo do EULA esta disponivel no repositorio publico:");
            sb.AppendLine("  docs/legal/EULA.md");
            sb.AppendLine();
            sb.AppendLine("Resumo executivo:");
            sb.AppendLine();
            sb.AppendLine("  - Licenca nao-exclusiva, nao-transferivel, revogavel.");
            sb.AppendLine("  - Permitida instalacao em ate 3 computadores de uso pessoal/profissional.");
            sb.AppendLine("  - Vedada engenharia reversa, revenda ou redistribuicao.");
            sb.AppendLine("  - Software fornecido 'como esta' (AS-IS), sem garantias.");
            sb.AppendLine("  - O Plugin NAO substitui a responsabilidade tecnica do");
            sb.AppendLine("    engenheiro estrutural pelo projeto. Revisao final eh");
            sb.AppendLine("    de responsabilidade exclusiva do Licenciado.");
            sb.AppendLine("  - Coleta de dados (opt-in) conforme Politica de Privacidade.");
            sb.AppendLine("  - Reembolso em ate 7 dias (Art. 49 do CDC).");
            sb.AppendLine("  - Lei brasileira aplicavel.");
            sb.AppendLine();
            sb.AppendLine("Ao prosseguir, voce confirma ciencia e aceite destes termos.");
            return sb.ToString();
        }
    }
}
