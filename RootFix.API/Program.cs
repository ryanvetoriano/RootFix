using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using RootFix.API.Endpoints;
using RootFix.API.Errors;
using RootFix.API.Extensions;
using RootFix.API.Middleware;
using RootFix.Application;
using RootFix.Application.Common;
using RootFix.Infrastructure;
using Serilog;

// Logger de bootstrap: cobre o intervalo entre o início do processo e a construção do host.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // O Render informa a porta pela variável PORT.
    var porta = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(porta))
        builder.WebHost.UseUrls($"http://0.0.0.0:{porta}");

    builder.Host.AddSerilogLogging();

    // ---------- Fuso horário ----------
    // Combinado do grupo: datas no horário de Brasília nas duas APIs (o Java com TZ=America/Sao_Paulo).
    // Registrado antes de AddApplication, que só adiciona o relógio padrão se não houver outro.
    var fuso = RelogioDoRootFix.ObterFuso(builder.Configuration["FusoHorario"]);
    builder.Services.AddSingleton<TimeProvider>(new RelogioDoRootFix(fuso));

    // ---------- JSON ----------
    // Enums trafegam como texto (EM_ANDAMENTO), igual à API Java. Números não são aceitos:
    // "status": 2 seria ambíguo para quem lê o contrato. Datas saem com o fuso explícito (-03:00).
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.SerializerOptions.Converters.Add(new DataHoraComFusoConverter(fuso));
    });
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.JsonSerializerOptions.Converters.Add(new DataHoraComFusoConverter(fuso));
    });

    // Erros de binding (JSON malformado, enum inválido) viram exceção e passam pelo handler
    // global, que responde com ProblemDetails em vez de um 400 sem corpo.
    builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

    // ---------- Swagger / OpenAPI ----------
    builder.Services.AddSwaggerDocumentation();

    // ---------- Segurança ----------
    builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddRateLimitingPolicies(builder.Configuration);

    // O app é nativo (sem CORS), mas o Expo Web e o Swagger de outra origem precisam disto.
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Retry-After", CorrelationIdMiddleware.HeaderName, "Idempotent-Replayed")));

    // ---------- Erros ----------
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // ---------- Camadas ----------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ---------- Health checks ----------
    builder.Services.AddApplicationHealthChecks(builder.Configuration);

    // O Render termina o HTTPS no proxy e repassa HTTP: estes cabeçalhos dizem à API o
    // esquema e o IP originais (usado no rate limiting de requisições sem token).
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseCorrelationId();
    app.UseRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RootFix API .NET v1");
        options.RoutePrefix = string.Empty; // Swagger na raiz
        options.DocumentTitle = "RootFix — API .NET";
    });

    app.UseCors();
    app.UseAuthentication();
    // Entre autenticação e autorização: o limitador já conhece o usuário (partição por usuário)
    // e também barra quem martela os endpoints sem token ou com token inválido (partição por IP),
    // antes de a autorização devolver 401.
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapPainelEndpoints();
    app.MapAcompanhamentoEndpoints();
    app.MapApplicationHealthChecks();

    if (app.Environment.IsDevelopment())
        app.MapDevEndpoints();

    Log.Information("Iniciando a RootFix API .NET no ambiente {Environment}.", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "A RootFix API .NET foi encerrada de forma inesperada.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposta para a <c>WebApplicationFactory&lt;Program&gt;</c> dos testes de integração.</summary>
public partial class Program;
