#nullable enable
using System.Globalization;

namespace SteelBIM.Services
{
    /// <summary>
    /// Logica PURA (sem Revit) da formatacao do sufixo de romaneio de um perfil metalico:
    /// "24 un · 360,0 m · 11,2 kg/m". Cultura explicita pt-BR (separador decimal virgula) para
    /// consistencia na planilha. Omite o trecho kg/m quando comprimento ou peso linear sao nulos.
    /// </summary>
    public static class ListaMateriaisRomaneioFormatter
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static string Sufixo(int quantidade, double comprimentoM, double kgPorM)
        {
            string s = quantidade + " un · " + comprimentoM.ToString("0.0", PtBr) + " m";
            if (comprimentoM > 0.0 && kgPorM > 0.0)
                s += " · " + kgPorM.ToString("0.0", PtBr) + " kg/m";
            return s;
        }
    }
}
