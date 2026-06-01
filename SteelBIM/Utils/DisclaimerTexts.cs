#nullable enable

namespace SteelBIM.Utils
{
    /// <summary>
    /// Textos de aviso tecnico/legal exibidos antes de funcoes que GERAM detalhamento
    /// automatico (armadura PF, conexoes metalicas). Centralizados aqui como fonte unica
    /// e versionada do texto de responsabilidade — garante consistencia entre comandos e
    /// permite teste de regressao do conteudo.
    /// </summary>
    public static class DisclaimerTexts
    {
        /// <summary>Aviso para geracao automatica de armaduras (modulo PF).</summary>
        public const string Armadura =
            "Os detalhamentos de armadura são gerados automaticamente como apoio à produção e " +
            "NÃO substituem o projeto estrutural. Antes de executar, verifique bitolas, " +
            "espaçamentos, zonas de adensamento, armadura de suspensão e comprimentos de " +
            "ancoragem/traspasse conforme o projeto estrutural e a NBR 6118.";

        /// <summary>Aviso para geracao de conexoes metalicas.</summary>
        public const string Conexoes =
            "As conexões são geradas de forma GEOMÉTRICA/DOCUMENTAL, sem verificação de " +
            "capacidade (esforços, soldas, parafusos, chapas). Antes de executar, verifique o " +
            "dimensionamento e a resistência da ligação conforme o projeto estrutural.";
    }
}
