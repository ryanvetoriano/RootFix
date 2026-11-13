using System.Diagnostics;
using Serilog.Context;

namespace RootFix.API.Middleware;

/// <summary>
/// Garante que toda requisição tenha um identificador de correlação. O valor enviado pelo
/// cliente em <c>X-Correlation-ID</c> é reaproveitado; senão, um novo é gerado. Ele volta no
/// cabeçalho da resposta, aparece em todos os logs da requisição e nos ProblemDetails, o que
/// permite achar no log do servidor o erro que o app mostrou.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaxLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers[HeaderName].FirstOrDefault();

        // Vazio, longo demais ou com caracteres de controle: não é confiável, gera outro.
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength || candidate.Any(char.IsControl))
            return Guid.NewGuid().ToString();

        return candidate;
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
