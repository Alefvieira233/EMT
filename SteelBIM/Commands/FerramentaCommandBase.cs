using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Infrastructure.Telemetry;
using FerramentaEMT.Licensing;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Classe base para todos os IExternalCommand do FerramentaEMT.
    /// Fornece try/catch padrao, logging com Stopwatch e tratamento
    /// uniforme de OperationCanceledException.
    /// </summary>
    /// <remarks>
    /// Para usar:
    /// <code>
    /// [Transaction(TransactionMode.Manual)]
    /// public class CmdMinhaCoisa : FerramentaCommandBase
    /// {
    ///     protected override string CommandName => "Minha Coisa";
    ///
    ///     protected override Result ExecuteCore(UIDocument uidoc, Document doc)
    ///     {
    ///         // ... logica do comando
    ///         return Result.Succeeded;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [Transaction(TransactionMode.Manual)]
    public abstract class FerramentaCommandBase : IExternalCommand
    {
        /// <summary>
        /// Nome amigavel do comando (usado em logs e diálogos de erro).
        /// </summary>
        protected abstract string CommandName { get; }

        /// <summary>
        /// Indica se este comando requer licenca valida (paga ou trial).
        /// Padrao: true. Sobrescreva apenas em casos especiais (debug interno, etc.).
        /// </summary>
        /// <remarks>
        /// Comandos publicos do sistema de licenca (ativar/sobre) NAO usam essa flag —
        /// eles implementam IExternalCommand diretamente, fora desta hierarquia.
        /// </remarks>
        protected virtual bool RequiresLicense => true;

        /// <summary>
        /// Logica do comando. Implementacoes nao precisam fazer try/catch
        /// generico — a base ja faz isso. Apenas lance excecoes especificas
        /// quando necessario.
        /// </summary>
        /// <param name="uidoc">UIDocument ativo.</param>
        /// <param name="doc">Documento Revit ativo.</param>
        /// <returns>Result.Succeeded, Result.Cancelled ou Result.Failed.</returns>
        protected abstract Result ExecuteCore(UIDocument uidoc, Document doc);

        /// <summary>
        /// Implementacao final do IExternalCommand.Execute.
        /// Nao sobrescreva — sobrescreva ExecuteCore em vez disso.
        /// </summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var sw = Stopwatch.StartNew();
            string commandName = CommandName ?? GetType().Name;

            try
            {
                Logger.Info("[{Cmd}] iniciando", commandName);

                // ===== LICENSE GATE =====
                // Bloqueia execucao se nao tem licenca valida nem trial ativo.
                // Comandos que NAO devem ser bloqueados (ex.: ativar/sobre) NAO devem
                // herdar de FerramentaCommandBase — devem implementar IExternalCommand direto.
                if (RequiresLicense)
                {
                    LicenseState licState = LicenseService.GetCurrentState();
                    if (!licState.PodeUsar)
                    {
                        Logger.Warn("[{Cmd}] BLOQUEADO — licenca: {Status}", commandName, licState.Status);
                        AppDialogService.ShowWarning(
                            commandName,
                            (licState.MensagemAmigavel ?? "Licença não disponível.") +
                            "\n\nClique em \"Ativar Licença\" no painel \"Licença\" para continuar.",
                            "Funcionalidade bloqueada");

                        // Abre direto a janela de ativacao para o usuario
                        try { new LicenseActivationWindow().ShowDialog(); }
                        catch (Exception ex) { Logger.Error(ex, "[{Cmd}] falha abrindo LicenseActivationWindow", commandName); }

                        return Result.Cancelled;
                    }
                }

                UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
                if (uidoc == null)
                {
                    Logger.Warn("[{Cmd}] UIDocument nulo — aborting", commandName);
                    AppDialogService.ShowError(
                        commandName,
                        "Nenhum documento ativo. Abra um projeto antes de executar este comando.",
                        "Documento indisponivel");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;
                if (doc == null)
                {
                    Logger.Warn("[{Cmd}] Document nulo — aborting", commandName);
                    AppDialogService.ShowError(
                        commandName,
                        "Documento Revit indisponivel.",
                        "Documento indisponivel");
                    return Result.Failed;
                }

                Result result = ExecuteCore(uidoc, doc);

                sw.Stop();
                Logger.Info("[{Cmd}] concluido em {Elapsed}ms — {Result}",
                    commandName, sw.ElapsedMilliseconds, result);

                // PR-4: telemetria — command.executed (sucesso ou Failed/Cancelled
                // retornado pelo proprio command sem excecao). Sample rate 10% se
                // sucesso, 100% se falha (SamplingDecider).
                TrackCommandExecuted(commandName, sw.ElapsedMilliseconds, result == Result.Succeeded);

                return result;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                sw.Stop();
                Logger.Info("[{Cmd}] cancelado pelo usuario apos {Elapsed}ms",
                    commandName, sw.ElapsedMilliseconds);
                // Cancelado eh "success path" — nao conta como falha.
                TrackCommandExecuted(commandName, sw.ElapsedMilliseconds, true);
                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException revitEx)
            {
                sw.Stop();
                Logger.Error(revitEx, "[{Cmd}] InvalidOperationException do Revit apos {Elapsed}ms",
                    commandName, sw.ElapsedMilliseconds);
                message = revitEx.Message;
                AppDialogService.ShowError(
                    commandName,
                    "Operacao invalida no Revit:\n\n" + revitEx.Message,
                    "Falha de operacao Revit");
                TrackCommandFailed(commandName, sw.ElapsedMilliseconds, revitEx);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Logger.Error(ex, "[{Cmd}] FALHOU apos {Elapsed}ms",
                    commandName, sw.ElapsedMilliseconds);
                message = ex.Message;
                AppDialogService.ShowError(
                    commandName,
                    "Erro inesperado:\n\n" + ex.Message +
                    "\n\nDetalhes foram salvos no log:\n" + Logger.LogDirectory,
                    "Falha");
                TrackCommandFailed(commandName, sw.ElapsedMilliseconds, ex);
                return Result.Failed;
            }
        }

        // ============== PR-4: telemetry hooks ==============

        private static void TrackCommandExecuted(string commandName, long durationMs, bool success)
        {
            try
            {
                TelemetryReporter.Track(new TelemetryEvent(
                    SamplingDecider.EventCommandExecuted,
                    new Dictionary<string, object>
                    {
                        { "command_name", commandName ?? "unknown" },
                        { "duration_ms", durationMs },
                        { "success", success },
                    }));
            }
            catch { /* defensivo: TelemetryReporter ja eh try/catch raiz */ }
        }

        private static void TrackCommandFailed(string commandName, long durationMs, Exception ex)
        {
            try
            {
                TelemetryReporter.Track(new TelemetryEvent(
                    SamplingDecider.EventCommandFailed,
                    new Dictionary<string, object>
                    {
                        { "command_name", commandName ?? "unknown" },
                        { "exception_type", ex?.GetType().Name ?? "unknown" },
                        { "duration_ms", durationMs },
                    }));
            }
            catch { /* defensivo */ }
        }

        // =====================================================================
        // Sprint 4: helpers de feedback padronizado para subclasses
        // (todos os comandos devem usar essas APIs ao final da execucao para
        // manter consistencia visual e facilitar futuras mudancas)
        // =====================================================================

        /// <summary>
        /// Exibe um dialogo de sucesso padronizado.
        /// </summary>
        protected void ShowSuccess(string message, string headline = null)
        {
            AppDialogService.ShowInfo(
                CommandName ?? GetType().Name,
                message ?? string.Empty,
                headline ?? "Concluido");
        }

        /// <summary>
        /// Exibe um dialogo de aviso padronizado (operacao parcial, nada a fazer, etc).
        /// </summary>
        protected void ShowWarning(string message, string headline = null)
        {
            AppDialogService.ShowWarning(
                CommandName ?? GetType().Name,
                message ?? string.Empty,
                headline ?? "Atencao");
        }

        /// <summary>
        /// Exibe um dialogo de informacao neutro (resumo de operacao sem destaque positivo/negativo).
        /// </summary>
        protected void ShowInfo(string message, string headline = null)
        {
            AppDialogService.ShowInfo(
                CommandName ?? GetType().Name,
                message ?? string.Empty,
                headline);
        }

        /// <summary>
        /// Solicita confirmacao ao usuario antes de prosseguir com operacao destrutiva.
        /// </summary>
        protected bool Confirm(string message, string headline = null,
            string confirmText = "Continuar", string cancelText = "Cancelar")
        {
            return AppDialogService.ShowConfirmation(
                CommandName ?? GetType().Name,
                message ?? string.Empty,
                headline,
                confirmText,
                cancelText);
        }

        /// <summary>
        /// Helper "no-op feedback": comando rodou sem nada a fazer (selecao vazia, vista invalida, etc).
        /// Loga em Info e mostra um aviso amigavel — retorna sempre Result.Cancelled
        /// para que o caller possa fazer "return NothingToDo(...)".
        /// </summary>
        protected Result NothingToDo(string reason)
        {
            Logger.Info("[{Cmd}] nada a fazer: {Reason}", CommandName ?? GetType().Name, reason);
            ShowWarning(reason, "Nada a fazer");
            return Result.Cancelled;
        }
    }
}
