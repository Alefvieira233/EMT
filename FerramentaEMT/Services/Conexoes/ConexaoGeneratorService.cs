#nullable enable
using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using FerramentaEMT.Infrastructure;
using FerramentaEMT.Models.Conexoes;

namespace FerramentaEMT.Services.Conexoes
{
    /// <summary>
    /// Serviço de geração de conexões estruturais (Chapa de Ponta, Dupla Cantoneira, Chapa Gusset).
    /// Tenta colocar as famílias; se não encontradas, marca visualmente e registra para integração manual.
    /// </summary>
    public class ConexaoGeneratorService
    {
        private const string Titulo = "Gerador de Conexões";

        /// <summary>
        /// Nome da família que faltou carregar (quando retorno foi false por familia ausente).
        /// Permite ao caller dar mensagem acionável ao usuario.
        /// </summary>
        public string? FamiliaNaoCarregada { get; private set; }

        /// <summary>
        /// Tenta colocar uma conexão entre uma viga e outro elemento (pilar ou viga).
        /// Se a família de conexão não estiver carregada, marca o ponto de conexão visualmente
        /// e registra um aviso no parâmetro Comments do elemento.
        /// </summary>
        /// <param name="doc">Documento Revit.</param>
        /// <param name="vigaPrincipal">FamilyInstance da viga.</param>
        /// <param name="conectadoA">Elemento ao qual a viga se conecta (pilar ou outra viga).</param>
        /// <param name="config">Configuração da conexão.</param>
        /// <returns>True se a conexão foi colocada com sucesso; false se apenas marcada.</returns>
        public bool TentarColocarConexao(
            Document doc,
            FamilyInstance vigaPrincipal,
            Element conectadoA,
            ConexaoConfig config)
        {
            Logger.Info("[ConexaoGenerator] Tentando colocar conexão tipo {Tipo} entre viga {VigaId} e {ConectId}",
                config.Tipo,
                vigaPrincipal.Id.Value,
                conectadoA.Id.Value);

            try
            {
                // Obter pontos de referência ou geometria
                XYZ? pontoviga = ObterPontoConexa(vigaPrincipal);
                XYZ? pontoConectado = ObterPontoConexa(conectadoA);

                if (pontoviga == null || pontoConectado == null)
                {
                    Logger.Warn("[ConexaoGenerator] Nao foi possivel obter pontos de conexao");
                    MarcarConexaoPendente(doc, vigaPrincipal, config);
                    return false;
                }

                // Procurar família de conexão carregada
                Family? familyConexao = ProcurarFamiliaConexao(doc, config.Tipo);
                if (familyConexao == null)
                {
                    string nomeFam = NomeFamiliaEsperado(config.Tipo);
                    Logger.Warn("[ConexaoGenerator] Familia {Nome} nao carregada no modelo", nomeFam);
                    MarcarConexaoPendente(doc, vigaPrincipal, config);
                    FamiliaNaoCarregada = nomeFam;
                    return false;
                }

                // Colocar a instance da família
                using (Transaction tx = new Transaction(doc, $"Colocar conexão {config.Tipo}"))
                {
                    tx.Start();

                    // Obter o primeiro tipo de símbolo disponível
                    FamilySymbol? symbol = ProcurarFamilySymbol(familyConexao);
                    if (symbol == null)
                    {
                        tx.RollBack();
                        Logger.Warn("[ConexaoGenerator] Nenhum Symbol de {Tipo} disponivel", config.Tipo);
                        MarcarConexaoPendente(doc, vigaPrincipal, config);
                        return false;
                    }

                    if (!symbol.IsActive)
                        symbol.Activate();

                    // Colocar a família no ponto de conexão.
                    // CRITICO: nao usar doc.ActiveView.SketchPlane — a maioria das vistas nao tem SketchPlane
                    // e qualquer acesso a .Normal lanca NullReferenceException (bug antigo: virava "pendente" silencioso).
                    // Usamos o overload 3-arg (ponto, simbolo, StructuralType) que coloca no nivel da viga.
                    FamilyInstance conexaoInstance = doc.Create.NewFamilyInstance(
                        pontoviga,
                        symbol,
                        StructuralType.NonStructural);

                    // Registrar IDs relacionados como referência
                    Parameter? paramViga = conexaoInstance.LookupParameter("EMT_Viga_Conectada");
                    if (paramViga != null && paramViga.StorageType == StorageType.ElementId)
                        paramViga.Set(vigaPrincipal.Id);

                    Parameter? paramConectado = conexaoInstance.LookupParameter("EMT_Conectado_A");
                    if (paramConectado != null && paramConectado.StorageType == StorageType.ElementId)
                        paramConectado.Set(conectadoA.Id);

                    // Registrar tipo de conexão
                    Parameter? paramTipo = conexaoInstance.LookupParameter("EMT_Tipo_Conexao");
                    if (paramTipo != null && paramTipo.StorageType == StorageType.String)
                        paramTipo.Set(config.Tipo.ToString());

                    tx.Commit();
                }

                Logger.Info("[ConexaoGenerator] Conexão colocada com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ConexaoGenerator] Erro ao tentar colocar conexão");
                MarcarConexaoPendente(doc, vigaPrincipal, config);
                return false;
            }
        }

        /// <summary>
        /// Calcula o número total de parafusos necessários para um conjunto de conexões.
        /// (Delegado puro para ConexaoCalculator.)
        /// </summary>
        public int ContarParafusosTotal(ConexaoConfig config, int numConexoes)
        {
            return ConexaoCalculator.ContarParafusosTotal(config, numConexoes);
        }

        /// <summary>
        /// Gera um marcador (identificador) único para a conexão baseado em sua configuração.
        /// (Delegado puro para ConexaoCalculator.)
        /// </summary>
        public string GerarMarcadorConexao(ConexaoConfig config)
        {
            return ConexaoCalculator.GerarMarcadorConexao(config);
        }

        // =====================================================================
        // Métodos auxiliares privados
        // =====================================================================

        /// <summary>Marca a conexão como pendente de integração (sem familia carregada).</summary>
        private void MarcarConexaoPendente(Document doc, FamilyInstance viga, ConexaoConfig config)
        {
            try
            {
                using (Transaction tx = new Transaction(doc, "Marcar conexão pendente"))
                {
                    tx.Start();

                    Parameter? comments = viga.LookupParameter("Comments");
                    if (comments != null)
                    {
                        string marcador = GerarMarcadorConexao(config);
                        string msg = $"CONEXAO_PENDENTE:{config.Tipo}|{marcador}";
                        comments.Set(msg);
                    }

                    tx.Commit();
                }

                Logger.Info("[ConexaoGenerator] Conexão marcada como pendente: {Tipo}", config.Tipo);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[ConexaoGenerator] Erro ao marcar conexão pendente");
            }
        }

        /// <summary>Obtém o ponto de conexão (centróide da geometria) de um elemento.</summary>
        private XYZ? ObterPontoConexa(Element elem)
        {
            try
            {
                if (elem is FamilyInstance fi)
                {
                    return fi.Location is LocationPoint lp ? lp.Point : null;
                }
                else
                {
                    GeometryElement? geom = elem.get_Geometry(new Options { IncludeNonVisibleObjects = false });
                    if (geom != null)
                    {
                        var bbox = geom.GetBoundingBox();
                        if (bbox != null)
                        {
                            return (bbox.Min + bbox.Max) * 0.5;
                        }
                    }
                }
            }
            catch
            {
                // ignorar erros de geometria
            }

            return null;
        }

        /// <summary>Retorna o nome de família esperado para o tipo de conexão.</summary>
        internal static string NomeFamiliaEsperado(TipoConexao tipo) => ConexaoFamilyNames.For(tipo);

        /// <summary>Procura uma família de conexão carregada no modelo pelo tipo.</summary>
        private Family? ProcurarFamiliaConexao(Document doc, TipoConexao tipo)
        {
            string nomeFamilia = NomeFamiliaEsperado(tipo);

            if (string.IsNullOrEmpty(nomeFamilia))
                return null;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            foreach (Family family in collector.OfClass(typeof(Family)))
            {
                if (family.Name.Equals(nomeFamilia, StringComparison.OrdinalIgnoreCase))
                    return family;
            }

            return null;
        }

        /// <summary>Obtém o primeiro FamilySymbol de uma Family.</summary>
        private FamilySymbol? ProcurarFamilySymbol(Family family)
        {
            foreach (ElementId symId in family.GetFamilySymbolIds())
            {
                if (family.Document.GetElement(symId) is FamilySymbol fs)
                    return fs;
            }

            return null;
        }
    }
}
