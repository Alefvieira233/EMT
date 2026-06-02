#nullable enable
namespace SteelBIM.Models
{
    /// <summary>
    /// Escopo de busca de elementos para o comando Numerar Itens.
    ///
    /// <para>v2.8.0 F11 (Wave 3): movido do arquivo NumeracaoItensConfig.cs
    /// pra arquivo proprio, removendo dependencia do Autodesk.Revit.DB do
    /// enum em si. Permite testar o NumeracaoEscopoParser via xUnit sem
    /// linkar tudo do Revit. O Config continua no NumeracaoItensConfig.cs
    /// porque tem props com tipos Revit (StorageType, ElementId).</para>
    /// </summary>
    public enum NumeracaoEscopo
    {
        ModeloInteiro = 0,
        VistaAtiva = 1,
        SelecaoAtual = 2
    }
}
