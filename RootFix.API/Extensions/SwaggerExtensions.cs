using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using RootFix.API.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RootFix.API.Extensions;

/// <summary>
/// Documentação OpenAPI/Swagger: descrição da API, comentários XML dos DTOs, esquema Bearer e
/// o cabeçalho <c>Idempotency-Key</c> nos endpoints que o usam.
/// </summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "RootFix — API .NET (painel do gestor)",
                Version = "v1",
                Description =
                    """
                    Painel do gestor do **RootFix**, a zeladoria urbana por comunidade. Cuida dos
                    problemas **depois da adoção**: status, prioridade e indicadores. Também oferece
                    ao morador o **acompanhamento** do problema, com os recados do gestor.

                    **Autenticação:** use o mesmo JWT do login na API Java
                    (`POST /auth/login`). Clique em **Authorize** e cole apenas o token.
                    Em Development, `POST /dev/token` gera um token de teste.

                    **Idempotência:** `PATCH /painel/problemas/{id}/status` exige o cabeçalho
                    `Idempotency-Key` (um UUID por operação). Repetir a mesma chave devolve a mesma
                    resposta sem gravar de novo.

                    **Rate limiting:** por usuário. Estourou: `429` com `Retry-After`.

                    **Valores aceitos:** status `PENDENTE`, `EM_ANDAMENTO`, `RESOLVIDO`, `ARQUIVADO`;
                    prioridade `URGENTE`, `ALTA`, `MEDIA`, `BAIXA`.

                    **Erros:** sempre `application/problem+json` (RFC 9457) com `correlationId`.
                    """
            });

            foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Application.DependencyInjection).Assembly })
            {
                var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xml))
                    options.IncludeXmlComments(xml);
            }

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT emitido pela API Java (ou por POST /dev/token em Development)."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
            });

            options.OperationFilter<SecurityRequirementOperationFilter>();
            options.OperationFilter<IdempotencyKeyOperationFilter>();
            options.DocumentFilter<HealthChecksDocumentFilter>();
            options.DescribeAllParametersInCamelCase();
        });

        return services;
    }
}

/// <summary>
/// Tira o cadeado dos endpoints públicos (health, /dev/token) e documenta o 401/403 nos
/// protegidos. Pela especificação OpenAPI, <c>security</c> vazio na operação anula o requisito global.
/// </summary>
public sealed class SecurityRequirementOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var exigeAutenticacao = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

        if (exigeAutenticacao)
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Token ausente, inválido ou expirado." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Perfil sem permissão ou gestor de outra comunidade." });
            operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Limite de requisições atingido (veja Retry-After)." });
            return;
        }

        operation.Security = [];
    }
}

/// <summary>
/// Inclui <c>/health</c>, <c>/health/live</c> e <c>/health/ready</c> no Swagger. Eles são
/// mapeados por <c>MapHealthChecks</c>, que o ApiExplorer não enxerga; sem isto ficariam fora
/// da documentação interativa.
/// </summary>
public sealed class HealthChecksDocumentFilter : IDocumentFilter
{
    private static readonly (string Rota, string Resumo)[] Rotas =
    [
        ("/health", "Saúde completa: API, Oracle, API Java e RabbitMQ (se configurados)"),
        ("/health/live", "Liveness: só o processo da API (usado pelo Render)"),
        ("/health/ready", "Readiness: API e dependências")
    ];

    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        document.Paths ??= new OpenApiPaths();

        foreach (var (rota, resumo) in Rotas)
        {
            var operacao = new OpenApiOperation
            {
                Summary = resumo,
                Description = "Público. Responde JSON com status geral (Healthy, Degraded, Unhealthy) e, para cada check, nome, status, descrição e duração.",
                Tags = new HashSet<OpenApiTagReference> { new("Health checks", document) },
                Security = [],
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse { Description = "Healthy ou Degraded" },
                    ["503"] = new OpenApiResponse { Description = "Unhealthy (ex.: Oracle fora do ar)" }
                }
            };

            document.Paths[rota] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = operacao }
            };
        }
    }
}

/// <summary>Documenta o cabeçalho <c>Idempotency-Key</c> nos endpoints marcados.</summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IdempotenciaMetadata>()
            .FirstOrDefault();

        if (metadata is null) return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotenciaFilter.Cabecalho,
            In = ParameterLocation.Header,
            Required = metadata.Obrigatoria,
            Description = metadata.Obrigatoria
                ? "Identificador único da operação (ex.: UUID). Reenvie o mesmo valor ao repetir a mesma operação."
                : "Opcional. Se enviado, repetições com a mesma chave não gravam de novo.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 100 }
        });
    }
}
