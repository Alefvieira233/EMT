using System;

namespace FerramentaEMT.Core
{
    /// <summary>
    /// Result&lt;T&gt; — representa o desfecho de uma operacao sem recorrer a excecoes para
    /// fluxos previsiveis (input invalido, selecao vazia, regra de negocio violada).
    ///
    /// Uso recomendado:
    ///   - excecoes: bugs, falhas de infraestrutura (API Revit retornando o inesperado, IO)
    ///   - Result&lt;T&gt;: falhas esperadas do dominio (usuario nao selecionou peca, parametro
    ///     obrigatorio vazio, elemento nao e do tipo certo)
    ///
    /// Exemplo:
    ///   public Result&lt;int&gt; MarcarPeca(Element el)
    ///   {
    ///       if (el == null) return Result&lt;int&gt;.Fail("Elemento nulo");
    ///       // ...
    ///       return Result&lt;int&gt;.Ok(marca);
    ///   }
    ///
    /// Consumo em comando:
    ///   Result&lt;int&gt; r = _service.MarcarPeca(el);
    ///   if (!r.IsSuccess)
    ///   {
    ///       AppDialogService.ShowWarning("Marcar Peca", r.Error, "Nao foi possivel marcar");
    ///       return Autodesk.Revit.UI.Result.Cancelled;
    ///   }
    ///   int marca = r.Value;
    /// </summary>
    public readonly struct Result<T>
    {
        public T Value { get; }
        public string Error { get; }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        private Result(T value, string error, bool isSuccess)
        {
            Value = value;
            Error = error;
            IsSuccess = isSuccess;
        }

        /// <summary>Resultado de sucesso com valor.</summary>
        public static Result<T> Ok(T value) => new Result<T>(value, null, true);

        /// <summary>Resultado de falha com mensagem amigavel ao usuario.</summary>
        public static Result<T> Fail(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error deve ser uma mensagem nao vazia.", nameof(error));
            return new Result<T>(default, error, false);
        }

        /// <summary>
        /// Executa <paramref name="onSuccess"/> se sucesso, <paramref name="onFailure"/> se falha,
        /// e devolve um novo resultado. Util para encadear transformacoes.
        /// </summary>
        public Result<TOut> Match<TOut>(Func<T, Result<TOut>> onSuccess, Func<string, Result<TOut>> onFailure)
        {
            if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
            if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));
            return IsSuccess ? onSuccess(Value) : onFailure(Error);
        }

        /// <summary>
        /// Converte o valor quando sucesso, repassa a falha quando falha.
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            return IsSuccess ? Result<TOut>.Ok(transform(Value)) : Result<TOut>.Fail(Error);
        }

        public override string ToString() => IsSuccess ? $"Ok({Value})" : $"Fail({Error})";
    }

    /// <summary>
    /// Variante sem valor, util para operacoes que so importa sucesso/falha.
    /// Exemplo: aplicar uma edicao no modelo que nao retorna nada.
    /// </summary>
    public readonly struct Result
    {
        public string Error { get; }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        private Result(string error, bool isSuccess)
        {
            Error = error;
            IsSuccess = isSuccess;
        }

        public static Result Ok() => new Result(null, true);

        public static Result Fail(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error deve ser uma mensagem nao vazia.", nameof(error));
            return new Result(error, false);
        }

        public override string ToString() => IsSuccess ? "Ok" : $"Fail({Error})";
    }
}
