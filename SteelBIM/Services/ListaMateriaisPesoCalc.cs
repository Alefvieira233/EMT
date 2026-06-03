#nullable enable

namespace SteelBIM.Services
{
    /// <summary>
    /// v2.8.12 (Onda 1): logica PURA do peso da lista de materiais — peso por densidade e
    /// inferencia da base do material (concreto/metalico) por TEXTO. Extraida de
    /// <c>ListaMateriaisExportService</c> (Revit-bound) para ser testavel sem Revit.
    /// </summary>
    public static class ListaMateriaisPesoCalc
    {
        public enum BaseMaterial
        {
            Outro = 0,
            Concreto = 1,
            Metalico = 2
        }

        /// <summary>Peso (kg) = densidade (kg/m³) x volume (m³). Zero se algum nao-positivo.</summary>
        public static double PesoKg(double volumeM3, double densidadeKgM3)
        {
            if (volumeM3 <= 0.0 || densidadeKgM3 <= 0.0)
                return 0.0;
            return densidadeKgM3 * volumeM3;
        }

        /// <summary>
        /// Infere a base do material por texto: nome do MATERIAL e/ou da FAMILIA/TIPO contendo
        /// "concreto"/"concrete" -> Concreto; "aco"/"aço"/"steel" -> Metalico; senao, se for
        /// fundacao -> Concreto; senao Outro. Concreto tem prioridade sobre aco.
        /// </summary>
        public static BaseMaterial InferirBase(string? nomeMaterial, string? nomeFamiliaTipo, bool isFundacao)
        {
            if (ContemConcreto(nomeMaterial) || ContemConcreto(nomeFamiliaTipo))
                return BaseMaterial.Concreto;
            if (ContemAco(nomeMaterial) || ContemAco(nomeFamiliaTipo))
                return BaseMaterial.Metalico;
            if (isFundacao)
                return BaseMaterial.Concreto;
            return BaseMaterial.Outro;
        }

        private static bool ContemConcreto(string? s)
        {
            string t = (s ?? string.Empty).ToLowerInvariant();
            return t.Contains("concreto") || t.Contains("concrete");
        }

        private static bool ContemAco(string? s)
        {
            string t = (s ?? string.Empty).ToLowerInvariant();
            return t.Contains("aco") || t.Contains("aço") || t.Contains("steel");
        }
    }
}
