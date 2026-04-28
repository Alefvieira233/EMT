using System;

namespace FerramentaEMT.Infrastructure.Update
{
    /// <summary>
    /// Logging facade puro para o subsistema de auto-update.
    /// Defaults sao no-op (testes nao precisam configurar).
    /// App.OnStartup configura os delegates para encaminhar ao Serilog
    /// via FerramentaEMT.Infrastructure.Logger.
    ///
    /// Existe porque UpdateCheckService eh testado em xUnit, e o test csproj
    /// nao referencia Serilog (incluir Logger.cs quebraria a compilacao do
    /// test). Esta classe nao tem dependencia externa — Action delegates puros.
    /// </summary>
    public static class UpdateLog
    {
        public static Action<string, object[]> Debug { get; set; } = NoOp;
        public static Action<string, object[]> Info { get; set; } = NoOp;
        public static Action<string, object[]> Warn { get; set; } = NoOp;
        public static Action<Exception, string, object[]> WarnException { get; set; } = NoOpEx;

        private static void NoOp(string template, object[] args) { }
        private static void NoOpEx(Exception ex, string template, object[] args) { }

        /// <summary>
        /// Reseta delegates para no-op. Util em testes que querem
        /// garantir isolamento.
        /// </summary>
        public static void Reset()
        {
            Debug = NoOp;
            Info = NoOp;
            Warn = NoOp;
            WarnException = NoOpEx;
        }
    }
}
