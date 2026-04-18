namespace FerramentaEMT.Models
{
    public enum EscopoMarcacao
    {
        ModeloInteiro = 0,
        VistaAtiva = 1,
        SelecaoManual = 2
    }

    public enum DestinoMarca
    {
        /// <summary>Grava no parametro nativo ALL_MODEL_MARK.</summary>
        ParametroMark = 0,

        /// <summary>Grava no parametro Comments.</summary>
        ParametroComments = 1,

        /// <summary>Grava em um parametro compartilhado/projeto informado pelo usuario.</summary>
        ParametroCustomizado = 2
    }

    public sealed class MarcarPecasConfig
    {
        // --- Escopo ---
        public EscopoMarcacao Escopo { get; set; } = EscopoMarcacao.VistaAtiva;

        // --- Categorias a marcar ---
        public bool MarcarVigas { get; set; } = true;
        public bool MarcarPilares { get; set; } = true;
        public bool MarcarContraventamentos { get; set; } = true;

        // --- Prefixos por categoria ---
        public string PrefixoVigas { get; set; } = "V";
        public string PrefixoPilares { get; set; } = "P";
        public string PrefixoContraventamentos { get; set; } = "C";

        // --- Numeracao ---
        public int NumeroInicial { get; set; } = 1;
        public int Digitos { get; set; } = 3;

        // --- Destino da marca ---
        public DestinoMarca Destino { get; set; } = DestinoMarca.ParametroMark;
        public string NomeParametroCustomizado { get; set; } = string.Empty;

        // --- Opcoes adicionais ---
        /// <summary>Sobrescrever marcas existentes (se false, pula elementos ja marcados).</summary>
        public bool SobrescreverExistentes { get; set; } = false;

        /// <summary>Aplicar destaque visual (cores por grupo) na vista ativa apos a marcacao.</summary>
        public bool DestaqueVisual { get; set; } = true;

        // --- Tolerancia de comprimento para agrupamento (mm) ---
        public double ToleranciaComprimentoMm { get; set; } = 1.0;

        public bool TemCategoriaSelecionada()
        {
            return MarcarVigas || MarcarPilares || MarcarContraventamentos;
        }

        public string ObterPrefixo(string categoriaLogica)
        {
            return categoriaLogica switch
            {
                "Viga" => PrefixoVigas,
                "Pilar" => PrefixoPilares,
                "Contraventamento" => PrefixoContraventamentos,
                _ => "X"
            };
        }

        public string FormatarNumero(int numero)
        {
            return numero.ToString($"D{Digitos}");
        }
    }
}
