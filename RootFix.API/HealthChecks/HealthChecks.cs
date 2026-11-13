using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RootFix.Infrastructure.Mensageria;
using RootFix.Infrastructure.Persistence;

namespace RootFix.API.HealthChecks;

/// <summary>
/// Conectividade real com o Oracle via <c>Database.CanConnectAsync()</c>, que abre uma conexão.
/// A descrição nunca inclui a connection string.
/// </summary>
public sealed class OracleHealthCheck : IHealthCheck
{
    public const string Name = "oracle";

    private readonly RootFixContext _context;
    private readonly ILogger<OracleHealthCheck> _logger;

    public OracleHealthCheck(RootFixContext context, ILogger<OracleHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();

        try
        {
            var conectou = await _context.Database.CanConnectAsync(cancellationToken);
            var dados = Dados(cronometro);

            return conectou
                ? HealthCheckResult.Healthy("Conexão com o banco estabelecida.", dados)
                : HealthCheckResult.Unhealthy("Não foi possível conectar ao banco.", data: dados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check do banco falhou.");
            return HealthCheckResult.Unhealthy("Falha ao verificar o banco.", data: Dados(cronometro));
        }
    }

    private static IReadOnlyDictionary<string, object> Dados(Stopwatch cronometro)
        => new Dictionary<string, object> { ["elapsedMs"] = cronometro.ElapsedMilliseconds };
}

/// <summary>
/// Conexão com o RabbitMQ. Só é registrado quando <c>RabbitMq:Uri</c> está configurado.
/// </summary>
/// <remarks>
/// Falha vira <c>Degraded</c>, não <c>Unhealthy</c>: o status continua sendo gravado no Oracle
/// mesmo com a fila fora do ar, então a API segue útil.
/// </remarks>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    public const string Name = "rabbitmq";

    private readonly RabbitMqConexao _conexao;
    private readonly ILogger<RabbitMqHealthCheck> _logger;

    public RabbitMqHealthCheck(RabbitMqConexao conexao, ILogger<RabbitMqHealthCheck> logger)
    {
        _conexao = conexao;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conexao = await _conexao.ObterAsync(cancellationToken);
            return conexao.IsOpen
                ? HealthCheckResult.Healthy("Conectado ao RabbitMQ.")
                : new HealthCheckResult(context.Registration.FailureStatus, "Conexão com o RabbitMQ fechada.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check do RabbitMQ falhou.");
            return new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ inacessível.");
        }
    }
}

/// <summary>Serviço HTTP externo monitorado (seção <c>HealthChecks:ExternalServices</c>).</summary>
public sealed class ExternalServiceOptions
{
    public const string SectionName = "HealthChecks:ExternalServices";

    public string Name { get; set; } = string.Empty;

    /// <summary>URL de verificação. Não deve conter credenciais.</summary>
    public string Url { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary><c>true</c> = indisponibilidade resulta em <c>Degraded</c>, não <c>Unhealthy</c>.</summary>
    public bool Optional { get; set; } = true;
}

/// <summary>
/// Verifica uma dependência HTTP externa — no RootFix, a API Java (o emissor do JWT).
/// </summary>
/// <remarks>
/// A API Java fica no Render gratuito e dorme após 15 minutos sem uso: a primeira chamada pode
/// levar ~1 minuto. Por isso ela é opcional (<c>Degraded</c> no timeout), e o timeout é curto
/// para o <c>/health</c> não travar esperando o Java acordar.
/// </remarks>
public sealed class ExternalServiceHealthCheck : IHealthCheck
{
    public const string HttpClientName = "health-checks";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalServiceOptions _options;
    private readonly ILogger<ExternalServiceHealthCheck> _logger;

    public ExternalServiceHealthCheck(
        IHttpClientFactory httpClientFactory,
        ExternalServiceOptions options,
        ILogger<ExternalServiceHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var falha = _options.Optional ? HealthStatus.Degraded : HealthStatus.Unhealthy;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).GetAsync(
                _options.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy($"Serviço {_options.Name} respondeu {(int)response.StatusCode}.");

            return new HealthCheckResult(falha, $"Serviço {_options.Name} respondeu {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(falha,
                $"Serviço {_options.Name} excedeu o timeout de {_options.TimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao consultar o serviço externo {Servico}.", _options.Name);
            return new HealthCheckResult(falha, $"Serviço {_options.Name} inacessível.");
        }
    }
}

/// <summary>
/// Resposta JSON dos health checks: nome, status, descrição, duração e tags de cada check —
/// nunca connection strings ou mensagens de exceção.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.ToString(),
                tags = entry.Value.Tags
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions), context.RequestAborted);
    }
}
