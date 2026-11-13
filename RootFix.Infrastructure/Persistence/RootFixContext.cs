using Microsoft.EntityFrameworkCore;
using RootFix.Domain.Entities;
using RootFix.Infrastructure.Idempotencia;

namespace RootFix.Infrastructure.Persistence;

/// <summary>
/// Contexto do EF Core sobre o Oracle compartilhado com a API Java.
/// </summary>
/// <remarks>
/// Não há migrations: o schema é o DDL oficial do grupo (<c>scripts/script-bd.sql</c>, o mesmo
/// do DevOps). O EF Core apenas mapeia as tabelas existentes, como o JPA faz do lado Java.
/// </remarks>
public class RootFixContext : DbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Comunidade> Comunidades => Set<Comunidade>();

    public DbSet<Participacao> Participacoes => Set<Participacao>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Problema> Problemas => Set<Problema>();

    public DbSet<Apoio> Apoios => Set<Apoio>();

    public DbSet<HistoricoStatus> HistoricosStatus => Set<HistoricoStatus>();

    public DbSet<RegistroIdempotencia> RegistrosIdempotencia => Set<RegistroIdempotencia>();

    public RootFixContext(DbContextOptions<RootFixContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RootFixContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
