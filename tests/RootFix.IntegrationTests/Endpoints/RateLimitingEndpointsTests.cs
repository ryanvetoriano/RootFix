using System.Net;
using System.Net.Http.Json;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>Fábrica com limites baixos, para chegar ao 429 com poucas requisições.</summary>
public sealed class RateLimitBaixoFactory : RootFixApiFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfiguracoesExtras => new Dictionary<string, string?>
    {
        ["RateLimiting:LeituraPorMinuto"] = "3",
        ["RateLimiting:EscritaCapacidade"] = "2",
        ["RateLimiting:EscritaReposicaoPorMinuto"] = "1"
    };
}

public sealed class RateLimitFixture : IAsyncLifetime
{
    public RateLimitBaixoFactory Factory { get; } = new();

    public Task InitializeAsync() => Factory.InicializarBancoAsync();

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Requisito 04 do PDF: limitação de requisições com o middleware nativo do ASP.NET Core e
/// tratamento do HTTP 429.
/// </summary>
public class RateLimitingEndpointsTests : IClassFixture<RateLimitFixture>
{
    private readonly RateLimitBaixoFactory _factory;

    public RateLimitingEndpointsTests(RateLimitFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Leitura_AcimaDoLimite_Retorna429ComRetryAfterEProblemDetails()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);
        var url = $"/painel/comunidades/{DadosDeTeste.ComunidadeId}/indicadores";

        // Act
        var dentroDoLimite = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
            dentroDoLimite.Add(await client.GetAsync(url));

        var excedente = await client.GetAsync(url);

        // Assert
        Assert.All(dentroDoLimite, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        Assert.True(excedente.Headers.RetryAfter is not null || excedente.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", excedente.Content.Headers.ContentType?.MediaType);

        var corpo = await excedente.LerJsonAsync();
        Assert.Equal(429, corpo.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Leitura_LimiteEhPorUsuario_OutroGestorNaoEhAfetado()
    {
        // Arrange
        // O teste anterior (ou este) esgota a cota da gestora; o outro gestor tem a própria.
        using var gestora = Tokens.Gestora(_factory);
        for (var i = 0; i < 4; i++)
            await gestora.GetAsync("/painel/comunidades");

        using var outro = Tokens.OutroGestor(_factory);

        // Act
        var response = await outro.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Escrita_RajadaAcimaDaCapacidade_Retorna429()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId: DadosDeTeste.ComunidadeDoOutroGestorId);
        using var client = Tokens.OutroGestor(_factory);

        HttpRequestMessage Requisicao(string prioridade) => new(HttpMethod.Patch, $"/painel/problemas/{problemaId}/prioridade")
        {
            Content = JsonContent.Create(new { prioridade }, options: ApiJson.Options)
        };

        // Act
        var r1 = await client.SendAsync(Requisicao("ALTA"));
        var r2 = await client.SendAsync(Requisicao("URGENTE"));
        var r3 = await client.SendAsync(Requisicao("BAIXA"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }

    [Fact]
    public async Task SemToken_AcimaDoLimite_Retorna429PeloIpEmVezDe401Infinito()
    {
        // Arrange
        using var anonimo = Tokens.Cliente(_factory);

        // Act
        var respostas = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            respostas.Add((await anonimo.GetAsync("/problemas/1/acompanhamento")).StatusCode);

        // Assert
        // As primeiras são 401 (sem token); depois do limite, o IP é barrado antes da autorização.
        Assert.Equal(HttpStatusCode.Unauthorized, respostas[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, respostas[^1]);
    }

    [Fact]
    public async Task HealthChecks_NaoSaoLimitados()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var respostas = new List<HttpResponseMessage>();
        for (var i = 0; i < 6; i++)
            respostas.Add(await client.GetAsync("/health/live"));

        // Assert
        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }
}
