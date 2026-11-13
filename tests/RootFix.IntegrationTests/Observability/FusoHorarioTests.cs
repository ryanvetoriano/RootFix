using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RootFix.API.Extensions;
using RootFix.Application.Common;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Observability;

/// <summary>
/// Combinado do grupo: datas no horário de Brasília nas duas APIs. O .NET grava a hora de
/// Brasília no Oracle e devolve as datas com o offset explícito (-03:00).
/// </summary>
[Collection(ApiCollection.Name)]
public class FusoHorarioTests
{
    private static readonly TimeZoneInfo Brasilia = RelogioDoRootFix.ObterFuso("America/Sao_Paulo");

    private readonly RootFixApiFactory _factory;

    public FusoHorarioTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task AlterarStatus_GravaHoraDeBrasiliaEDevolveComOffset()
    {
        // Arrange
        var problemaId = await DadosDeTeste.CriarProblemaAsync(_factory);
        using var client = Tokens.Gestora(_factory);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/painel/problemas/{problemaId}/status")
        {
            Content = JsonContent.Create(new { status = "EM_ANDAMENTO" }, options: ApiJson.Options)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var brasiliaAgora = DateTime.UtcNow.AddHours(-3);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var dataHora = JsonDocument.Parse(json).RootElement.GetProperty("dataHora").GetString();
        Assert.EndsWith("-03:00", dataHora);

        var gravado = Assert.Single(await DadosDeTeste.HistoricoAsync(_factory, problemaId)).DataHora;
        Assert.InRange((gravado - brasiliaAgora).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task DatasLidasDoBanco_SaemComOffsetDeBrasilia()
    {
        // Arrange
        // ADOTADO_EM é gravado pelo Java (hora de Brasília, sem fuso no TIMESTAMP).
        var adotadoEm = new DateTime(2026, 11, 10, 9, 30, 0);
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, adotadoEm: adotadoEm);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync($"/painel/comunidades/{comunidadeId}/supervisionados");

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        var item = JsonDocument.Parse(json).RootElement.GetProperty("itens")[0];
        Assert.Equal("2026-11-10T09:30:00-03:00", item.GetProperty("adotadoEm").GetString());
    }

    [Fact]
    public void Conversor_DataEmUtc_SaiConvertidaParaBrasilia()
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new DataHoraComFusoConverter(Brasilia) } };
        var utc = new DateTime(2026, 11, 13, 18, 0, 0, DateTimeKind.Utc);

        // Act
        var json = JsonSerializer.Serialize(utc, options);

        // Assert
        Assert.Equal("\"2026-11-13T15:00:00-03:00\"", json);
    }

    [Fact]
    public void Conversor_LeituraComOutroOffset_ConverteParaHoraDeBrasilia()
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new DataHoraComFusoConverter(Brasilia) } };

        // Act
        var data = JsonSerializer.Deserialize<DateTime>("\"2026-11-13T18:00:00Z\"", options);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 13, 15, 0, 0), data);
    }
}
