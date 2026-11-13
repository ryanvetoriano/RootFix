using Microsoft.EntityFrameworkCore;
using RootFix.Application.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;

namespace RootFix.Infrastructure.Persistence.Repositories;

public sealed class ProblemaRepository : IProblemaRepository
{
    private readonly RootFixContext _context;

    public ProblemaRepository(RootFixContext context)
    {
        _context = context;
    }

    public Task<Problema?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _context.Problemas
            .Include(p => p.Comunidade)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<ProblemaResumo>> BuscarSupervisionadosAsync(
        long comunidadeId,
        SupervisionadosFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        var query = ComContagens(Supervisionados(comunidadeId));

        if (filtro.Status.HasValue)
        {
            var status = filtro.Status.Value;

            // Adotado sem status gravado é PENDENTE (regra do domínio), então entra no filtro.
            query = status == StatusProblema.PENDENTE
                ? query.Where(x => x.Problema.Status == status || x.Problema.Status == null)
                : query.Where(x => x.Problema.Status == status);
        }

        if (filtro.Prioridade.HasValue)
        {
            var prioridade = filtro.Prioridade.Value;
            query = query.Where(x => x.Problema.Prioridade == prioridade);
        }
        else if (filtro.SemPrioridade == true)
        {
            query = query.Where(x => x.Problema.Prioridade == null);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Titulo))
        {
            var titulo = filtro.Titulo.Trim().ToUpper();
            query = query.Where(x => x.Problema.Titulo.ToUpper().Contains(titulo));
        }

        var desc = filtro.OrdemDescendente();

        // Prioridade e gravidade são texto no banco; a ordem de urgência vem de um CASE,
        // não da ordem alfabética (que poria ALTA antes de URGENTE).
        var ordenada = filtro.CampoOrdenacao() switch
        {
            "apoios" => query.OrdenarPor(x => x.TotalApoios, desc),
            "criadoem" => query.OrdenarPor(x => x.Problema.CriadoEm, desc),
            "prioridade" => query.OrdenarPor(x =>
                x.Problema.Prioridade == Prioridade.URGENTE ? 4 :
                x.Problema.Prioridade == Prioridade.ALTA ? 3 :
                x.Problema.Prioridade == Prioridade.MEDIA ? 2 :
                x.Problema.Prioridade == Prioridade.BAIXA ? 1 : 0, desc),
            "gravidade" => query.OrdenarPor(x =>
                x.Problema.GravidadeIa == "CRITICA" ? 4 :
                x.Problema.GravidadeIa == "ALTA" ? 3 :
                x.Problema.GravidadeIa == "MEDIA" ? 2 :
                x.Problema.GravidadeIa == "BAIXA" ? 1 : 0, desc),
            _ => query.OrdenarPor(x => x.Problema.AdotadoEm, desc)
        };

        var total = await ordenada.CountAsync(cancellationToken);

        var itens = await ordenada
            .ThenBy(x => x.Problema.Id)
            .Skip(filtro.Deslocamento())
            .Take(filtro.TamanhoPaginaAtual)
            .Select(ParaResumo())
            .ToListAsync(cancellationToken);

        return new PagedResult<ProblemaResumo>(itens, total);
    }

    public async Task<IReadOnlyList<ProblemaResumo>> ListarSupervisionadosAsync(
        long comunidadeId,
        CancellationToken cancellationToken = default)
        => await ComContagens(Supervisionados(comunidadeId))
            .Select(ParaResumo())
            .ToListAsync(cancellationToken);

    public Task<ProblemaResumo?> ObterResumoAsync(long id, CancellationToken cancellationToken = default)
        => ComContagens(_context.Problemas.AsNoTracking().Where(p => p.Id == id))
            .Select(ParaResumo())
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ContagemPorStatus>> ContarSupervisionadosPorStatusAsync(
        IReadOnlyCollection<long> comunidadeIds,
        CancellationToken cancellationToken = default)
        => await _context.Problemas.AsNoTracking()
            .Where(p => p.Adotado && comunidadeIds.Contains(p.ComunidadeId))
            .GroupBy(p => new { p.ComunidadeId, p.Status })
            .Select(g => new ContagemPorStatus(g.Key.ComunidadeId, g.Key.Status, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HistoricoStatus>> ListarHistoricoAsync(
        long problemaId,
        CancellationToken cancellationToken = default)
        => await _context.HistoricosStatus.AsNoTracking()
            .Where(h => h.ProblemaId == problemaId)
            .OrderBy(h => h.DataHora)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HistoricoStatus>> ListarHistoricoDaComunidadeAsync(
        long comunidadeId,
        StatusProblema statusNovo,
        CancellationToken cancellationToken = default)
        => await (
                from h in _context.HistoricosStatus.AsNoTracking()
                join p in _context.Problemas on h.ProblemaId equals p.Id
                where p.ComunidadeId == comunidadeId && h.StatusNovo == statusNovo
                select h)
            .ToListAsync(cancellationToken);

    public async Task SalvarAlteracaoDeStatusAsync(
        Problema problema,
        HistoricoStatus historico,
        CancellationToken cancellationToken = default)
    {
        // Um único SaveChanges = uma única transação: o UPDATE do status e o INSERT do
        // histórico são gravados juntos ou nenhum dos dois.
        _context.HistoricosStatus.Add(historico);
        await SalvarComControleDeConcorrenciaAsync(cancellationToken);
    }

    public Task SalvarAsync(Problema problema, CancellationToken cancellationToken = default)
        => SalvarComControleDeConcorrenciaAsync(cancellationToken);

    private async Task SalvarComControleDeConcorrenciaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflitoException(
                "O problema foi alterado por outra pessoa enquanto você editava. "
                + "Atualize o painel e tente de novo.");
        }
    }

    private IQueryable<Problema> Supervisionados(long comunidadeId)
        => _context.Problemas.AsNoTracking()
            .Where(p => p.ComunidadeId == comunidadeId && p.Adotado);

    /// <summary>
    /// Junta autor e contagem de apoios antes de ordenar e paginar, para que a ordenação por
    /// apoios aconteça no banco (subconsulta correlacionada) e não em memória.
    /// </summary>
    private IQueryable<ProblemaComContagens> ComContagens(IQueryable<Problema> query)
        => query.Select(p => new ProblemaComContagens
        {
            Problema = p,
            AutorNome = p.Autor!.Nome,
            TotalApoios = _context.Apoios.Count(a => a.ProblemaId == p.Id)
        });

    private static System.Linq.Expressions.Expression<Func<ProblemaComContagens, ProblemaResumo>> ParaResumo()
        => x => new ProblemaResumo(
            x.Problema.Id,
            x.Problema.ComunidadeId,
            x.Problema.Titulo,
            x.Problema.Descricao,
            x.Problema.Adotado,
            x.Problema.Status,
            x.Problema.Prioridade,
            x.Problema.GravidadeIa,
            x.Problema.CriadoEm,
            x.Problema.AdotadoEm,
            x.AutorNome,
            x.TotalApoios,
            x.Problema.Latitude,
            x.Problema.Longitude,
            x.Problema.Categoria != null ? x.Problema.Categoria.Nome : null,
            x.Problema.JustificativaIa);

    private sealed class ProblemaComContagens
    {
        public Problema Problema { get; init; } = null!;
        public string AutorNome { get; init; } = null!;
        public int TotalApoios { get; init; }
    }
}
