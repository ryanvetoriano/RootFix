using Microsoft.EntityFrameworkCore;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Domain.Entities;

namespace RootFix.Infrastructure.Persistence.Repositories;

public sealed class ComunidadeRepository : IComunidadeRepository
{
    private readonly RootFixContext _context;

    public ComunidadeRepository(RootFixContext context)
    {
        _context = context;
    }

    public Task<Comunidade?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _context.Comunidades.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Comunidade>> ListarDoGestorAsync(
        long gestorId,
        CancellationToken cancellationToken = default)
        => await _context.Comunidades.AsNoTracking()
            .Where(c => c.GestorId == gestorId)
            .OrderBy(c => c.Nome)
            .ToListAsync(cancellationToken);

    public Task<bool> EhMembroAsync(
        long comunidadeId,
        long usuarioId,
        CancellationToken cancellationToken = default)
        => _context.Participacoes.AsNoTracking()
            .AnyAsync(p => p.ComunidadeId == comunidadeId && p.UsuarioId == usuarioId, cancellationToken);
}
