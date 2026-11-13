using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Infrastructure.Persistence;

namespace RootFix.IntegrationTests.Fixtures;

/// <summary>
/// Cenário base e fábrica de dados dos testes de integração.
/// </summary>
/// <remarks>
/// Em produção, usuários, comunidades e problemas são criados pela API Java. Aqui eles são
/// inseridos direto no banco, simulando o que o Java já gravou. Cada teste que altera dados
/// cria os próprios problemas (IDs únicos), então os testes não interferem uns nos outros.
/// </remarks>
public static class DadosDeTeste
{
    public const long GestoraId = 1;
    public const string GestoraEmail = "gestora@rootfix.test";
    public const long OutroGestorId = 2;
    public const long MoradorId = 3;
    public const long MoradorDeForaId = 4;

    public const long CategoriaBuracoId = 1;
    public const string CategoriaBuraco = "Buraco ou pavimento";

    public const long ComunidadeId = 10;
    public const long ComunidadeDoOutroGestorId = 20;

    private static long _proximoId = 1_000;

    public static long NovoId() => Interlocked.Increment(ref _proximoId);

    public static readonly DateTime Agora = DateTime.UtcNow;

    public static async Task SemearBaseAsync(RootFixContext context)
    {
        context.Usuarios.AddRange(
            new Usuario(GestoraId, "Gestora Ana", GestoraEmail, Perfil.GESTOR, "Associação Vila Esperança"),
            new Usuario(OutroGestorId, "Gestor Bruno", "bruno@rootfix.test", Perfil.GESTOR),
            new Usuario(MoradorId, "Morador Carlos", "carlos@rootfix.test", Perfil.MORADOR),
            new Usuario(MoradorDeForaId, "Moradora Dora", "dora@rootfix.test", Perfil.MORADOR));

        context.Comunidades.AddRange(
            new Comunidade(ComunidadeId, "Vila Esperança", GestoraId, "Comunidade de teste"),
            new Comunidade(ComunidadeDoOutroGestorId, "Jardim Norte", OutroGestorId));

        context.Participacoes.Add(new Participacao(1, MoradorId, ComunidadeId, Agora.AddDays(-30)));

        context.Categorias.Add(new Categoria(CategoriaBuracoId, CategoriaBuraco));

        await context.SaveChangesAsync();
    }

    /// <summary>Cria uma comunidade nova (para testes que contam problemas sem interferência).</summary>
    public static async Task<long> CriarComunidadeAsync(RootFixApiFactory factory, long gestorId = GestoraId)
    {
        var id = NovoId();

        using var scope = factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();
        context.Comunidades.Add(new Comunidade(id, $"Comunidade {id}", gestorId));
        await context.SaveChangesAsync();

        return id;
    }

    /// <summary>
    /// Cria um problema como o Java teria gravado: publicado por um morador e, se pedido,
    /// adotado (com o status inicial que o Java gravar, ou nulo).
    /// </summary>
    public static async Task<long> CriarProblemaAsync(
        RootFixApiFactory factory,
        long comunidadeId = ComunidadeId,
        bool adotado = true,
        StatusProblema? status = StatusProblema.PENDENTE,
        int apoios = 0,
        Prioridade? prioridade = null,
        string? titulo = null,
        DateTime? adotadoEm = null)
    {
        var id = NovoId();

        using var scope = factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();

        var problema = new Problema(
            id, comunidadeId, MoradorId, titulo ?? $"Problema {id}", Agora.AddDays(-5),
            "Descrição do problema", "ALTA", -23.55, -46.63,
            CategoriaBuracoId, "Buraco em via de tráfego com risco de acidente.");

        context.Problemas.Add(problema);
        await context.SaveChangesAsync();

        // Status, prioridade e adoção são gravados por SQL, como o Java faria, para o teste não
        // depender das regras de transição do domínio ao montar o cenário.
        if (adotado)
        {
            await context.Problemas.Where(p => p.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Adotado, true)
                .SetProperty(p => p.AdotadoEm, adotadoEm ?? Agora.AddDays(-4))
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.Prioridade, prioridade));
        }

        for (var i = 0; i < apoios; i++)
            context.Apoios.Add(new Apoio(NovoId(), id, MoradorId, Agora));

        await context.SaveChangesAsync();

        return id;
    }

    public static async Task<List<HistoricoStatus>> HistoricoAsync(RootFixApiFactory factory, long problemaId)
    {
        using var scope = factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();

        return await context.HistoricosStatus.AsNoTracking()
            .Where(h => h.ProblemaId == problemaId)
            .OrderBy(h => h.Id)
            .ToListAsync();
    }

    public static async Task<Problema> ProblemaAsync(RootFixApiFactory factory, long problemaId)
    {
        using var scope = factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();

        return await context.Problemas.AsNoTracking().SingleAsync(p => p.Id == problemaId);
    }

    public static async Task AdicionarHistoricoAsync(
        RootFixApiFactory factory,
        long problemaId,
        StatusProblema? anterior,
        StatusProblema novo,
        DateTime dataHora,
        string? observacao = null)
    {
        using var scope = factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();

        context.HistoricosStatus.Add(
            HistoricoStatus.Reconstituir(0, problemaId, anterior, novo, dataHora, GestoraId, observacao));
        await context.SaveChangesAsync();
    }
}
