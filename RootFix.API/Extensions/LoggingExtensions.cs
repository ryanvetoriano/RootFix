using RootFix.API.Middleware;
using Serilog;
using Serilog.Events;

namespace RootFix.API.Extensions;

/// <summary>
/// Logging estruturado com Serilog. No Render, o console é o destino dos logs.
/// </summary>
public static class LoggingExtensions
{
    public static IHostBuilder AddSerilogLogging(this ConfigureHostBuilder host)
    {
        // preserveStaticLogger: os testes de integração sobem mais de um host no mesmo processo.
        host.UseSerilog(
            (context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "RootFix.API.Dotnet")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName),
            preserveStaticLogger: true);

        return host;
    }

    /// <summary>
    /// Uma linha de log por requisição com método, rota, status e duração. Cabeçalhos de
    /// autenticação, corpo e query string não são registrados (podem ter token e dados pessoais).
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        var loggerDaAplicacao = app.ApplicationServices.GetService<Serilog.ILogger>();

        app.UseSerilogRequestLogging(options =>
        {
            options.Logger = loggerDaAplicacao;
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
                : httpContext.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Debug
                : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId))
                    diagnosticContext.Set("CorrelationId", correlationId);
            };
        });

        return app;
    }
}
