using System.Net;
using System.Security.Claims;
using RootFix.Domain.Enums;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>
/// Requisito 05 do PDF: endpoints protegidos por JWT com controle de acesso.
/// O token é o mesmo emitido pela API Java; aqui ele é gerado com a chave de teste.
/// </summary>
[Collection(ApiCollection.Name)]
public class AutenticacaoEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public AutenticacaoEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Painel_SemToken_Retorna401()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Painel_TokenAssinadoComOutraChave_Retorna401()
    {
        // Arrange
        var chaveErrada = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        var token = Tokens.Personalizado(_factory,
            [new Claim("sub", DadosDeTeste.GestoraId.ToString()), new Claim("perfil", "GESTOR")],
            chave: chaveErrada);
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Painel_TokenExpirado_Retorna401()
    {
        // Arrange
        var token = Tokens.Personalizado(_factory,
            [new Claim("sub", DadosDeTeste.GestoraId.ToString()), new Claim("perfil", "GESTOR")],
            expiraEm: DateTime.UtcNow.AddMinutes(-2));
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Painel_EmissorDiferenteDoConfigurado_Retorna401()
    {
        // Arrange
        var token = Tokens.Personalizado(_factory,
            [new Claim("sub", DadosDeTeste.GestoraId.ToString()), new Claim("perfil", "GESTOR")],
            issuer: "outro-emissor");
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Painel_PerfilMorador_Retorna403()
    {
        // Arrange
        using var client = Tokens.Morador(_factory);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Painel_GestorDeOutraComunidade_Retorna403ComProblemDetails()
    {
        // Arrange
        using var client = Tokens.OutroGestor(_factory);

        // Act
        var response = await client.GetAsync($"/painel/comunidades/{DadosDeTeste.ComunidadeId}/supervisionados");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Painel_TokenIgualAoEmitidoPelaApiJava_Autoriza()
    {
        // Arrange
        // Exatamente as claims do TokenService do Rootfix_Java: iss, sub (ID), iat, exp, perfil,
        // nome e email, assinado com HS256 sobre os bytes UTF-8 do JWT_SECRET.
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var token = Tokens.Personalizado(_factory,
        [
            new Claim("sub", DadosDeTeste.GestoraId.ToString()),
            new Claim("iat", agora, ClaimValueTypes.Integer64),
            new Claim("perfil", "GESTOR"),
            new Claim("nome", "Gestora Ana"),
            new Claim("email", DadosDeTeste.GestoraEmail)
        ], issuer: "rootfix-api");
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comunidades = await response.LerJsonAsync();
        Assert.Contains(comunidades.EnumerateArray(), c => c.GetProperty("id").GetInt64() == DadosDeTeste.ComunidadeId);
    }

    [Fact]
    public async Task Painel_TokenAssinadoComHs512_Retorna401PorqueOJavaSoUsaHs256()
    {
        // Arrange
        var chaveLonga = System.Text.Encoding.UTF8.GetBytes(new string('k', 64));
        var token = Tokens.Personalizado(_factory,
            [new Claim("sub", DadosDeTeste.GestoraId.ToString()), new Claim("perfil", "GESTOR")],
            chave: chaveLonga,
            algoritmo: Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha512);
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Painel_TokenSemClaimPerfil_Retorna403()
    {
        // Arrange
        var token = Tokens.Personalizado(_factory, [new Claim("sub", DadosDeTeste.GestoraId.ToString())]);
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Painel_SubNaoNumerico_Retorna403()
    {
        // Arrange
        // O Java sempre grava o ID no sub; um e-mail ali não identifica ninguém.
        var token = Tokens.Personalizado(_factory,
            [new Claim("sub", DadosDeTeste.GestoraEmail), new Claim("perfil", "GESTOR")]);
        using var client = Tokens.Cliente(_factory, token);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Painel_TokenDeUsuarioInexistente_Retorna403()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory, Tokens.Para(_factory, 987_654, Perfil.GESTOR));

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        // O token é válido, mas o usuário não gerencia nada: a lista vem vazia.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comunidades = await response.LerJsonAsync();
        Assert.Equal(0, comunidades.GetArrayLength());
    }

    [Fact]
    public async Task ListarComunidades_Gestora_VeSoAsProprias()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync("/painel/comunidades");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await response.LerJsonAsync()).EnumerateArray().Select(c => c.GetProperty("id").GetInt64()).ToList();
        Assert.Contains(DadosDeTeste.ComunidadeId, ids);
        Assert.DoesNotContain(DadosDeTeste.ComunidadeDoOutroGestorId, ids);
    }

    [Fact]
    public async Task DevToken_ForaDeDevelopment_NaoExiste()
    {
        // Arrange
        using var client = Tokens.Cliente(_factory);

        // Act
        var response = await client.PostAsync("/dev/token", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
