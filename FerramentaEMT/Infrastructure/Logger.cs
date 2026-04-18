using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace FerramentaEMT.Infrastructure
{
    /// <summary>
    /// Wrapper estatico do Serilog para uso em todo o projeto.
    /// Inicialize uma vez em App.OnStartup chamando Logger.Initialize().
    /// </summary>
    /// <remarks>
    /// Logs sao salvos em:
    ///   %LocalAppData%\FerramentaEMT\logs\emt-YYYYMMDD.log
    /// Rotacao diaria, retencao de 30 dias.
    /// </remarks>
    public static class Logger
    {
        private static bool _initialized;
        private static readonly object _lock = new object();

        /// <summary>
        /// Diretorio onde os logs sao gravados.
        /// </summary>
        public static string LogDirectory { get; private set; }

        /// <summary>
        /// Caminho do arquivo de log atual.
        /// </summary>
        public static string CurrentLogFile { get; private set; }

        /// <summary>
        /// Inicializa o sistema de logging. Idempotente — chamar varias vezes
        /// e seguro mas so configura na primeira chamada.
        /// </summary>
        /// <param name="minimumLevel">
        /// Nivel minimo de log. Padrao Information.
        /// Use Debug em desenvolvimento para ver mais detalhes.
        /// </param>
        public static void Initialize(LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    LogDirectory = Path.Combine(localAppData, "FerramentaEMT", "logs");
                    Directory.CreateDirectory(LogDirectory);

                    CurrentLogFile = Path.Combine(LogDirectory, "emt-.log");

                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Is(minimumLevel)
                        .Enrich.WithProperty("Application", "FerramentaEMT")
                        .Enrich.WithProperty("Version", typeof(Logger).Assembly.GetName().Version?.ToString() ?? "unknown")
                        .Enrich.WithProperty("MachineName", Environment.MachineName)
                        .Enrich.WithProperty("UserName", Environment.UserName)
                        .WriteTo.File(
                            path: CurrentLogFile,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 30,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                            shared: true)
                        .CreateLogger();

                    _initialized = true;

                    Log.Information("=== FerramentaEMT iniciado ===");
                    Log.Information("Log dir: {LogDirectory}", LogDirectory);
                }
                catch (Exception ex)
                {
                    // Logging deve ser silencioso em caso de falha de inicializacao.
                    // O ultimo recurso e o Debug.WriteLine.
                    System.Diagnostics.Debug.WriteLine($"[FerramentaEMT.Logger] Falha ao inicializar: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Encerra o logger (chamar em App.OnShutdown).
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (!_initialized) return;
                Log.Information("=== FerramentaEMT encerrado ===");
                Log.CloseAndFlush();
                _initialized = false;
            }
        }

        // ---------------------------------------------------------------
        // Atalhos delegados (para nao precisar fazer using Serilog em todo lado)
        // ---------------------------------------------------------------

        public static void Debug(string message) => Log.Debug(message);
        public static void Debug(string template, params object[] args) => Log.Debug(template, args);

        public static void Info(string message) => Log.Information(message);
        public static void Info(string template, params object[] args) => Log.Information(template, args);

        public static void Warn(string message) => Log.Warning(message);
        public static void Warn(string template, params object[] args) => Log.Warning(template, args);
        public static void Warn(Exception ex, string message) => Log.Warning(ex, message);
        public static void Warn(Exception ex, string template, params object[] args) => Log.Warning(ex, template, args);

        public static void Error(string message) => Log.Error(message);
        public static void Error(string template, params object[] args) => Log.Error(template, args);
        public static void Error(Exception ex, string message) => Log.Error(ex, message);
        public static void Error(Exception ex, string template, params object[] args) => Log.Error(ex, template, args);

        public static void Fatal(string message) => Log.Fatal(message);
        public static void Fatal(Exception ex, string message) => Log.Fatal(ex, message);
        public static void Fatal(Exception ex, string template, params object[] args) => Log.Fatal(ex, template, args);
    }
}
