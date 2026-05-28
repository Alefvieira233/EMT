#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    /// <summary>
    /// Configuracao do comando "Inserir Conexao de Terca" (v2.8.1, Victor;
    /// expandida em v2.8.2 com pick de vigas de apoio + Modo Completo).
    /// Construida pela <c>ConexaoTercasWindow</c>, consumida pelo
    /// <c>ConexaoTercasService</c>.
    /// </summary>
    public class ConexaoTercasConfig
    {
        public FamilySymbol? SymbolSelecionado { get; set; }
        public bool ColocarExtremidades { get; set; } = true;
        public bool ColocarMeio { get; set; } = false;
        public double OffsetRotacaoGraus { get; set; } = 0.0;
        public double OffsetVerticalAdicionalMm { get; set; } = 0.0;

        // Valores de parametros de tipo (em unidades internas Revit: pes, radianos).
        // Aplicados ao FamilySymbol antes do lancamento das instancias.
        public Dictionary<string, double> ParametrosInternos { get; set; } = new Dictionary<string, double>();

        // ---------- v2.8.2 ----------

        /// <summary>
        /// Referencias dos Element das vigas de apoio (pickadas pelo command
        /// no segundo PickObjects). O service usa pra:
        /// 1. Filtrar extremidades de terça que estao perto de alguma viga
        ///    (IsCloseToReference, raio 2000mm).
        /// 2. Projetar o endpoint escolhido na curva da viga mais proxima
        ///    pra obter o ponto de insercao da conexao.
        /// 3. Modo Completo: raycast vertical pra GetBottomFace + ajuste altura.
        /// </summary>
        public IList<Reference>? VigasRefs { get; set; }

        /// <summary>
        /// Quando true, calcula a altura da chapa de conexao ate o topo da
        /// viga de apoio (raycast vertical + GetBottomFace) e aplica em
        /// parametro <c>Altura_PlacaInf_a_Terca</c> (com fallback
        /// <c>Altura_PlacaInf_a_Correa</c>) na familia. Se a familia nao tiver
        /// o parametro, o modo eh silenciosamente ignorado (no-op).
        /// </summary>
        public bool ModoCompleto { get; set; } = false;

        /// <summary>
        /// Quando true (somente significativo se ModoCompleto=true): considera
        /// que as vigas de apoio sao perfis tipo I (W, H, IPN, IPE). O
        /// algoritmo de altura subtrai a espessura da mesa (<c>tf</c>) do
        /// raio calculado pra colocar a chapa exatamente sobre a alma da
        /// viga I, nao sobre o topo da mesa.
        /// </summary>
        public bool VigaTipoI { get; set; } = false;
    }
}
