using Microsoft.AspNetCore.Http.HttpResults;
using RootFix.API.Auth;
using RootFix.API.Extensions;
using RootFix.API.Filters;
using RootFix.Application.DTOs.Acompanhamento;
using RootFix.Application.Interfaces.Services;

namespace RootFix.API.Endpoints;

/// <summary>
/// Endpoint voltado ao cidadão: o morador acompanha o problema que publicou ou apoiou.
/// </summary>
public static class AcompanhamentoEndpoints
{
    public static IEndpointRouteBuilder MapAcompanhamentoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/problemas/{problemaId:long}/acompanhamento", Obter)
            .WithTags("Acompanhamento do morador")
            .RequireAuthorization(Politicas.Autenticado)
            .RequireRateLimiting(RateLimitingExtensions.Leitura)
            .AddEndpointFilter<ValidacaoFilter>()
            .WithName("AcompanharProblema")
            .WithSummary("Situação e linha do tempo de um problema")
            .WithDescription(
                "Para membros da comunidade (e o gestor dela): se o problema foi adotado, o status atual, uma frase "
                + "pronta para exibir e a linha do tempo com os recados do gestor a cada mudança.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<Ok<AcompanhamentoResponse>> Obter(
        long problemaId,
        IUsuarioAtual usuarioAtual,
        IAcompanhamentoService acompanhamento,
        CancellationToken cancellationToken)
    {
        var usuarioId = await usuarioAtual.ObterIdAsync(cancellationToken);
        return TypedResults.Ok(await acompanhamento.ObterAsync(usuarioId, problemaId, cancellationToken));
    }
}
