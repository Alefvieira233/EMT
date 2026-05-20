using FluentAssertions;
using SteelBIM.Infrastructure;
using Xunit;

namespace SteelBIM.Tests.Infrastructure
{
    /// <summary>
    /// Cobre os 2 padroes que o PiiScrubber tem que remover antes de qualquer
    /// evento ir pro Sentry ou pra telemetria PostHog: email e path Windows
    /// com username. Tambem cobre o invariante de NAO mexer no que nao deve
    /// (paths Linux, stack frames).
    /// </summary>
    public class PiiScrubberTests
    {
        // ---------- Emails ----------

        [Fact]
        public void Scrubs_simple_email()
        {
            string input = "Falha ao enviar para joao@exemplo.com";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be("Falha ao enviar para <EMAIL>");
        }

        [Fact]
        public void Scrubs_email_with_subdomain()
        {
            string input = "Erro: a.b@sub.dominio.co.uk nao encontrado";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain("<EMAIL>");
            output.Should().NotContain("a.b@");
            output.Should().NotContain("sub.dominio.co.uk");
        }

        [Fact]
        public void Scrubs_email_with_plus_tag()
        {
            string input = "user+tag@gmail.com";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be("<EMAIL>");
        }

        [Fact]
        public void Scrubs_email_with_dash_and_dot()
        {
            string input = "alef-christian.gomes@empresa.com.br lances";
            string output = PiiScrubber.Scrub(input);
            output.Should().StartWith("<EMAIL>");
            output.Should().NotContain("alef-christian");
        }

        [Fact]
        public void Scrubs_multiple_emails_in_same_string()
        {
            string input = "From: a@x.com To: b@y.com Cc: c@z.com";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be("From: <EMAIL> To: <EMAIL> Cc: <EMAIL>");
        }

        // ---------- Windows paths ----------

        [Fact]
        public void Scrubs_windows_user_path()
        {
            // v2.6.1: tambem scrubba o nome do .rvt (Projeto Vulcaflex.rvt
            // etc. frequentemente identifica cliente). Stack frame com
            // C:\Users\<user>\Desktop\projeto.rvt agora vira
            // <USER>\Desktop\<REVIT_FILE>.rvt.
            string input = @"Falha em C:\Users\joao\Desktop\projeto.rvt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"Falha em <USER>\Desktop\<REVIT_FILE>.rvt");
        }

        [Fact]
        public void Scrubs_lowercase_drive_and_users()
        {
            string input = @"em c:\users\maria\AppData\Local\arquivo.log";
            string output = PiiScrubber.Scrub(input);
            // Username sumiu, resto preservado.
            output.Should().Contain(@"<USER>\AppData\Local\arquivo.log");
            output.Should().NotContain(@"maria");
        }

        [Fact]
        public void Scrubs_multiple_windows_paths_in_stack_trace()
        {
            string input =
                "at Foo() in C:\\Users\\joao\\src\\Foo.cs:line 10\r\n" +
                "at Bar() in C:\\Users\\joao\\src\\Bar.cs:line 22";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain(@"<USER>\src\Foo.cs:line 10");
            output.Should().Contain(@"<USER>\src\Bar.cs:line 22");
            output.Should().NotContain("joao");
        }

        // ---------- Out-of-scope (paths que NAO devem ser tocados) ----------

        [Fact]
        public void Does_not_touch_linux_paths()
        {
            string input = "/home/joao/projeto/file.cs";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(input);
        }

        [Fact]
        public void Scrubs_unc_paths_server_and_share()
        {
            // v2.6.1 (hotfix P0 SECURITY-2): UNC paths AGORA sao scrubbed.
            // O share name frequentemente carrega nome de cliente
            // ('\\nas01\projeto-vulcaflex\') — scrub server+share previne
            // o vazamento principal. Gap conhecido: username apos o share
            // continua exposto (aceito pela auditoria — ver PiiScrubber.cs
            // comentario sobre WindowsUncPathRegex).
            string input = @"\\fileserver\share\joao\arquivo.txt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"<UNC>\joao\arquivo.txt");
        }

        // ---------- Stack frame preservation ----------

        [Fact]
        public void Preserves_class_and_method_names_in_stack_frame()
        {
            string input =
                "SteelBIM.Services.PfRebarService.GerarEstribosPilar() " +
                "in C:\\Users\\joao\\dev\\FerramentaEMT\\Services\\PF\\PfRebarService.cs:line 312";
            string output = PiiScrubber.Scrub(input);

            output.Should().Contain("SteelBIM.Services.PfRebarService.GerarEstribosPilar()");
            output.Should().Contain("PfRebarService.cs:line 312");
            output.Should().NotContain("joao");
        }

        // ---------- Defensive ----------

        [Fact]
        public void Null_input_returns_null_without_throwing()
        {
            string output = PiiScrubber.Scrub(null);
            output.Should().BeNull();
        }

        [Fact]
        public void Empty_input_returns_empty()
        {
            PiiScrubber.Scrub(string.Empty).Should().Be(string.Empty);
        }

        [Fact]
        public void Combined_email_and_path_in_same_string()
        {
            // Cenario realista: exception message com email do usuario E
            // path absoluto com .rvt. Os tres somem (email, username, filename).
            // v2.6.1: filename .rvt agora tambem e scrubbed.
            string input =
                "User joao@empresa.com falhou em C:\\Users\\joao\\Desktop\\modelo.rvt";
            string output = PiiScrubber.Scrub(input);

            output.Should().Contain("<EMAIL>");
            output.Should().Contain(@"<USER>\Desktop\<REVIT_FILE>.rvt");
            output.Should().NotContain("joao@empresa.com");
            output.Should().NotContain("modelo.rvt"); // filename scrubed
            // Username "joao" some 3 vezes (email + path + filename context)
        }

        // ============================================================
        // v2.6.1 (hotfix P0 SECURITY-2): novos padroes
        // ============================================================

        // ---------- Windows path localizado PT-BR ----------

        [Fact]
        public void Scrubs_windows_user_path_ptbr_sem_acento()
        {
            string input = @"em C:\Usuarios\maria\Documentos\nota.txt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"em <USER>\Documentos\nota.txt");
        }

        [Fact]
        public void Scrubs_windows_user_path_ptbr_com_acento()
        {
            string input = @"em D:\Usuários\joão\Documentos\file.log";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain(@"<USER>\Documentos\file.log");
            output.Should().NotContain(@"joão");
        }

        [Fact]
        public void Scrubs_windows_user_path_ptbr_e_en_no_mesmo_string()
        {
            string input = @"first C:\Users\a\b\ then D:\Usuários\b\c\";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"first <USER>\b\ then <USER>\c\");
        }

        // ---------- UNC paths ----------

        [Fact]
        public void Scrubs_unc_path_with_client_share_name()
        {
            // Cenario tipico EMT: NAS com share por cliente.
            string input = @"erro lendo \\nas01\projetos-vulcaflex\modelo.rvt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain("<UNC>\\");
            output.Should().NotContain("nas01");
            output.Should().NotContain("vulcaflex");
        }

        [Fact]
        public void Scrubs_multiple_unc_paths()
        {
            string input = @"\\srv1\sh1\a\ vs \\srv2\sh2\b\";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"<UNC>\a\ vs <UNC>\b\");
        }

        // ---------- Revit filenames ----------

        [Theory]
        [InlineData("modelo.rvt", "<REVIT_FILE>.rvt")]
        [InlineData("template.rte", "<REVIT_FILE>.rte")]
        [InlineData("base.rft", "<REVIT_FILE>.rft")]
        [InlineData("MODELO.RVT", "<REVIT_FILE>.RVT")] // case preservada na extensao
        public void Scrubs_revit_filename_standalone(string input, string expected)
        {
            PiiScrubber.Scrub(input).Should().Be(expected);
        }

        [Fact]
        public void Scrubs_revit_filename_multiword_partial()
        {
            // Limitacao conhecida (v2.6.1): nome de arquivo com espacos tem
            // partial leak — apenas a ULTIMA palavra antes da extensao e
            // scrubbed. "Familia Coluna.rfa" → "Familia <REVIT_FILE>.rfa".
            // Aceita pela auditoria porque (a) palavras genericas tipo
            // "Familia"/"Projeto"/"Modelo" nao identificam cliente sozinhas,
            // (b) regex menos restritiva traz risco de gobbling sentencas
            // inteiras quando .rvt aparece em mensagem de log. Documentado
            // no PiiScrubber.cs (RevitFilenameRegex).
            string output = PiiScrubber.Scrub("Familia Coluna.rfa");
            output.Should().Be("Familia <REVIT_FILE>.rfa");
        }

        [Fact]
        public void Scrubs_revit_filename_inside_sentence()
        {
            string input = "Failed to open Projeto Vulcaflex.rvt at line 42";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain("<REVIT_FILE>.rvt");
            output.Should().NotContain("Vulcaflex");
        }

        [Fact]
        public void Does_not_scrub_non_revit_extensions()
        {
            // .cs, .xaml, .log, .txt preservados — nao sao Revit-specific
            // e seu nome geralmente nao e PII.
            string input = "stack: at Foo() in MarcarPecasService.cs:12 ; log em emt.log";
            string output = PiiScrubber.Scrub(input);
            output.Should().Contain("MarcarPecasService.cs");
            output.Should().Contain("emt.log");
            output.Should().NotContain("<REVIT_FILE>");
        }

        [Fact]
        public void Does_not_scrub_substring_match()
        {
            // 'extensao.rfton' (palavra que contem 'rft' mas nao termina la)
            // — word boundary impede match falso.
            string input = "myfile.rftensao should stay";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(input);
        }

        // ---------- Regression guards P0 SECURITY-2 (v2.6.1) ----------

        [Fact]
        public void NaoRegredeV261_SECURITY2_ServerName_em_stack_path()
        {
            // Cenario realista: stack frame com path do cliente em NAS.
            string input =
                @"System.IO.FileNotFoundException at \\nas01\projetos-vulcaflex\modelo.rvt";
            string output = PiiScrubber.Scrub(input);
            output.Should().NotContain("nas01");
            output.Should().NotContain("vulcaflex");
            output.Should().NotContain("modelo.rvt");
            output.Should().Contain("<UNC>\\");
            output.Should().Contain("<REVIT_FILE>.rvt");
        }

        [Fact]
        public void NaoRegredeV261_SECURITY2_PtBr_em_locale_brasileiro()
        {
            // Cobre: locale ptBR (Usuários) + filename multi-word.
            // Username e ULTIMA palavra do filename viram scrubbed; o "Projeto"
            // (palavra generica nao-identificadora) fica como leak documentado.
            string input = @"Caminho: C:\Usuários\fulano\Documents\Projeto Tal.rvt";
            string output = PiiScrubber.Scrub(input);
            output.Should().NotContain("fulano");
            output.Should().NotContain("Tal.rvt"); // ultima palavra do filename
            output.Should().Contain(@"<USER>\Documents\");
            output.Should().Contain("<REVIT_FILE>.rvt");
        }
    }
}
