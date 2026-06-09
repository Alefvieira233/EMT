#nullable enable
using System;
using Autodesk.Revit.DB;
using SteelBIM.Infrastructure;

namespace SteelBIM.Services
{
    /// <summary>
    /// Resolve o nome do perfil de uma <see cref="FamilyInstance"/>: parametro shared "EMT_Perfil" →
    /// nome do tipo (Symbol.Name) → nome da familia. Extraido de TagearTrelicaService e
    /// IdentificarPerfilService (eram identicos) para uma fonte unica.
    /// </summary>
    public static class PerfilNomeResolver
    {
        public static string ObterNomePerfil(FamilyInstance fi, string titulo)
        {
            try
            {
                // 1) parametro shared EMT_Perfil (se existir no projeto)
                Parameter? paramShared = fi.LookupParameter("EMT_Perfil");
                if (paramShared != null && paramShared.StorageType == StorageType.String)
                {
                    string val = paramShared.AsString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }

                // 2) nome do tipo (FamilySymbol.Name)
                string typeName = fi.Symbol?.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(typeName))
                    return typeName;

                // 3) nome da familia
                string familyName = fi.Symbol?.Family?.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(familyName))
                    return familyName;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[{Title}] erro obtendo nome do perfil para {ElemId}", titulo, fi.Id);
            }

            return "-";
        }
    }
}
