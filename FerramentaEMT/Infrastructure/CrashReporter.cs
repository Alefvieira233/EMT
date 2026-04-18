using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FerramentaEMT.Infrastructure
{
    /// <summary>
    /// Captura excecoes nao observadas no plugin e as dumpa em arquivo local.
    ///
    /// Fase 4 do plano 100/100 preve integracao com Sentry.NET (remote crash
    /// reporting). Esta classe e o skeleton: hoje ela ja capta crashes e
    /// escreve em %LOCALAPPDATA%\FerramentaEMT\crashes\ — util para o usuario
    /// anexar ao bug report mesmo sem backend remoto.
    ///
    /// Zero custo quando nao ha crashes. Inicializar em App.OnStartup apos
    /// o Logger.
    /// </summary>
    public static class CrashReporter
    {
        private static bool _initialized;
        private static readonly object _lock = new object();

        /// <summary>Diretorio onde os dumps de crash sao gravados.</summary>
        public static string CrashDirectory { get; private set; }

        /// <summary>
        /// Registra handlers globais de excecao. Idempotente.
        ///
        /// TODO (fase 4): se SENTRY_DSN estiver presente (env var ou
        /// %LOCALAPPDATA%\FerramentaEMT\sentry.dsn), inicializar SentrySdk
        /// e encaminhar os crashes. Package Sentry.NET nao esta referenciado
        /// hoje — adicionar quando comecar a fase 4.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Etapas fallibles PRIMEIRO. Se qualquer uma lanca, o handler nao
                    // sera registrado e um futuro Initialize() pode tentar de novo
                    // sem risco de duplicar subscricao (regressao do audit 2026-04).
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    CrashDirectory = Path.Combine(localAppData, "FerramentaEMT", "crashes");
                    Directory.CreateDirectory(CrashDirectory);

                    // Commit do estado ANTES de registrar handlers OU logar. Se Logger.Info
                    // fosse o culpado de uma excecao no primeiro attempt (antes),
                    // _initialized ficava false e um retry subscrevia os handlers de novo,
                    // produzindo dois dumps por crash.
                    _initialized = true;

                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                    // Log final — isolado num try/catch proprio para que uma falha aqui
                    // nao derrube a captura ja registrada.
                    try { Logger.Info("[CrashReporter] inicializado em {Dir}", CrashDirectory); }
                    catch { /* logger pode falhar sem comprometer captura */ }
                }
                catch (Exception ex)
                {
                    // Falha antes de _initialized=true: handlers nao foram registrados,
                    // CrashDirectory pode estar null. Estado consistente para retry.
                    try { Logger.Error(ex, "[CrashReporter] falha ao inicializar — continuara sem captura"); }
                    catch { /* logger tambem falhou, nada a fazer */ }
                }
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            DumpCrash("unhandled", ex, $"IsTerminating={e.IsTerminating}");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            DumpCrash("unobserved-task", e.Exception, $"Observed={e.Observed}");
            e.SetObserved(); // evita escalar para crash do processo (CLR 4+)
        }

        private static void DumpCrash(string kind, Exception ex, string extraInfo)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                string path = Path.Combine(CrashDirectory ?? Path.GetTempPath(),
                    $"crash-{kind}-{stamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine($"=== FerramentaEMT crash dump ({kind}) ===");
                sb.AppendLine($"UTC:          {DateTime.UtcNow:O}");
                sb.AppendLine($"Local:        {DateTime.Now:O}");
                sb.AppendLine($"Extra:        {extraInfo}");
                sb.AppendLine($"CLR version:  {Environment.Version}");
                sb.AppendLine($"OS version:   {Environment.OSVersion}");
                sb.AppendLine($"Machine:      {Environment.MachineName}");
                sb.AppendLine($"User:         {Environment.UserName}");
                sb.AppendLine();
                if (ex != null)
                {
                    sb.AppendLine("=== Exception ===");
                    sb.AppendLine(ex.ToString());
                }
                else
                {
                    sb.AppendLine("(exception object was null)");
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                try { Logger.Error(ex, "[CrashReporter] crash ({Kind}) dump em {Path}", kind, path); }
                catch { /* ignore */ }
            }
            catch
            {
                // ultima linha de defesa — nao re-lancar dentro de handler global
            }
        }
    }
}
