namespace RootFix.Application.Common;

/// <summary>
/// Página devolvida pelos repositórios: os itens da página pedida e o total de registros que
/// atendem aos filtros, necessário para calcular o número de páginas.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Itens, int TotalItens);
