using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteelBIM.Services
{
    /// <summary>
    /// Helper puro (sem dependencia Revit) que monta partes deterministicas
    /// da assinatura de fabricacao de uma peca. Extraido em v2.6.1 (hotfix P0
    /// DETERMINISMO) apos auditoria senior 2026-05-19 PHASE 5.6 identificar
    /// que MarcarPecasService.MontarAssinaturaFabricacao gerava signatures
    /// nao-deterministicas (Element.Parameters iteration tem ordem instavel
    /// entre versoes/sessoes/projetos + uso de ElementId.Value que varia
    /// entre documentos).
    ///
    /// As 3 funcoes:
    ///   BuildTypeKey(familyName, typeName) — chave estavel cross-document
    ///   BuildMaterialKey(materialName) — idem para material
    ///   BuildParameterSection(prefixo, params) — ordena alfabeticamente
    ///
    /// CRITICO: nenhuma das funcoes usa ElementId. Toda chave e string
    /// derivada de nomes — garantindo que peças idênticas em projetos
    /// diferentes gerem MESMA marca (idempotencia cross-project).
    /// </summary>
    public static class MarcarPecasSignatureBuilder
    {
        /// <summary>
        /// Constroi chave estavel para identificar um ElementType. Usa
        /// FamilyName + "|" + Name (ambos string). Nulos / whitespace viram "?".
        ///
        /// NUNCA use ElementId.Value como chave de fabricacao — ele e
        /// per-document e duas pecas idênticas em projetos diferentes
        /// recebem IDs diferentes, quebrando dedup inter-projeto.
        /// </summary>
        public static string BuildTypeKey(string familyName, string typeName)
        {
            string fn = string.IsNullOrWhiteSpace(familyName) ? "?" : familyName.Trim();
            string tn = string.IsNullOrWhiteSpace(typeName) ? "?" : typeName.Trim();
            return fn + "|" + tn;
        }

        /// <summary>
        /// Constroi chave estavel para identificar um Material por nome.
        /// </summary>
        public static string BuildMaterialKey(string materialName)
        {
            return string.IsNullOrWhiteSpace(materialName) ? "<sem>" : materialName.Trim();
        }

        /// <summary>
        /// Constroi a substring "prefixo:nome1=val1|prefixo:nome2=val2|..."
        /// ordenando os parametros ALFABETICAMENTE por nome
        /// (StringComparer.Ordinal). Ignora entradas com nome ou valor
        /// vazio/whitespace.
        ///
        /// CRITICO: Element.Parameters no Revit API NAO garante ordem
        /// estavel entre versoes / sessoes / projetos. Sem este OrderBy,
        /// executar MarcarPecas N+1 vezes podia gerar signatures
        /// diferentes para o mesmo modelo — quebrando deduplicacao
        /// de marca.
        /// </summary>
        public static string BuildParameterSection(string prefixo, IEnumerable<(string Name, string Value)> parametros)
        {
            if (parametros == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (var (name, value) in parametros
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Value))
                .OrderBy(p => p.Name, System.StringComparer.Ordinal))
            {
                sb.Append(prefixo).Append(':')
                  .Append(name).Append('=')
                  .Append(value).Append('|');
            }
            return sb.ToString();
        }
    }
}
