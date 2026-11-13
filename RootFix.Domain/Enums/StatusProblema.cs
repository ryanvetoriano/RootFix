namespace RootFix.Domain.Enums;

/// <summary>
/// Status de um problema depois de adotado pelo gestor.
/// </summary>
/// <remarks>
/// Os nomes ficam em maiúsculas de propósito: são exatamente os valores gravados na coluna
/// <c>STATUS</c> do Oracle e os que a API Java e o app usam. Com isso o mesmo texto circula
/// sem conversão pelo banco, pelo JSON e pela query string (<c>?status=EM_ANDAMENTO</c>).
/// </remarks>
public enum StatusProblema
{
    PENDENTE = 1,
    EM_ANDAMENTO = 2,
    RESOLVIDO = 3,
    ARQUIVADO = 4
}
