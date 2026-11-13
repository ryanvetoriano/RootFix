namespace RootFix.Domain.Exceptions;

/// <summary>
/// Violação de uma regra de negócio (ex.: transição de status não permitida).
/// Traduzida para HTTP 422 pelo tratamento global de exceções.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Recurso solicitado não existe.
/// Traduzida para HTTP 404 pelo tratamento global de exceções.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public static NotFoundException For(string recurso, long id)
        => new($"{recurso} com o ID '{id}' não foi encontrado(a).");
}

/// <summary>
/// O usuário está autenticado, mas não pode agir sobre este recurso (ex.: gestor de outra
/// comunidade). Traduzida para HTTP 403.
/// </summary>
public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A operação conflita com o estado atual do recurso (ex.: o problema já está no status pedido
/// ou foi alterado por outra pessoa ao mesmo tempo). Traduzida para HTTP 409.
/// </summary>
public class ConflitoException : Exception
{
    public ConflitoException(string message)
        : base(message)
    {
    }
}
