using System.Collections.Generic;

namespace FerramentaEMT.Models
{
    /// <summary>
    /// Resultado consolidado de uma rodada de <c>Services.CortarElementosService</c>.
    /// Emitido como payload de <c>Result&lt;CortarElementosResultado&gt;</c> pelo servico
    /// (ADR-003: servico e mudo, caller monta UX).
    /// </summary>
    /// <remarks>
    /// <para>Ate 2026-04 esta classe era <c>internal sealed</c> dentro do proprio servico,
    /// na versao do Victor. Extrair para Models permite:</para>
    /// <list type="bullet">
    /// <item>teste unitario fora do assembly (o projeto de testes nao referencia RevitAPI)</item>
    /// <item>comando desacoplado da estrutura interna do servico</item>
    /// </list>
    /// <para>Por isso <see cref="ElementosRelacionados"/> e <c>IReadOnlyList&lt;long&gt;</c>
    /// (mesmo padrao usado em <c>ModelCheckIssue.ElementId</c>) — o chamador materializa
    /// <c>Autodesk.Revit.DB.ElementId</c> via <c>new ElementId(valor)</c> quando precisa.</para>
    /// </remarks>
    public sealed class CortarElementosResultado
    {
        public CortarElementosResultado(
            int totalSelecionados,
            int hostsAnalisados,
            int cuttersAnalisados,
            int paresIntersectando,
            int alteracoesAplicadas,
            int jaConformes,
            int falhas,
            IReadOnlyList<long> elementosRelacionados,
            IReadOnlyList<string> diagnostico)
        {
            TotalSelecionados = totalSelecionados;
            HostsAnalisados = hostsAnalisados;
            CuttersAnalisados = cuttersAnalisados;
            ParesIntersectando = paresIntersectando;
            AlteracoesAplicadas = alteracoesAplicadas;
            JaConformes = jaConformes;
            Falhas = falhas;
            ElementosRelacionados = elementosRelacionados ?? new List<long>();
            Diagnostico = diagnostico ?? new List<string>();
        }

        /// <summary>Elementos validos fornecidos (pre-filtrados por categoria) ao servico.</summary>
        public int TotalSelecionados { get; }

        /// <summary>Numero de hosts distintos (piso/quadro) analisados no escopo.</summary>
        public int HostsAnalisados { get; }

        /// <summary>Numero de cortadores distintos (colunas/pilares) analisados no escopo.</summary>
        public int CuttersAnalisados { get; }

        /// <summary>Pares host-cortador com interferencia de fato detectada.</summary>
        public int ParesIntersectando { get; }

        /// <summary>Pares onde foi aplicada uma alteracao nesta rodada (novos cortes / inversoes).</summary>
        public int AlteracoesAplicadas { get; }

        /// <summary>Pares que ja estavam corretamente cortados (nenhuma mudanca necessaria).</summary>
        public int JaConformes { get; }

        /// <summary>Pares onde a tentativa de corte falhou (API Revit rejeitou, geometria invalida, etc).</summary>
        public int Falhas { get; }

        /// <summary>True se alguma alteracao foi aplicada (caller deve commitar a transacao).</summary>
        public bool HouveAlteracao => AlteracoesAplicadas > 0;

        /// <summary>True se alguma alteracao foi aplicada OU havia pares ja conformes (nao-erro).</summary>
        public bool HouveSucesso => HouveAlteracao || JaConformes > 0;

        /// <summary>
        /// Valores de ID (long) de todos os elementos envolvidos em pares detectados
        /// (uniao host+cortador). O comando usa isso para re-selecionar o conjunto
        /// apos a transacao, materializando <c>new ElementId(valor)</c>.
        /// </summary>
        public IReadOnlyList<long> ElementosRelacionados { get; }

        /// <summary>
        /// Linhas de diagnostico descrevendo o que aconteceu em cada par. Formato livre,
        /// destinado a exibicao textual no dialogo final.
        /// </summary>
        public IReadOnlyList<string> Diagnostico { get; }
    }
}
