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
    ///
    /// <para>
    /// <b>Semantica do default:</b> <c>default(Result&lt;T&gt;)</c> e tratado como
    /// <c>Ok(default(T))</c> — o flag interno e <c>_isFailure</c>, que nasce false.
    /// Isso evita a armadilha classica de struct onde um campo nao inicializado
    /// aparece como "falha sem mensagem" (surfaced como NRE ao logar Error).
    /// Continue preferindo os factories <c>Ok</c>/<c>Fail</c> — o tratamento de
    /// default existe so para defesa em profundidade.
    /// </para>
    /// </summary>
    public readonly struct Result<T>
    {
        // Invariante do default-safe: _isFailure nasce false -> IsSuccess nasce true.
        // Se voce inverter isso, releia a rationale acima antes.
        private readonly bool _isFailure;
        private readonly T _value;
        private readonly string _error;

        public T Value => _value;
        public string Error => _error;
        public bool IsSuccess => !_isFailure;
        public bool IsFailure => _isFailure;

        private Result(T value, string error, bool isFailure)
        {
            _value = value;
            _error = error;
            _isFailure = isFailure;
        }

        /// <summary>Resultado de sucesso com valor. <c>value</c> pode ser null (permitido).</summary>
        public static Result<T> Ok(T value) => new Result<T>(value, null, isFailure: false);

        /// <summary>Resultado de falha com mensagem amigavel ao usuario.</summary>
        public static Result<T> Fail(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error deve ser uma mensagem nao vazia.", nameof(error));
            return new Result<T>(default, error, isFailure: true);
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
    ///
    /// <para>
    /// <b>Semantica do default:</b> mesma de <see cref="Result{T}"/> — <c>default(Result)</c>
    /// e tratado como <c>Ok()</c>.
    /// </para>
    /// </summary>
    public readonly struct Result
    {
        private readonly bool _isFailure;
        private readonly string _error;

        public string Error => _error;
        public bool IsSuccess => !_isFailure;
        public bool IsFailure => _isFailure;

        private Result(string error, bool isFailure)
        {
            _error = error;
            _isFailure = isFailure;
        }

        public static Result Ok() => new Result(null, isFailure: false);

        public static Result Fail(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error deve ser uma mensagem nao vazia.", nameof(error));
            return new Result(error, isFailure: true);
        }

        public override string ToString() => IsSuccess ? "Ok" : $"Fail({Error})";
    }
}
