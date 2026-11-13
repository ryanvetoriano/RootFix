using System.Net;
using System.Net.Http.Json;
using RootFix.Application.DTOs.Painel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RootFix.Infrastructure.Persistence;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>
/// Requisito 03 do PDF: requisições repetidas não geram operações duplicadas.
/// </summary>
[Collection(ApiCollection.Name)]
public class IdempotenciaEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public IdempotenciaEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private static HttpRequestMessage Patch(long problemaId, string status, string? chave, string? observacao = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/status")
        {
            Content = JsonContent.Create(new { status, observacao }, options: ApiJson.Options)
        };

        if (chave is not null)
            request.Headers.Add("Idempotency-Key", chave);

        return request;
    }

    [Fact]
    public async Task AlterarStatus_SemIdempotencyKey_Retorna400()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave: null));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
    }

    [Fact]
    public async Task AlterarStatus_MesmaChaveDuasVezes_ExecutaUmaVezERepeteAResposta()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();

        // Act
        var primeira = await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave, "Equipe a caminho"));
        var repetida = await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave, "Equipe a caminho"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        // Sem idempotência, a repetição daria 409 (mesmo status). Com ela, a resposta original volta.
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.True(repetida.Headers.TryGetValues("Idempotent-Replayed", out var valores));
        Assert.Equal("true", Assert.Single(valores));

        var corpo1 = await primeira.LerAsync<AlteracaoStatusResponse>();
        var corpo2 = await repetida.LerAsync<AlteracaoStatusResponse>();
        Assert.Equal(corpo1.HistoricoId, corpo2.HistoricoId);
        Assert.Equal(corpo1.DataHora, corpo2.DataHora);

        Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
        Assert.Single(_factory.Publicador.DoProblema(problemaId));
    }

    [Fact]
    public async Task AlterarStatus_Concluido_GravaRotaEStatusHttpNaRfIdempotencia()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();

        // Act
        await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave));

        // Assert
        using var scope = _factory.CriarEscopo();
        var context = scope.ServiceProvider.GetRequiredService<RootFixContext>();
        var registro = await context.RegistrosIdempotencia.AsNoTracking()
            .SingleAsync(r => r.Chave == chave);

        Assert.Equal(DadosDeTeste.GestoraId, registro.UsuarioId);
        Assert.Equal($"PATCH /painel/problemas/{problemaId}/status", registro.Rota);
        Assert.Equal(200, registro.StatusHttp); // 102 enquanto processava
        Assert.Equal(64, registro.ImpressaoDigital.Length);
        Assert.Contains("\"statusNovo\":\"EM_ANDAMENTO\"", registro.Resposta);
    }

    [Fact]
    public async Task AlterarStatus_TokenDeUsuarioQueNaoExisteNoBanco_Retorna403()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Cliente(_factory, Tokens.Para(_factory, 555_555, Domain.Enums.Perfil.GESTOR));

        // Act
        var response = await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", Guid.NewGuid().ToString()));

        // Assert
        // RF_IDEMPOTENCIA.ID_USUARIO é FK: sem a checagem, viraria erro de integridade (500).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AlterarStatus_MesmaChaveComOutroCorpo_Retorna422()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();
        await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave));

        // Act
        var response = await client.SendAsync(Patch(problemaId, "ARQUIVADO", chave));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
    }

    [Fact]
    public async Task AlterarStatus_MesmaChaveEmOutroProblema_Retorna422()
    {
        // Arrange
        var problemaA = await DadosDeTeste.CriarProblemaAsync(_factory);
        var problemaB = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();
        await client.SendAsync(Patch(problemaA, "EM_ANDAMENTO", chave));

        // Act
        var response = await client.SendAsync(Patch(problemaB, "EM_ANDAMENTO", chave));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(await DadosDeTeste.HistoricoAsync(_factory, problemaB));
    }

    [Fact]
    public async Task AlterarStatus_PrimeiraTentativaFalhou_MesmaChavePodeSerReutilizada()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();

        // Act
        // Tentativa inválida: falhas não são guardadas, a reserva é desfeita.
        var falha = await client.SendAsync(Patch(problemaId, "RESOLVIDO", chave));
        // Corrigida, com a mesma chave e o mesmo corpo da tentativa original não importa mais.
        var outraFalha = await client.SendAsync(Patch(problemaId, "RESOLVIDO", chave));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, falha.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, outraFalha.StatusCode);
        Assert.False(outraFalha.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task AlterarStatus_MesmaChaveUsadaPorOutroGestor_NaoColide()
    {
        // Arrange
        var problemaDaGestora = await DadosDeTeste.CriarProblemaAsync(_factory);
        var problemaDoOutro = await DadosDeTeste.CriarProblemaAsync(
            _factory, comunidadeId: DadosDeTeste.ComunidadeDoOutroGestorId);
        using var gestora = Tokens.Gestora(_factory);
        using var outro = Tokens.OutroGestor(_factory);
        var chave = "chave-compartilhada-" + Guid.NewGuid();

        // Act
        var r1 = await gestora.SendAsync(Patch(problemaDaGestora, "EM_ANDAMENTO", chave));
        var r2 = await outro.SendAsync(Patch(problemaDoOutro, "EM_ANDAMENTO", chave));

        // Assert
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.False(r2.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task AlterarStatus_ChaveMaiorQue100Caracteres_Retorna400()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", new string('k', 101)));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AlterarStatus_RequisicoesSimultaneasComAMesmaChave_GravamUmaVezSo()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();

        // Act
        var respostas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => client.SendAsync(Patch(problemaId, "EM_ANDAMENTO", chave))));

        // Assert
        // Cada uma recebe a resposta original (200), "em andamento" (409) ou, se chegou depois de
        // gravado, a repetição (200). Nunca dois registros no histórico.
        foreach (var r in respostas)
        {
            if (r.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
                Assert.Fail($"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        }
        Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId));
        Assert.Single(_factory.Publicador.DoProblema(problemaId));
    }

    [Fact]
    public async Task DefinirPrioridade_ComChaveRepetida_RepeteRespostaComPrioridadeAnteriorOriginal()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var chave = Guid.NewGuid().ToString();

        HttpRequestMessage Requisicao()
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/prioridade")
            {
                Content = JsonContent.Create(new { prioridade = "ALTA" }, options: ApiJson.Options)
            };
            request.Headers.Add("Idempotency-Key", chave);
            return request;
        }

        // Act
        var primeira = await client.SendAsync(Requisicao());
        var repetida = await client.SendAsync(Requisicao());

        // Assert
        var corpo = await repetida.LerAsync<PrioridadeResponse>();
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Null(corpo.PrioridadeAnterior); // a resposta da 1ª vez, não "ALTA → ALTA"
        Assert.True(repetida.Headers.Contains("Idempotent-Replayed"));
    }
}
