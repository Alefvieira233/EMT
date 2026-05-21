using System;

namespace SteelBIM.Services.Ifc
{
    /// <summary>
    /// Calculadora pura (sem Autodesk.Revit.DB) que decide se um elemento e
    /// "linear" baseado no seu bounding box — usado pelo wrapper Revit
    /// <c>ConverterPerfilIfcService.EhPerfilEstruturalLinear</c> para filtrar
    /// acessorios IFC nao-conversiveis (armaduras, chapas, ganchos, BoltArrays)
    /// que vieram do CYPE mas nao sao perfis estruturais lineares.
    ///
    /// Default threshold 3.0 valida em galpao real do Alef (6983 elementos
    /// IFC, filtragem reduz pra ~vigas+pilares).
    /// </summary>
    public static class IfcStructuralFilterPure
    {
        /// <summary>
        /// Razao minima (dim maior / dim menor) para considerar uma peca como
        /// linear. Default 3.0 — pecas com 1 dimensao 3x ou mais maior que a
        /// menor sao linhas (vigas, pilares, terças). Quadradas/cubicas (placas,
        /// blocos, chapas) ficam abaixo de 3.
        /// </summary>
        public const double RazaoMinimaLinear = 3.0;

        /// <summary>
        /// Tolerancia em pes para considerar uma dimensao "zero" (degenerada).
        /// Pecas com qualquer dimensao &lt; 1mm sao rejeitadas como ruido.
        /// </summary>
        public const double EpsilonFt = 1e-3;

        /// <summary>
        /// Retorna true se a relacao (maior_dim / menor_dim) >= 3.0 — peca
        /// linear como viga, pilar, terça. Retorna false para:
        /// <list type="bullet">
        /// <item>Pecas quase quadradas/cubicas (chapas, blocos)</item>
        /// <item>Pecas com qualquer dimensao degenerada (&lt; <see cref="EpsilonFt"/>)</item>
        /// </list>
        /// Aceita dimensoes negativas (toma valor absoluto).
        /// </summary>
        public static bool EhLinearPorBbox(double dxFt, double dyFt, double dzFt)
        {
            double dx = Math.Abs(dxFt);
            double dy = Math.Abs(dyFt);
            double dz = Math.Abs(dzFt);

            double menor = Math.Min(dx, Math.Min(dy, dz));
            if (menor < EpsilonFt)
                return false;

            double maior = Math.Max(dx, Math.Max(dy, dz));
            return maior / menor >= RazaoMinimaLinear;
        }
    }
}
