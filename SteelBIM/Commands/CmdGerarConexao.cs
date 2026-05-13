#nullable enable
using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Services.Conexoes;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    /// <summary>
    /// Comando para gerar conexões estruturais (Chapa de Ponta, Dupla Cantoneira, Chapa Gusset)
    /// entre elementos selecionados (viga a viga ou viga a pilar).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarConexao : FerramentaCommandBase
    {
        protected override string CommandName => "Gerar Conexão";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            // Try/catch geral fica na FerramentaCommandBase. Aqui so deixamos a logica.
            Logger.Info("[CmdGerarConexao] Iniciando seleção de elementos");

            // Pedir seleção da viga principal
            AppDialogService.ShowInfo(
                CommandName,
                "Selecione a viga que será conectada.",
                "Etapa 1 de 2");

            Reference refViga = uidoc.Selection.PickObject(ObjectType.Element);
            if (refViga == null)
                return Result.Cancelled;

            Element elemViga = doc.GetElement(refViga);
            if (elemViga == null || !(elemViga is FamilyInstance fi))
            {
                AppDialogService.ShowError(
                    CommandName,
                    "O elemento selecionado não é uma instância de família. Selecione uma viga.",
                    "Erro de seleção");
                return Result.Failed;
            }

            // Pedir seleção do elemento a conectar (pilar ou outra viga)
            AppDialogService.ShowInfo(
                CommandName,
                "Selecione o elemento a conectar (pilar ou outra viga).",
                "Etapa 2 de 2");

            Reference refConectado = uidoc.Selection.PickObject(ObjectType.Element);
            if (refConectado == null)
                return Result.Cancelled;

            Element elemConectado = doc.GetElement(refConectado);
            if (elemConectado == null)
            {
                AppDialogService.ShowError(
                    CommandName,
                    "Elemento inválido.",
                    "Erro de seleção");
                return Result.Failed;
            }

            // Abrir janela de configuração
            var window = new ConexaoConfigWindow();
            bool? dialogResult = window.ShowDialog();

            if (dialogResult != true)
            {
                Logger.Info("[CmdGerarConexao] Usuário cancelou a configuração");
                return Result.Cancelled;
            }

            var config = window.BuildConfig();
            if (config == null)
            {
                Logger.Error("[CmdGerarConexao] Falha ao construir configuração");
                return Result.Failed;
            }

            // Executar geração
            var service = new ConexaoGeneratorService();
            bool sucesso = service.TentarColocarConexao(doc, fi, elemConectado, config);

            if (sucesso)
            {
                ShowSuccess("Conexão gerada com sucesso.", "Sucesso");
                Logger.Info("[CmdGerarConexao] Conexão gerada com sucesso");
            }
            else
            {
                string? faltante = service.FamiliaNaoCarregada;
                string msg = !string.IsNullOrEmpty(faltante)
                    ? $"A família '{faltante}' NÃO está carregada no modelo.\n\n" +
                      "Para gerar a conexão:\n" +
                      "1) Revit > Insert > Load Family\n" +
                      $"2) Carregue a família '{faltante}.rfa'\n" +
                      "3) Execute 'Gerar Conexão' novamente.\n\n" +
                      "A viga foi marcada em Comments com 'CONEXAO_PENDENTE'."
                    : "Não foi possível posicionar a conexão (geometria inválida). A viga foi marcada como pendente.";
                ShowWarning(msg, "Família não carregada");
                Logger.Warn("[CmdGerarConexao] Conexão marcada como pendente: {Faltante}", faltante ?? "(geometria)");
            }

            return Result.Succeeded;
        }
    }
}
