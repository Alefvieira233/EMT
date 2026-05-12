using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FerramentaEMT.Models;
using FerramentaEMT.Services;
using FerramentaEMT.Utils;
using FerramentaEMT.Views;

namespace FerramentaEMT.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdGerarContraventamentoPlano : FerramentaCommandBase
    {
        protected override string CommandName => "Contraventamento";

        protected override Result ExecuteCore(UIDocument uidoc, Document doc)
        {
            Plane plane = ObterPlanoDoPlanoDeTrabalhoAtual(doc);
            if (plane == null)
                return Result.Cancelled;

            List<FamilySymbol> listaPerfis = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .ToList();

            if (listaPerfis.Count == 0)
            {
                ShowWarning("Nenhuma familia estrutural foi encontrada.", "Perfis nao encontrados");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettings.Load();
            ContraventamentoPlanoWindow wnd = new ContraventamentoPlanoWindow(listaPerfis, settings);
            bool? result = wnd.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            ContraventamentoPlanoConfig config = wnd.BuildConfig();
            if (config == null || config.SymbolSelecionado == null)
            {
                ShowWarning("Configuracao invalida.", "Dados incompletos");
                return Result.Cancelled;
            }

            try
            {
                Func<bool> confirmarPreview = () => AppDialogService.ShowConfirmation(
                    CommandName,
                    "Foi gerada uma pre-visualizacao temporaria do contraventamento.\n\nConfirma a criacao definitiva?",
                    "Confirmar modelagem",
                    "Criar",
                    "Cancelar");

                ContraventamentoPlanoService service = new ContraventamentoPlanoService();
                Result resultadoRevit = service.Executar(
                    uidoc, doc, config, plane,
                    out ContraventamentoPlanoResultado resultado,
                    confirmarPreview);

                ApresentarResultado(resultado);
                return resultadoRevit;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(CommandName, ex.Message, "Falha ao gerar contraventamento");
                return Result.Failed;
            }
        }

        private void ApresentarResultado(ContraventamentoPlanoResultado resultado)
        {
            if (resultado == null)
                return;

            if (resultado.Sucesso)
            {
                ShowInfo(resultado.ToResumoSucesso(), "Modelagem concluida");
                return;
            }

            if (resultado.DocumentoIndisponivel)
            {
                AppDialogService.ShowError(CommandName, "Documento indisponivel.", "Erro");
                return;
            }

            if (resultado.PlanoInvalido)
            {
                AppDialogService.ShowError(CommandName, "Nao foi possivel obter um plano valido.", "Erro");
                return;
            }

            if (resultado.ConfiguracaoInvalida)
            {
                ShowWarning("Selecione um perfil estrutural valido.", "Configuracao invalida");
                return;
            }

            if (resultado.EixosInvalidos)
            {
                AppDialogService.ShowError(CommandName, "O plano ativo nao possui eixos validos.", "Erro");
                return;
            }

            if (resultado.PontosInvalidos)
            {
                ShowWarning(
                    "Os pontos C1 e C2 precisam formar um painel com largura e altura diferentes de zero no plano ativo.",
                    "Pontos invalidos");
                return;
            }

            if (resultado.NivelNaoEncontrado)
            {
                AppDialogService.ShowError(CommandName, "Nao foi possivel determinar um nivel de referencia.", "Erro");
                return;
            }

            if (resultado.NenhumaBarraCriada)
            {
                ShowWarning("Nenhuma barra foi criada.", "Sem resultado");
                return;
            }

            // PreviewCancelado e PickPointCancelado: silencioso (usuario cancelou).
        }

        private Plane ObterPlanoDoPlanoDeTrabalhoAtual(Document doc)
        {
            View vistaAtiva = doc?.ActiveView;
            SketchPlane sketchPlane = vistaAtiva?.SketchPlane;
            if (sketchPlane == null)
            {
                ShowWarning(
                    "A vista ativa nao possui um plano de trabalho definido.\n\nDefina o plano de trabalho no Revit e execute o comando novamente.",
                    "Plano de trabalho ausente");
                return null;
            }

            Plane plane = sketchPlane.GetPlane();
            if (plane == null)
            {
                AppDialogService.ShowError(CommandName, "Nao foi possivel obter a geometria do plano de trabalho atual.", "Falha ao ler plano");
                return null;
            }

            return plane;
        }
    }
}
