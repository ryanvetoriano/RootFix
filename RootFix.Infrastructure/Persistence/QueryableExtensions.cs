using System.Linq.Expressions;

namespace RootFix.Infrastructure.Persistence;

/// <summary>
/// Ordenação traduzida para SQL pelo EF Core. No Oracle, o <c>Skip</c>/<c>Take</c> seguinte
/// vira <c>OFFSET ... FETCH NEXT ...</c>: só a página pedida sai do banco.
/// </summary>
internal static class QueryableExtensions
{
    public static IOrderedQueryable<T> OrdenarPor<T, TChave>(
        this IQueryable<T> query,
        Expression<Func<T, TChave>> chave,
        bool descendente)
        => descendente
            ? query.OrderByDescending(chave)
            : query.OrderBy(chave);
}
