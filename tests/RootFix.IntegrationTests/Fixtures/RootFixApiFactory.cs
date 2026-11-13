using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RootFix.Application.Interfaces.Mensageria;
using RootFix.Infrastructure.Persistence;

namespace RootFix.IntegrationTests.Fixtures;

/// <summary>
/// Sobe a API completa em memória: pipeline HTTP, autenticação JWT, filtros, rate limiting,
/// EF Core, health checks — tudo real, exceto o banco e a fila.
/// </summary>
/// <remarks>
/// <para><b>Banco.</b> As tabelas são do Raul e ainda estão sendo fechadas (Marco 0); os testes
/// não podem depender de um Oracle compartilhado nem de credenciais. Usamos SQLite (arquivo temporário)
/// com o <b>mesmo modelo do EF Core</b> (mesmas entidades, configurações, token de concorrência
/// e índice único da idempotência), criado do zero a cada fábrica e apagado no final. O SQL do Oracle em si é
/// coberto pelos testes manuais no ambiente com o DDL oficial.</para>
/// <para><b>Fila.</b> O publicador do RabbitMQ é trocado por um que guarda os eventos em
/// memória, para os testes verificarem o que seria enviado ao Java.</para>
/// <para><b>JWT.</b> Uma chave aleatória por execução, injetada como se fosse a do Java.</para>
/// </remarks>
public class RootFixApiFactory : WebApplicationFactory<Program>
{
    // Arquivo temporário (e não ":memory:" com uma conexão compartilhada): cada DbContext abre a
    // própria conexão, como no Oracle com pool. Uma única SqliteConnection dividida entre
    // requisições simultâneas não é thread-safe e quebraria o teste de concorrência.
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"rootfix-testes-{Guid.NewGuid():N}.db");
    // Como o JWT_SECRET do Java: texto com 32+ caracteres, usado pelos bytes UTF-8.
    private readonly string _chaveJwt = $"segredo-de-teste-{Guid.NewGuid():N}-{Guid.NewGuid():N}";

    public PublicadorEmMemoria Publicador { get; } = new();

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _arquivo,
        Pooling = false,
        DefaultTimeout = 30 // espera o lock de escrita em vez de falhar na hora
    }.ToString();

    /// <summary>Configurações extras de cada cenário (ex.: limites baixos de rate limiting).</summary>
    protected virtual IReadOnlyDictionary<string, string?> ConfiguracoesExtras => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:SigningKey", _chaveJwt);
        builder.UseSetting("Jwt:FormatoChave", "Texto");
        builder.UseSetting("Jwt:Issuer", "rootfix-api");

        // O appsettings.json aponta para o health da API Java no Render; os testes não saem
        // para a internet. Fábricas específicas sobrescrevem isto em ConfiguracoesExtras.
        builder.UseSetting("HealthChecks:ExternalServices:0:Url", "");
        builder.UseSetting("ConnectionStrings:OracleConnection", "nao-usada-nos-testes");

        // Limites altos por padrão: só os testes de rate limiting querem bater no 429.
        builder.UseSetting("RateLimiting:LeituraPorMinuto", "10000");
        builder.UseSetting("RateLimiting:EscritaCapacidade", "10000");
        builder.UseSetting("RateLimiting:EscritaReposicaoPorMinuto", "10000");

        foreach (var (chave, valor) in ConfiguracoesExtras)
            builder.UseSetting(chave, valor);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RootFixContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<RootFixContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<RootFixContext>>();

            services.AddDbContext<RootFixContext>(options => options.UseSqlite(ConnectionString));

            services.RemoveAll<IPublicadorDeEventos>();
            services.AddSingleton<IPublicadorDeEventos>(Publicador);
        });
    }

    /// <summary>Cria o schema e os dados base. Chamado uma vez por fábrica.</summary>
    public async Task InicializarBancoAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();

        await context.Database.EnsureCreatedAsync();
        // WAL: leituras não bloqueiam a escrita concorrente (mais próximo do Oracle).
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await DadosDeTeste.SemearBaseAsync(context);
    }

    public IServiceScope CriarEscopo() => Services.CreateScope();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        foreach (var arquivo in new[] { _arquivo, _arquivo + "-wal", _arquivo + "-shm" })
        {
            try { File.Delete(arquivo); } catch (IOException) { /* temporário; o SO limpa depois */ }
        }
    }
}

/// <summary>Guarda os eventos que seriam publicados no RabbitMQ.</summary>
public sealed class PublicadorEmMemoria : IPublicadorDeEventos
{
    private readonly ConcurrentQueue<ProblemaStatusAlteradoEvento> _eventos = new();

    public IReadOnlyCollection<ProblemaStatusAlteradoEvento> Eventos => _eventos.ToArray();

    public IEnumerable<ProblemaStatusAlteradoEvento> DoProblema(long problemaId)
        => _eventos.Where(e => e.ProblemaId == problemaId);

    public Task PublicarStatusAlteradoAsync(ProblemaStatusAlteradoEvento evento, CancellationToken cancellationToken = default)
    {
        _eventos.Enqueue(evento);
        return Task.CompletedTask;
    }
}
