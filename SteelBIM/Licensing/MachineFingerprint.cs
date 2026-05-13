using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace FerramentaEMT.Licensing
{
    /// <summary>
    /// Gera um identificador estavel da maquina para amarrar a licenca a um PC especifico.
    /// </summary>
    /// <remarks>
    /// Combina:
    /// - MachineGuid do registro do Windows (HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid)
    ///   - Esse Guid e gerado quando o Windows e instalado e nao muda com troca de hardware.
    /// - Username do usuario atual (Environment.UserName).
    ///   - Permite que duas contas Windows na mesma maquina precisem de licencas separadas.
    ///
    /// Resultado: hash SHA-256 dos dois valores concatenados, codificado em hex maiusculo,
    /// truncado para 16 caracteres (suficiente para evitar colisoes praticas).
    ///
    /// NOTA: nao e infalivel. Reformatar o Windows muda o MachineGuid. O usuario teria que
    /// pedir reativacao para o ALEF (esperado: limite manual de 1-2 reativacoes/ano).
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class MachineFingerprint
    {
        private const string FallbackValue = "EMT-NO-MACHINE-ID";

        /// <summary>
        /// Gera o fingerprint da maquina atual (idempotente).
        /// </summary>
        public static string Current()
        {
            string machineGuid = TryReadMachineGuid() ?? FallbackValue;
            string user = Environment.UserName ?? "anon";

            string raw = $"{machineGuid}|{user}";
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));

            // hex maiusculo, primeiros 16 chars (8 bytes = 64 bits) — colisao praticamente impossivel
            StringBuilder sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static string TryReadMachineGuid()
        {
            try
            {
                // 64-bit registry view — necessario em apps 32-bit lendo HKLM no Windows 64-bit.
                // Para nosso plugin x64 usar Default ja basta, mas explicitar e mais seguro.
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);
                using RegistryKey crypto = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (crypto == null) return null;

                object value = crypto.GetValue("MachineGuid");
                return value?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
