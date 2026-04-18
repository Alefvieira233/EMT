using System;
using System.Globalization;
using FerramentaEMT.Licensing;

namespace EmtKeyGen
{
    /// <summary>
    /// Gerador de chaves de licenca do FerramentaEMT.
    /// USO INTERNO DO ALEF — nunca distribuir para clientes finais.
    ///
    /// Compilar:
    ///   dotnet build tools\EmtKeyGen\EmtKeyGen.csproj -c Release
    ///
    /// Rodar (modo interativo, recomendado):
    ///   dotnet run --project tools\EmtKeyGen
    ///
    /// Rodar (modo argumento, para automacao):
    ///   dotnet run --project tools\EmtKeyGen -- "cliente@exemplo.com" 365
    ///   (gera chave para esse email valida por 365 dias)
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("======================================================");
            Console.WriteLine("  FerramentaEMT — Gerador de Chaves de Licenca");
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

            string token = KeySigner.Sign(payload);

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
