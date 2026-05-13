using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FerramentaEMT.Core
{
    /// <summary>
    /// Abstrai o acesso ao Revit para os servicos do plugin.
    ///
    /// Hoje e um wrapper minimo sobre UIDocument/Document. A intencao e que servicos
    /// nao dependam diretamente de <see cref="ExternalCommandData"/> e que novos
    /// servicos recebam <see cref="IRevitContext"/> no construtor — abrindo espaco
    /// para substituir por mocks/fakes em testes unitarios no futuro.
    ///
    /// Nao e uma abstracao completa do Revit API — operacoes avancadas devem usar
    /// <see cref="Document"/> e <see cref="UIDocument"/> diretamente ate que
    /// abstracoes de alto nivel (IElementQuery, ITransactionScope) sejam introduzidas.
    /// </summary>
    public interface IRevitContext
    {
        /// <summary>
        /// Documento Revit ativo. <b>Nota:</b> acesso "ao vivo" — se o usuario
        /// trocar de documento entre chamadas dentro de um servico, esta property
        /// refletira o novo. Se o servico precisar estabilidade dentro de uma
        /// operacao, cache a referencia no inicio.
        /// </summary>
        Document Document { get; }

        /// <summary>UIDocument ativo. Mesma semantica de "ao vivo" do <see cref="Document"/>.</summary>
        UIDocument UIDocument { get; }

        /// <summary>Aplicacao Revit (para leitura de versao, locale, etc).</summary>
        Autodesk.Revit.ApplicationServices.Application Application { get; }

        /// <summary>
        /// Versao major do Revit como string (ex: "2025") — valor bruto de
        /// <c>Application.VersionNumber</c>. Nao inclui build nem patch.
        /// </summary>
        string RevitVersion { get; }
    }

    /// <summary>
    /// Implementacao padrao sobre <see cref="ExternalCommandData"/>.
    /// Use <see cref="CreateFromCommandData"/> nos comandos.
    /// </summary>
    public sealed class RevitContext : IRevitContext
    {
        private readonly UIDocument _uidoc;

        private RevitContext(UIDocument uidoc)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
        }

        public Document Document => _uidoc.Document;

        public UIDocument UIDocument => _uidoc;

        public Autodesk.Revit.ApplicationServices.Application Application =>
            _uidoc.Application.Application;

        public string RevitVersion => Application.VersionNumber;

        /// <summary>
        /// Cria um <see cref="IRevitContext"/> a partir do ExternalCommandData recebido pelo IExternalCommand.
        /// Lanca <see cref="InvalidOperationException"/> se nao houver documento ativo.
        /// </summary>
        public static IRevitContext CreateFromCommandData(ExternalCommandData commandData)
        {
            if (commandData == null) throw new ArgumentNullException(nameof(commandData));

            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            if (uidoc == null)
                throw new InvalidOperationException("Nenhum documento Revit ativo.");
            if (uidoc.Document == null)
                throw new InvalidOperationException("UIDocument sem Document associado.");

            return new RevitContext(uidoc);
        }
    }
}
