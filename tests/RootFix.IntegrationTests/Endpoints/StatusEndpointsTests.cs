using System.Net;
using System.Net.Http.Json;
using RootFix.Application.DTOs.Painel;
using RootFix.Domain.Enums;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>
/// <c>PATCH /painel/problemas/{id}/status</c>: regras de negócio, códigos HTTP e idempotência
/// (requisitos 01 e 03 do PDF).
/// </summary>
[Collection(ApiCollection.Name)]
public class StatusEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public StatusEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private static HttpRequestMessage Patch(long problemaId, object corpo, string? chave = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/status")
        {
            Content = JsonContent.Create(corpo, options: ApiJson.Options)
        };

        request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        return request;
    }

    [Fact]
    public async Task AlterarStatus_TransicaoValida_Retorna200GravaHistoricoEPublicaEvento()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId,
            new { status = "EM_ANDAMENTO", observacao = "Equipe agendada para 20/11" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var corpo = await response.LerAsync<AlteracaoStatusResponse>();
        Assert.Equal(StatusProblema.PENDENTE, corpo.StatusAnterior);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, corpo.StatusNovo);
        Assert.True(corpo.HistoricoId > 0);
        Assert.Contains(StatusProblema.RESOLVIDO, corpo.ProximosStatusPossiveis);

        var problema = await DadosDeTeste.ProblemaAsync(_factory, problemaId);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, problema.Status);

        var historico = Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
        Assert.Equal("Equipe agendada para 20/11", historico.Observacao);
        Assert.Equal(DadosDeTeste.GestoraId, historico.UsuarioId);

        var evento = Assert.Single(_factory.Publicador.DoProblema(problemaId));
        Assert.Equal(StatusProblema.PENDENTE, evento.StatusAnterior);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, evento.StatusNovo);
        Assert.Equal("Equipe agendada para 20/11", evento.Observacao);
    }

    [Fact]
    public async Task AlterarStatus_AdotadoSemStatusGravado_TrataComoPendente()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, status: null);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var historico = Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
        Assert.Null(historico.StatusAnterior);
    }

    [Fact]
    public async Task AlterarStatus_PularDePendenteParaResolvido_Retorna422()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { status = "RESOLVIDO" }));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
        Assert.Empty(_factory.Publicador.DoProblema(problemaId));
    }

    [Fact]
    public async Task AlterarStatus_MesmoStatusAtual_Retorna409()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, status: StatusProblema.EM_ANDAMENTO);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AlterarStatus_ProblemaNaoAdotado_Retorna422()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, adotado: false, status: null);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AlterarStatus_ProblemaInexistente_Retorna404()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(999_999, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AlterarStatus_GestorDeOutraComunidade_Retorna403ENaoAltera()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.OutroGestor(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(StatusProblema.PENDENTE, (await DadosDeTeste.ProblemaAsync(_factory, problemaId)).Status);
    }

    [Theory]
    [InlineData("""{ "status": "FEITO" }""")]
    [InlineData("""{ "status": 2 }""")]
    [InlineData("""{ "status": """)]
    public async Task AlterarStatus_CorpoMalformadoOuEnumInvalido_Retorna400(string json)
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/status")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AlterarStatus_SemStatus_Retorna400ComErroDoCampo()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, new { observacao = "sem status" }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        Assert.True(corpo.GetProperty("errors").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task AlterarStatus_ObservacaoComMaisDe500Caracteres_Retorna400()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId,
            new { status = "EM_ANDAMENTO", observacao = new string('x', 501) }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        Assert.True(corpo.GetProperty("errors").TryGetProperty("observacao", out _));
    }

    [Fact]
    public async Task AlterarStatus_FluxoCompleto_ChegaEmResolvidoComHistoricoEmOrdem()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var r1 = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));
        var r2 = await client.SendAsync(Patch(problemaId, new { status = "RESOLVIDO", observacao = "Buraco tapado" }));
        var r3 = await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r3.StatusCode); // RESOLVIDO é final

        var historico = await DadosDeTeste.HistoricoAsync(_factory, problemaId);
        Assert.Equal(
            [StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO],
            historico.Select(h => h.StatusNovo));
    }

    [Fact]
    public async Task DefinirPrioridade_SemIdempotencyKey_Retorna200PorqueEhOpcional()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/painel/problemas/{problemaId}/prioridade", new { prioridade = "URGENTE" }, ApiJson.Options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var corpo = await response.LerAsync<PrioridadeResponse>();
        Assert.Equal(Prioridade.URGENTE, corpo.Prioridade);
        Assert.Equal(Prioridade.URGENTE, (await DadosDeTeste.ProblemaAsync(_factory, problemaId)).Prioridade);
    }

    [Fact]
    public async Task DefinirPrioridade_ProblemaResolvido_Retorna422()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, status: StatusProblema.RESOLVIDO);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/painel/problemas/{problemaId}/prioridade", new { prioridade = "ALTA" }, ApiJson.Options);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ObterDetalhe_ProblemaComHistorico_TrazLinhaDoTempo()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory, apoios: 2);
        using var client = Tokens.Gestora(_factory);
        await client.SendAsync(Patch(problemaId, new { status = "EM_ANDAMENTO", observacao = "Iniciado" }));

        // Act
        var response = await client.GetAsync($"/painel/problemas/{problemaId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detalhe = await response.LerAsync<ProblemaDetalheResponse>();
        Assert.Equal(2, detalhe.Problema.TotalApoios);
        Assert.Equal("Vila Esperança", detalhe.ComunidadeNome);
        Assert.Equal("Iniciado", Assert.Single(detalhe.Historico).Observacao);
    }
}
