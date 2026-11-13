using Microsoft.AspNetCore.Http.HttpResults;
using RootFix.API.Auth;
using RootFix.API.Extensions;
using RootFix.API.Filters;
using RootFix.Application.DTOs.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Services;

namespace RootFix.API.Endpoints;

/// <summary>
/// Painel do gestor: problemas adotados, status, prioridade e indicadores.
/// Todo o grupo exige JWT com perfil GESTOR; a checagem de que ele é gestor <b>daquela</b>
/// comunidade fica nos serviços (só o banco sabe disso).
/// </summary>
public static class PainelEndpoints
{
    public static IEndpointRouteBuilder MapPainelEndpoints(this IEndpointRouteBuilder app)
    {
        var painel = app.MapGroup("/painel")
            .WithTags("Painel do gestor")
            .RequireAuthorization(Politicas.Gestor)
            .AddEndpointFilter<ValidacaoFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        painel.MapGet("/comunidades", ListarComunidades)
            .RequireRateLimiting(RateLimitingExtensions.Leitura)
            .WithName("ListarComunidadesDoGestor")
            .WithSummary("Comunidades que o gestor administra")
            .WithDescription("Lista as comunidades do gestor autenticado com o resumo de problemas supervisionados por status.");

        painel.MapGet("/comunidades/{comunidadeId:long}/supervisionados", BuscarSupervisionados)
            .RequireRateLimiting(RateLimitingExtensions.Leitura)
            .WithName("BuscarSupervisionados")
            .WithSummary("Problemas adotados da comunidade (paginado)")
            .WithDescription(
                "Filtros: status, prioridade, semPrioridade, titulo. Ordenação (ordenarPor): adotadoEm (padrão), "
                + "apoios, prioridade, gravidade, criadoEm; direcao asc|desc. Cada item traz os próximos status possíveis.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        painel.MapGet("/comunidades/{comunidadeId:long}/indicadores", ObterIndicadores)
            .RequireRateLimiting(RateLimitingExtensions.Leitura)
            .WithName("ObterIndicadores")
            .WithSummary("Indicadores da comunidade")
            .WithDescription(
                "Pendentes, em andamento, resolvidos, arquivados, distribuição por prioridade, tempo médio de "
                + "resolução (horas entre adoção e RESOLVIDO), taxa de resolução e os 5 problemas abertos mais apoiados.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        painel.MapGet("/problemas/{problemaId:long}", ObterDetalhe)
            .RequireRateLimiting(RateLimitingExtensions.Leitura)
            .WithName("ObterProblemaNoPainel")
            .WithSummary("Detalhe do problema com a linha do tempo de status")
            .ProducesProblem(StatusCodes.Status404NotFound);

        painel.MapPatch("/problemas/{problemaId:long}/status", AlterarStatus)
            .RequireRateLimiting(RateLimitingExtensions.Escrita)
            .ComIdempotencia(obrigatoria: true)
            .WithName("AlterarStatus")
            .WithSummary("Muda o status de um problema adotado")
            .WithDescription(
                "Transições: PENDENTE → EM_ANDAMENTO | ARQUIVADO; EM_ANDAMENTO → RESOLVIDO | ARQUIVADO | PENDENTE. "
                + "RESOLVIDO e ARQUIVADO são finais. Grava o histórico na mesma transação e publica "
                + "problema.status-alterado (se o RabbitMQ estiver configurado). Exige Idempotency-Key.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        painel.MapPatch("/problemas/{problemaId:long}/prioridade", DefinirPrioridade)
            .RequireRateLimiting(RateLimitingExtensions.Escrita)
            .ComIdempotencia(obrigatoria: false)
            .WithName("DefinirPrioridade")
            .WithSummary("Define a prioridade de um problema adotado")
            .WithDescription("Não é permitido em problemas RESOLVIDO ou ARQUIVADO. Idempotency-Key opcional.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ComunidadePainelResponse>>> ListarComunidades(
        IUsuarioAtual usuarioAtual,
        IPainelService painel,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await painel.ListarComunidadesAsync(gestorId, cancellationToken));
    }

    private static async Task<Ok<PagedResponse<ProblemaSupervisionadoResponse>>> BuscarSupervisionados(
        long comunidadeId,
        [AsParameters] SupervisionadosFiltro filtro,
        IUsuarioAtual usuarioAtual,
        IPainelService painel,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await painel.BuscarSupervisionadosAsync(gestorId, comunidadeId, filtro, cancellationToken));
    }

    private static async Task<Ok<IndicadoresResponse>> ObterIndicadores(
        long comunidadeId,
        IUsuarioAtual usuarioAtual,
        IIndicadoresService indicadores,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await indicadores.CalcularAsync(gestorId, comunidadeId, cancellationToken));
    }

    private static async Task<Ok<ProblemaDetalheResponse>> ObterDetalhe(
        long problemaId,
        IUsuarioAtual usuarioAtual,
        IPainelService painel,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await painel.ObterDetalheAsync(gestorId, problemaId, cancellationToken));
    }

    private static async Task<Ok<AlteracaoStatusResponse>> AlterarStatus(
        long problemaId,
        AlterarStatusRequest request,
        IUsuarioAtual usuarioAtual,
        IPainelService painel,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await painel.AlterarStatusAsync(gestorId, problemaId, request, cancellationToken));
    }

    private static async Task<Ok<PrioridadeResponse>> DefinirPrioridade(
        long problemaId,
        DefinirPrioridadeRequest request,
        IUsuarioAtual usuarioAtual,
        IPainelService painel,
        CancellationToken cancellationToken)
    {
        var gestorId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await painel.DefinirPrioridadeAsync(gestorId, problemaId, request, cancellationToken));
    }
}
