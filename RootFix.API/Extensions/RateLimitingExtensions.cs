using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RootFix.API.Auth;

namespace RootFix.API.Extensions;

/// <summary>Limites lidos da seção <c>RateLimiting</c>.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Consultas do painel e acompanhamento: janela fixa por usuário.</summary>
    public int LeituraPorMinuto { get; set; } = 60;

    /// <summary>Mudanças de status/prioridade: balde de fichas por usuário.</summary>
    public int EscritaCapacidade { get; set; } = 10;

    /// <summary>Fichas devolvidas ao balde de escrita a cada minuto.</summary>
    public int EscritaReposicaoPorMinuto { get; set; } = 10;
}

/// <summary>
/// Rate limiting com o middleware nativo do ASP.NET Core (<c>Microsoft.AspNetCore.RateLimiting</c>).
/// </summary>
/// <remarks>
/// <para>Duas políticas, ambas particionadas <b>por usuário</b> (claim do JWT) — um gestor
/// abusando não afeta os outros. Sem token, a partição é o IP.</para>
/// <list type="bullet">
///   <item><see cref="Leitura"/>: janela fixa (ex.: 60 requisições por minuto).</item>
///   <item><see cref="Escrita"/>: token bucket (ex.: 10 fichas, 10 repostas por minuto). Permite
///   uma rajada curta — o gestor atualizando vários problemas seguidos — mas não um loop.</item>
/// </list>
/// <para>Estourou o limite: <c>429 Too Many Requests</c> com <c>Retry-After</c> e ProblemDetails.</para>
/// </remarks>
public static class RateLimitingExtensions
{
    public const string Leitura = "leitura";
    public const string Escrita = "escrita";

    public static IServiceCollection AddRateLimitingPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(Leitura, context =>
            {
                var options = Opcoes(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    ChaveDaParticao(context, Leitura),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.LeituraPorMinuto,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            limiter.AddPolicy(Escrita, context =>
            {
                var options = Opcoes(context);
                return RateLimitPartition.GetTokenBucketLimiter(
                    ChaveDaParticao(context, Escrita),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = options.EscritaCapacidade,
                        TokensPerPeriod = options.EscritaReposicaoPorMinuto,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            limiter.OnRejected = async (rejeicao, cancellationToken) =>
            {
                var http = rejeicao.HttpContext;
                var segundos = 60;

                if (rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    segundos = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

                var problema = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas requisições",
                    Detail = $"Limite de requisições atingido. Tente novamente em {segundos} segundo(s).",
                    Instance = $"{http.Request.Method} {http.Request.Path}"
                };

                await http.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
            };
        });

        return services;
    }

    private static RateLimitingOptions Opcoes(HttpContext context)
        => context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static string ChaveDaParticao(HttpContext context, string politica)
    {
        var claimUsuario = context.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value.ClaimUsuario;
        var usuario = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(claimUsuario)?.Value
            : null;

        return usuario is not null
            ? $"{politica}:usuario:{usuario}"
            : $"{politica}:ip:{context.Connection.RemoteIpAddress}";
    }
}
