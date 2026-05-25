using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SteelBIM.Infrastructure.Update
{
    /// <summary>
    /// Implementacao de producao do <see cref="IAuthenticodeVerifier"/> usando
    /// WinVerifyTrust (wintrust.dll). Verifica:
    ///   - Assinatura presente
    ///   - Hash do arquivo bate com a assinatura (binario nao foi adulterado pos-sign)
    ///   - Cadeia de certificados eh valida ate um root no trust store local
    ///   - Cert valido (ou com timestamp counter-signature valida quando expirado)
    ///
    /// <para>v2.7.10 (auditoria 2026-05-25 §5.3) — defense-in-depth supply chain
    /// no auto-update. SHA256 do .zip ja confirma "binario veio do GitHub
    /// release", mas se o repo for comprometido o atacante pode trocar
    /// .zip + manifesto juntos. Authenticode adiciona camada independente:
    /// cert + chain + root trust nao podem ser forjados sem comprometer a CA.</para>
    ///
    /// <para>Win32-only. Em hosts sem wintrust.dll (Linux CI, mesmo que improvavel
    /// dado nosso TFM net8.0-windows), retorna <see cref="AuthenticodeVerifyResult.NotSupported"/>
    /// e o caller deve tratar como soft-pass.</para>
    /// </summary>
    public sealed class WinTrustAuthenticodeVerifier : IAuthenticodeVerifier
    {
        // GUID da policy "generic verify v2" — verifica chain + hash + timestamp.
        private static readonly Guid WinTrustActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        // Constantes documentadas em wintrust.h.
        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_CHOICE_FILE = 1;
        private const uint WTD_STATEACTION_VERIFY = 1;
        private const uint WTD_STATEACTION_CLOSE = 2;

        // HResults retornados por WinVerifyTrust (winerror.h).
        private const uint TRUST_E_SUCCESS = 0x00000000;
        private const uint TRUST_E_NOSIGNATURE = 0x800B0100;
        private const uint TRUST_E_PROVIDER_UNKNOWN = 0x800B0001;
        private const uint TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003;
        private const uint TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004;
        private const uint TRUST_E_BAD_DIGEST = 0x80096010;
        private const uint CERT_E_UNTRUSTEDROOT = 0x800B0109;
        private const uint CERT_E_CHAINING = 0x800B010A;
        private const uint CERT_E_EXPIRED = 0x800B0101;
        private const uint CERT_E_REVOKED = 0x800B010C;

        public AuthenticodeVerifyResult VerifyFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return AuthenticodeVerifyResult.Error;
            if (!File.Exists(filePath))
                return AuthenticodeVerifyResult.Error;

            IntPtr fileInfoPtr = IntPtr.Zero;
            WINTRUST_DATA data = null;
            try
            {
                WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                    pcwszFilePath = filePath,
                    hFile = IntPtr.Zero,
                    pgKnownSubject = IntPtr.Zero,
                };
                fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
                Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

                data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WTD_UI_NONE,
                    fdwRevocationChecks = WTD_REVOKE_NONE,
                    dwUnionChoice = WTD_CHOICE_FILE,
                    pFile = fileInfoPtr,
                    dwStateAction = WTD_STATEACTION_VERIFY,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = IntPtr.Zero,
                    dwProvFlags = 0,
                    dwUIContext = 0,
                };

                uint hr;
                try
                {
                    hr = WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, data);
                }
                catch (DllNotFoundException)
                {
                    return AuthenticodeVerifyResult.NotSupported;
                }
                catch (EntryPointNotFoundException)
                {
                    return AuthenticodeVerifyResult.NotSupported;
                }

                // Cleanup obrigatorio do state interno do wintrust antes de
                // sair (sem isso vaza handle por chamada).
                try
                {
                    data.dwStateAction = WTD_STATEACTION_CLOSE;
                    WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, data);
                }
                catch
                {
                    // Cleanup best-effort; nao deve mascarar o resultado real.
                }

                return MapHResult(hr);
            }
            catch
            {
                return AuthenticodeVerifyResult.Error;
            }
            finally
            {
                if (fileInfoPtr != IntPtr.Zero)
                {
                    try
                    { Marshal.FreeHGlobal(fileInfoPtr); }
                    catch { /* best effort */ }
                }
            }
        }

        internal static AuthenticodeVerifyResult MapHResult(uint hr)
        {
            switch (hr)
            {
                case TRUST_E_SUCCESS:
                    return AuthenticodeVerifyResult.Verified;
                case TRUST_E_NOSIGNATURE:
                case TRUST_E_PROVIDER_UNKNOWN:
                case TRUST_E_SUBJECT_FORM_UNKNOWN:
                    return AuthenticodeVerifyResult.NoSignature;
                case TRUST_E_SUBJECT_NOT_TRUSTED:
                case CERT_E_UNTRUSTEDROOT:
                case CERT_E_CHAINING:
                case CERT_E_REVOKED:
                    return AuthenticodeVerifyResult.UntrustedRoot;
                case CERT_E_EXPIRED:
                    return AuthenticodeVerifyResult.Expired;
                case TRUST_E_BAD_DIGEST:
                    return AuthenticodeVerifyResult.SignatureBroken;
                default:
                    return AuthenticodeVerifyResult.Error;
            }
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
        private static extern uint WinVerifyTrust(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
            WINTRUST_DATA pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private sealed class WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }
    }
}
