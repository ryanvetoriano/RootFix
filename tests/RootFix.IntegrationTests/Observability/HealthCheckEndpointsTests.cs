using System.Net;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Observability;

/// <summary>Fábrica com a API Java configurada num endereço onde nada responde.</summary>
public sealed class JavaForaDoArFactory : RootFixApiFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfiguracoesExtras => new Dictionary<string, string?>
    {
        // Porta 1 em localhost: conexão recusada na hora, sem depender de rede externa.
        ["HealthChecks:ExternalServices:0:Name"] = "api-java",
        ["HealthChecks:ExternalServices:0:Url"] = "http://127.0.0.1:1/actuator/health",
        ["HealthChecks:ExternalServices:0:TimeoutSeconds"] = "2",
        ["HealthChecks:ExternalServices:0:Optional"] = "true"
    };
}

public sealed class JavaForaDoArFixture : IAsyncLifetime
{
    public JavaForaDoArFactory Factory { get; } = new();

    public Task InitializeAsync() => Factory.InicializarBancoAsync();

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>Requisito 05/06 do PDF: health checks da API, do banco e dos serviços externos.</summary>
[Collection(ApiCollection.Name)]
public class HealthCheckEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public HealthCheckEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Health_ComBancoDisponivel_RetornaHealthyComOsChecks()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        Assert.Equal("Healthy", corpo.GetProperty("status").GetString());

        var nomes = corpo.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("api", nomes);
        Assert.Contains("oracle", nomes);
    }

    [Fact]
    public async Task HealthLive_NaoConsultaDependencias()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        var check = Assert.Single(corpo.GetProperty("checks").EnumerateArray());
        Assert.Equal("api", check.GetProperty("name").GetString());
    }

    [Fact]
    public async Task HealthReady_SemToken_ResponderPorqueEhPublico()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class ServicoExternoHealthCheckTests : IClassFixture<JavaForaDoArFixture>
{
    private readonly JavaForaDoArFactory _factory;

    public ServicoExternoHealthCheckTests(JavaForaDoArFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Health_ApiJavaForaDoAr_DegradaSemDerrubar()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        // Degraded responde 200: a API continua servindo o painel mesmo com o Java dormindo.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        Assert.Equal("Degraded", corpo.GetProperty("status").GetString());

        var java = corpo.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "api-java");
        Assert.Equal("Degraded", java.GetProperty("status").GetString());
    }
}
