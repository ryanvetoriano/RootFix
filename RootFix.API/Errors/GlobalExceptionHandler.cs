using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RootFix.API.Middleware;
using RootFix.Domain.Exceptions;

namespace RootFix.API.Errors;

/// <summary>
/// Tratamento global de exceções. Traduz as exceções conhecidas em respostas
/// <c>application/problem+json</c> com o código HTTP adequado e transforma qualquer falha
/// inesperada em um 500 genérico, sem vazar stack trace.
/// </summary>
/// <remarks>
/// <list type="table">
///   <item><term>400</term><description>JSON malformado, enum ou parâmetro com tipo errado.</description></item>
///   <item><term>403</term><description><see cref="AcessoNegadoException"/> (gestor de outra comunidade...).</description></item>
///   <item><term>404</term><description><see cref="NotFoundException"/>.</description></item>
///   <item><term>409</term><description><see cref="ConflitoException"/> (status repetido, edição simultânea).</description></item>
///   <item><term>422</term><description><see cref="DomainException"/> (regra de negócio, ex.: transição proibida).</description></item>
/// </list>
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = Traduzir(exception);

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Erro não tratado em {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("Requisição rejeitada em {Method} {Path} com status {StatusCode}: {Motivo}",
                httpContext.Request.Method, httpContext.Request.Path, status, exception.Message);
        }

        Activity.Current?.SetStatus(ActivityStatusCode.Error, title);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
            problemDetails.Extensions["correlationId"] = correlationId;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int Status, string Title, string Detail) Traduzir(Exception exception)
        => exception switch
        {
            BadHttpRequestException bad => (
                bad.StatusCode,
                "Requisição inválida",
                "O corpo ou os parâmetros da requisição não estão no formato esperado. "
                + "Confira os tipos dos campos e os valores aceitos nos enums (ex.: EM_ANDAMENTO)."),

            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),

            AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado", exception.Message),

            ConflitoException => (StatusCodes.Status409Conflict, "Conflito com o estado atual", exception.Message),

            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", exception.Message),

            _ => (StatusCodes.Status500InternalServerError,
                "Erro interno do servidor",
                "Ocorreu um erro inesperado ao processar a requisição.")
        };
}
