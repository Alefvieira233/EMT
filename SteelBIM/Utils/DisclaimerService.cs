#nullable enable
using System.Collections.Generic;

namespace SteelBIM.Utils
{
    /// <summary>
    /// Exibe avisos tecnicos/legais (ver <see cref="DisclaimerTexts"/>) UMA VEZ por sessao
    /// do Revit, por chave. Evita repetir o mesmo aviso a cada execucao do comando, mas
    /// garante que o usuario veja a ressalva de responsabilidade ao menos uma vez por sessao.
    /// </summary>
    public static class DisclaimerService
    {
        private static readonly HashSet<string> Exibidos = new HashSet<string>();
        private static readonly object SyncLock = new object();

        /// <summary>
        /// Mostra o aviso (dialogo informativo) na primeira vez que <paramref name="chave"/>
        /// e' usada nesta sessao; chamadas seguintes com a mesma chave nao fazem nada.
        /// </summary>
        public static void MostrarUmaVezPorSessao(string chave, string titulo, string mensagem)
        {
            lock (SyncLock)
            {
                if (!Exibidos.Add(chave))
                    return;
            }

            AppDialogService.ShowInfo(titulo, mensagem, "Aviso tecnico — verificar conforme projeto estrutural");
        }
    }
}
