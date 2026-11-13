using RootFix.Domain.Entities;

namespace RootFix.Application.Interfaces.Repositories;

public interface IComunidadeRepository
{
    Task<Comunidade?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comunidade>> ListarDoGestorAsync(
        long gestorId,
        CancellationToken cancellationToken = default);

    /// <summary>Indica se o usuário participa da comunidade (tabela PARTICIPACAO).</summary>
    Task<bool> EhMembroAsync(
        long comunidadeId,
        long usuarioId,
        CancellationToken cancellationToken = default);
}
