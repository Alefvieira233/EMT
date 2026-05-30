using System;
using System.Globalization;
using System.Security.Cryptography;
using SteelBIM.Licensing;

namespace EmtKeyGen
{
    /// <summary>
    /// Gerador de chaves de licenca do SteelBIM (ECDsa P-256 / SHA-256 — assimetrico, ADR-011).
    /// USO INTERNO DO ALEF — nunca distribuir para clientes finais.
    ///
    /// Compilar:
    ///   dotnet build tools\EmtKeyGen\EmtKeyGen.csproj -c Release
    ///
    /// Gerar par de chaves (FAZER UMA UNICA VEZ — cole a publica em
    /// SteelBIM/Licensing/LicenseKeys.cs e guarde a privada em segredo):
    ///   dotnet run --project tools\EmtKeyGen -- genkeypair
    ///
    /// Rodar (modo interativo, recomendado):
    ///   dotnet run --project tools\EmtKeyGen
    ///
    /// Rodar (modo argumento, para automacao):
    ///   dotnet run --project tools\EmtKeyGen -- "cliente@exemplo.com" 365
    ///   (gera chave para esse email valida por 365 dias)
    ///
    /// A chave PRIVADA e resolvida via STEELBIM_LICENSE_PRIVATE_KEY (env), ou
    /// %LOCALAPPDATA%\SteelBIM\license.private.key, ou arquivo ao lado do executavel.
    /// NUNCA commitar a chave privada.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Subcomando: genkeypair — gera par ECDsa P-256 e imprime SPKI+PKCS#8 base64.
            if (args != null && args.Length >= 1 &&
                args[0].Equals("genkeypair", StringComparison.OrdinalIgnoreCase))
            {
                using ECDsa ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                Console.WriteLine("=== CHAVE PUBLICA (cole em SteelBIM/Licensing/LicenseKeys.cs -> PublicKeySpkiBase64) ===");
                Console.WriteLine(Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo()));
                Console.WriteLine();
                Console.WriteLine("=== CHAVE PRIVADA (GUARDE EM SEGREDO; env STEELBIM_LICENSE_PRIVATE_KEY; NUNCA commitar) ===");
                Console.WriteLine(Convert.ToBase64String(ec.ExportPkcs8PrivateKey()));
                return 0;
            }

            Console.WriteLine("======================================================");
            Console.WriteLine("  SteelBIM — Gerador de Chaves de Licenca (ECDsa P-256)");
            Console.WriteLine("======================================================");
            Console.WriteLine();

            string email;
            int dias;

            if (args != null && args.Length >= 2)
            {
                email = args[0];
                if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out dias) || dias <= 0)
                {
                    Console.Error.WriteLine("ERRO: o segundo argumento deve ser um numero inteiro positivo de dias.");
                    return 2;
                }
            }
            else
            {
                Console.Write("Email do cliente: ");
                email = (Console.ReadLine() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(email))
                {
                    Console.Error.WriteLine("ERRO: email obrigatorio.");
                    return 2;
                }

                Console.Write("Validade em dias [365]: ");
                string diasStr = (Console.ReadLine() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(diasStr))
                {
                    dias = 365;
                }
                else if (!int.TryParse(diasStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out dias) || dias <= 0)
                {
                    Console.Error.WriteLine("ERRO: dias deve ser um inteiro positivo.");
                    return 2;
                }
            }

            DateTime nowUtc = DateTime.UtcNow;
            DateTime expiresUtc = nowUtc.AddDays(dias);

            var payload = new LicensePayload
            {
                Email = email,
                IssuedAtUnix = ((DateTimeOffset)nowUtc).ToUnixTimeSeconds(),
                ExpiresAtUnix = ((DateTimeOffset)expiresUtc).ToUnixTimeSeconds(),
                Version = 1,
            };

            string token;
            try
            {
                token = KeySigner.Sign(payload);
            }
            catch (InvalidOperationException ex)
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"  ERRO: {ex.Message}");
                Console.ForegroundColor = prev;
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Chave gerada:");
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine(token);
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("Detalhes:");
            Console.WriteLine($"  Email   : {email}");
            Console.WriteLine($"  Emitida : {nowUtc:dd/MM/yyyy HH:mm} UTC");
            Console.WriteLine($"  Expira  : {expiresUtc:dd/MM/yyyy HH:mm} UTC ({dias} dia(s))");
            Console.WriteLine();
            Console.WriteLine("Cole o conteudo entre as linhas no email do cliente.");
            Console.WriteLine("O cliente deve usar Ribbon → Licenca → Ativar Licenca.");
            return 0;
        }
    }
}
