using System.Net;
using System.Net.Http.Json;
using RootFix.Application.DTOs.Acompanhamento;
using RootFix.Domain.Enums;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>
/// A funcionalidade do .NET voltada ao cidadão: o morador acompanha o problema.
/// </summary>
[Collection(ApiCollection.Name)]
public class AcompanhamentoEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public AcompanhamentoEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Acompanhamento_MoradorMembro_VeStatusERecadoDoGestor()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var gestora = Tokens.Gestora(_factory);
        var mudanca = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/status")
        {
            Content = JsonContent.Create(
                new { status = "EM_ANDAMENTO", observacao = "Equipe agendada para 20/11" }, options: ApiJson.Options)
        };
        mudanca.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await gestora.SendAsync(mudanca);

        using var morador = Tokens.Morador(_factory);

        // Act
        var response = await morador.GetAsync($"/problemas/{problemaId}/acompanhamento");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var acompanhamento = await response.LerAsync<AcompanhamentoResponse>();
        Assert.True(acompanhamento.Adotado);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, acompanhamento.Status);
        Assert.Equal("Vila Esperança", acompanhamento.ComunidadeNome);
        Assert.Equal("Equipe agendada para 20/11", Assert.Single(acompanhamento.Historico).Observacao);
    }

    [Fact]
    public async Task Acompanhamento_ProblemaAindaNaoAdotado_InformaQueAguardaGestor()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, adotado: false, status: null);
        using var morador = Tokens.Morador(_factory);

        // Act
        var response = await morador.GetAsync($"/problemas/{problemaId}/acompanhamento");

        // Assert
        var acompanhamento = await response.LerAsync<AcompanhamentoResponse>();
        Assert.False(acompanhamento.Adotado);
        Assert.Null(acompanhamento.Status);
        Assert.Contains("Aguardando", acompanhamento.Situacao);
    }

    [Fact]
    public async Task Acompanhamento_MoradorDeForaDaComunidade_Retorna403()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var forasteira = Tokens.MoradorDeFora(_factory);

        // Act
        var response = await forasteira.GetAsync($"/problemas/{problemaId}/acompanhamento");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Acompanhamento_SemToken_Retorna401()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var anonimo = Tokens.Cliente(_factory);

        // Act
        var response = await anonimo.GetAsync($"/problemas/{problemaId}/acompanhamento");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Acompanhamento_ProblemaInexistente_Retorna404()
    {
        // Arrange
        using var morador = Tokens.Morador(_factory);

        // Act
        var response = await morador.GetAsync("/problemas/123456789/acompanhamento");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
