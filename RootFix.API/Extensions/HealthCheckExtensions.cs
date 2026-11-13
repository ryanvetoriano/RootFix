using Microsoft.Extensions.Diagnostics.HealthChecks;
using RootFix.API.HealthChecks;
using RootFix.Infrastructure.Mensageria;

namespace RootFix.API.Extensions;

/// <summary>
/// Health checks da API, do banco e dos serviços externos (API Java e RabbitMQ).
/// </summary>
public static class HealthCheckExtensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddApplicationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        // API: o processo está vivo e respondendo.
        builder.AddCheck("api", () => HealthCheckResult.Healthy("API em execução."), tags: [LiveTag, ReadyTag]);

        // Oracle: sem ele a API não funciona, então derruba o readiness.
        builder.AddCheck<OracleHealthCheck>(
            OracleHealthCheck.Name,
            failureStatus: HealthStatus.Unhealthy,
            tags: [ReadyTag, "db"],
            timeout: TimeSpan.FromSeconds(10));

        // RabbitMQ: só quando a mensageria está ligada (adendo 2).
        var rabbit = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        if (!rabbit.EstaConfigurado && !string.IsNullOrWhiteSpace(configuration["RABBITMQ_URL"]))
            rabbit.Uri = configuration["RABBITMQ_URL"];

        if (rabbit.EstaConfigurado)
        {
            builder.AddCheck<RabbitMqHealthCheck>(
                RabbitMqHealthCheck.Name,
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag, "external", "mensageria"],
                timeout: TimeSpan.FromSeconds(rabbit.TimeoutConexaoSegundos + 2));
        }

        // Serviços HTTP externos (a API Java). Entradas sem URL são ignoradas.
        var externos = configuration.GetSection(ExternalServiceOptions.SectionName)
            .Get<List<ExternalServiceOptions>>() ?? [];

        var validos = externos
            .Where(e => !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Url))
            .ToList();

        if (validos.Count > 0)
            services.AddHttpClient(ExternalServiceHealthCheck.HttpClientName);

        foreach (var externo in validos)
        {
            builder.Add(new HealthCheckRegistration(
                externo.Name,
                sp => new ExternalServiceHealthCheck(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    externo,
                    sp.GetRequiredService<ILogger<ExternalServiceHealthCheck>>()),
                failureStatus: externo.Optional ? HealthStatus.Degraded : HealthStatus.Unhealthy,
                tags: [ReadyTag, "external"],
                timeout: TimeSpan.FromSeconds(externo.TimeoutSeconds + 2)));
        }

        return services;
    }

    /// <summary>
    /// <c>/health</c> (tudo), <c>/health/live</c> (só o processo) e <c>/health/ready</c>
    /// (com dependências). Públicos: sondas de monitoramento não enviam token.
    /// </summary>
    public static IEndpointRouteBuilder MapApplicationHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new()
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        });

        endpoints.MapHealthChecks("/health/live", new()
        {
            Predicate = r => r.Tags.Contains(LiveTag),
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        });

        endpoints.MapHealthChecks("/health/ready", new()
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        });

        return endpoints;
    }
}
