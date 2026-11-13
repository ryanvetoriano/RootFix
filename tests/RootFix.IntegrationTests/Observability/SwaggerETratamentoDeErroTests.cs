using System.Net;
using System.Text.Json;
using RootFix.API.Auth;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Observability;

/// <summary>Requisito 02 do PDF: documentação OpenAPI/Swagger com parâmetros, contratos e respostas.</summary>
[Collection(ApiCollection.Name)]
public class SwaggerETratamentoDeErroTests
{
    private readonly RootFixApiFactory _factory;

    public SwaggerETratamentoDeErroTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private async Task<JsonElement> SwaggerAsync()
    {
        using var client = Tokens.Cliente(_factory);
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.LerJsonAsync();
    }

    [Fact]
    public async Task Swagger_PatchDeStatus_DocumentaIdempotencyKeyObrigatoriaERespostas()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();
        var patch = doc.GetProperty("paths").GetProperty("/painel/problemas/{problemaId}/status").GetProperty("patch");

        // Assert
        var cabecalho = patch.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.Equal("header", cabecalho.GetProperty("in").GetString());
        Assert.True(cabecalho.GetProperty("required").GetBoolean());

        Assert.True(patch.TryGetProperty("requestBody", out _));

        var respostas = patch.GetProperty("responses").EnumerateObject().Select(r => r.Name).ToList();
        foreach (var codigo in new[] { "200", "400", "401", "403", "404", "409", "422", "429" })
            Assert.Contains(codigo, respostas);
    }

    [Fact]
    public async Task Swagger_PatchDePrioridade_IdempotencyKeyOpcional()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();
        var patch = doc.GetProperty("paths").GetProperty("/painel/problemas/{problemaId}/prioridade").GetProperty("patch");

        // Assert
        var cabecalho = patch.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.False(cabecalho.TryGetProperty("required", out var obrigatoria) && obrigatoria.GetBoolean());
    }

    [Fact]
    public async Task Swagger_EnumsDocumentadosComoTexto()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();
        var status = doc.GetProperty("components").GetProperty("schemas").GetProperty("StatusProblema");

        // Assert
        var valores = status.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.Equal(["PENDENTE", "EM_ANDAMENTO", "RESOLVIDO", "ARQUIVADO"], valores);
    }

    [Fact]
    public async Task Swagger_SupervisionadosDocumentaFiltrosDePaginacao()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();
        var get = doc.GetProperty("paths").GetProperty("/painel/comunidades/{comunidadeId}/supervisionados").GetProperty("get");

        // Assert
        var nomes = get.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        foreach (var parametro in new[] { "comunidadeId", "status", "prioridade", "pagina", "tamanhoPagina", "ordenarPor", "direcao" })
            Assert.Contains(parametro, nomes);
    }

    [Fact]
    public async Task Swagger_HealthChecksDocumentadosEPublicos()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();
        var paths = doc.GetProperty("paths");

        // Assert
        foreach (var rota in new[] { "/health", "/health/live", "/health/ready" })
        {
            var get = paths.GetProperty(rota).GetProperty("get");
            Assert.Equal(0, get.GetProperty("security").GetArrayLength());
        }
    }

    [Fact]
    public async Task Swagger_EsquemaBearerDeclarado()
    {
        // Arrange & Act
        var doc = await SwaggerAsync();

        // Assert
        var bearer = doc.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task SwaggerUi_NaRaiz()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/index.html");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Erro_TrazCorrelationIdNoCabecalhoENoCorpo()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "teste-correlacao-123");

        // Act
        var response = await client.GetAsync("/painel/comunidades/987654/supervisionados");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("teste-correlacao-123", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        var corpo = await response.LerJsonAsync();
        Assert.Equal("teste-correlacao-123", corpo.GetProperty("correlationId").GetString());
    }
}

/// <summary>Leitura do perfil nos formatos que o Spring Security costuma gerar.</summary>
public class PerfilDoTokenTests
{
    [Theory]
    [InlineData("GESTOR", "GESTOR")]
    [InlineData("ROLE_GESTOR", "GESTOR")]
    [InlineData("role_gestor", "GESTOR")]
    [InlineData("""["ROLE_MORADOR"]""", "MORADOR")]
    [InlineData("""{"authority":"ROLE_GESTOR"}""", "GESTOR")]
    [InlineData("""[{"authority":"ROLE_GESTOR"}]""", "GESTOR")]
    [InlineData("openid GESTOR", "GESTOR")]
    public void Extrair_FormatosComuns_NormalizaParaOPerfil(string valor, string esperado)
    {
        // Act
        var papeis = PerfilDoToken.Extrair(valor).ToList();

        // Assert
        Assert.Contains(esperado, papeis);
    }

    [Fact]
    public void Extrair_JsonInvalido_NaoLanca()
    {
        // Act
        var papeis = PerfilDoToken.Extrair("{quebrado").ToList();

        // Assert
        Assert.Empty(papeis);
    }
}
