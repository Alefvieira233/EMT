using FerramentaEMT.Infrastructure;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Infrastructure
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
            string input = @"Falha em C:\Users\joao\Desktop\projeto.rvt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(@"Falha em <USER>\Desktop\projeto.rvt");
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
        public void Does_not_touch_unc_paths()
        {
            // UNC \\server\share\joao\ — diferente de drive letter, nao casa
            // o regex Windows. Se quisermos cobrir UNC no futuro, mudamos
            // o pattern; por enquanto fora de escopo (nao eh PII em CI / dev).
            string input = @"\\fileserver\share\joao\arquivo.txt";
            string output = PiiScrubber.Scrub(input);
            output.Should().Be(input);
        }

        // ---------- Stack frame preservation ----------

        [Fact]
        public void Preserves_class_and_method_names_in_stack_frame()
        {
            string input =
                "FerramentaEMT.Services.PfRebarService.GerarEstribosPilar() " +
                "in C:\\Users\\joao\\dev\\FerramentaEMT\\Services\\PF\\PfRebarService.cs:line 312";
            string output = PiiScrubber.Scrub(input);

            output.Should().Contain("FerramentaEMT.Services.PfRebarService.GerarEstribosPilar()");
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
            // path absoluto. Os dois somem.
            string input =
                "User joao@empresa.com falhou em C:\\Users\\joao\\Desktop\\modelo.rvt";
            string output = PiiScrubber.Scrub(input);

            output.Should().Contain("<EMAIL>");
            output.Should().Contain(@"<USER>\Desktop\modelo.rvt");
            output.Should().NotContain("joao@empresa.com");
            // Username "joao" some duas vezes (uma do email, uma do path).
        }
    }
}
