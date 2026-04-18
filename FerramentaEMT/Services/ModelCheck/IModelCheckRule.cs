using System.Collections.Generic;
using Autodesk.Revit.DB;
using FerramentaEMT.Models.ModelCheck;

namespace FerramentaEMT.Services.ModelCheck
{
    /// <summary>
    /// Interface para uma regra de verificacao de modelo.
    /// Cada implementacao verifica um aspecto especifico do modelo.
    /// </summary>
    public interface IModelCheckRule
    {
        /// <summary>Nome unico da regra.</summary>
        string Name { get; }

        /// <summary>Descricao do que a regra verifica.</summary>
        string Description { get; }

        /// <summary>
        /// Executa a regra contra o documento fornecido.
        /// </summary>
        /// <param name="doc">Documento Revit a verificar.</param>
        /// <param name="scopeIds">
        /// Lista de IDs de elementos a verificar. Se nula/vazia, usa a logica padrao da regra
        /// para determinar o escopo (p.ex., todos os elementos estruturais).
        /// </param>
        /// <returns>Colecao de problemas encontrados.</returns>
        IEnumerable<ModelCheckIssue> Check(Document doc, IList<ElementId> scopeIds);
    }
}
