using System.Net;
using RootFix.Application.DTOs.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Domain.Enums;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Endpoints;

/// <summary>
/// Lista de supervisionados e indicadores: filtros, ordenação, paginação e validação
/// (requisito 01 do PDF).
/// </summary>
[Collection(ApiCollection.Name)]
public class SupervisionadosEndpointsTests
{
    private readonly RootFixApiFactory _factory;

    public SupervisionadosEndpointsTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Supervisionados_SoTrazAdotadosDaComunidade()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        var adotado = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, adotado: false, status: null);
        await DadosDeTeste.CriarProblemaAsync(_factory); // outra comunidade
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync($"/painel/comunidades/{comunidadeId}/supervisionados");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await response.LerAsync<PagedResponse<ProblemaSupervisionadoResponse>>();
        Assert.Equal(adotado, Assert.Single(pagina.Itens).Id);
        Assert.Equal("Morador Carlos", pagina.Itens[0].AutorNome);
        Assert.Equal(DadosDeTeste.CategoriaBuraco, pagina.Itens[0].Categoria);
        Assert.Equal("ALTA", pagina.Itens[0].GravidadeIa);
        Assert.Equal("Buraco em via de tráfego com risco de acidente.", pagina.Itens[0].JustificativaIa);
    }

    [Fact]
    public async Task Supervisionados_FiltroPendente_IncluiAdotadoSemStatusGravado()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        var pendente = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, status: StatusProblema.PENDENTE);
        var semStatus = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, status: null);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, status: StatusProblema.EM_ANDAMENTO);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{comunidadeId}/supervisionados?status=PENDENTE");

        // Assert
        var pagina = await response.LerAsync<PagedResponse<ProblemaSupervisionadoResponse>>();
        Assert.Equal([pendente, semStatus], pagina.Itens.Select(p => p.Id).Order());
        Assert.All(pagina.Itens, p => Assert.Equal(StatusProblema.PENDENTE, p.Status));
    }

    [Fact]
    public async Task Supervisionados_OrdenadosPorApoios_DoMaisApoiadoParaOMenos()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        var poucos = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, apoios: 1);
        var muitos = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, apoios: 5);
        var medio = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, apoios: 3);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{comunidadeId}/supervisionados?ordenarPor=apoios&direcao=desc");

        // Assert
        var pagina = await response.LerAsync<PagedResponse<ProblemaSupervisionadoResponse>>();
        Assert.Equal([muitos, medio, poucos], pagina.Itens.Select(p => p.Id));
        Assert.Equal([5, 3, 1], pagina.Itens.Select(p => p.TotalApoios));
    }

    [Fact]
    public async Task Supervisionados_OrdenadosPorPrioridade_UrgenteAntesDeAlta()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        var baixa = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, prioridade: Prioridade.BAIXA);
        var urgente = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, prioridade: Prioridade.URGENTE);
        var alta = await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, prioridade: Prioridade.ALTA);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{comunidadeId}/supervisionados?ordenarPor=prioridade");

        // Assert
        var pagina = await response.LerAsync<PagedResponse<ProblemaSupervisionadoResponse>>();
        Assert.Equal([urgente, alta, baixa], pagina.Itens.Select(p => p.Id));
    }

    [Fact]
    public async Task Supervisionados_Paginacao_RespeitaTamanhoEInformaTotais()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        for (var i = 0; i < 5; i++)
            await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId);
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{comunidadeId}/supervisionados?pagina=2&tamanhoPagina=2");

        // Assert
        var pagina = await response.LerAsync<PagedResponse<ProblemaSupervisionadoResponse>>();
        Assert.Equal(2, pagina.Itens.Count);
        Assert.Equal(5, pagina.TotalItens);
        Assert.Equal(3, pagina.TotalPaginas);
        Assert.True(pagina.TemPaginaAnterior);
        Assert.True(pagina.TemProximaPagina);
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("tamanhoPagina=51")]
    [InlineData("ordenarPor=nome")]
    [InlineData("direcao=cima")]
    [InlineData("prioridade=ALTA&semPrioridade=true")]
    public async Task Supervisionados_ParametroInvalido_Retorna400ComErros(string query)
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{DadosDeTeste.ComunidadeId}/supervisionados?{query}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var corpo = await response.LerJsonAsync();
        Assert.True(corpo.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Supervisionados_StatusInexistenteNaQuery_Retorna400()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync(
            $"/painel/comunidades/{DadosDeTeste.ComunidadeId}/supervisionados?status=FEITO");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Supervisionados_ComunidadeInexistente_Retorna404()
    {
        // Arrange
        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync("/painel/comunidades/987654/supervisionados");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Indicadores_ComunidadeComProblemas_CalculaContagensTempoMedioEMaisApoiados()
    {
        // Arrange
        var comunidadeId = await DadosDeTeste.CriarComunidadeAsync(_factory);
        var adocao = DateTime.UtcNow.AddDays(-2);

        var resolvido = await DadosDeTeste.CriarProblemaAsync(
            _factory, comunidadeId, status: StatusProblema.RESOLVIDO, apoios: 10, adotadoEm: adocao);
        await DadosDeTeste.AdicionarHistoricoAsync(
            _factory, resolvido, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, adocao.AddHours(12));

        var maisApoiadoAberto = await DadosDeTeste.CriarProblemaAsync(
            _factory, comunidadeId, status: StatusProblema.EM_ANDAMENTO, apoios: 4, prioridade: Prioridade.URGENTE);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, status: StatusProblema.PENDENTE, apoios: 1);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, status: StatusProblema.ARQUIVADO);
        await DadosDeTeste.CriarProblemaAsync(_factory, comunidadeId, adotado: false, status: null, apoios: 99);

        using var client = Tokens.Gestora(_factory);

        // Act
        var response = await client.GetAsync($"/painel/comunidades/{comunidadeId}/indicadores");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var indicadores = await response.LerAsync<IndicadoresResponse>();
        Assert.Equal(4, indicadores.TotalSupervisionados);
        Assert.Equal(new ContagemStatusResponse(1, 1, 1, 1), indicadores.PorStatus);
        Assert.Equal(1, indicadores.PorPrioridade.Urgente);
        Assert.Equal(12.0, indicadores.TempoMedioResolucaoHoras);
        Assert.Equal(25.0, indicadores.TaxaResolucaoPercentual);
        Assert.Equal(5, indicadores.TotalApoiosEmAberto);
        Assert.Equal(maisApoiadoAberto, indicadores.MaisApoiadosEmAberto[0].Id);
    }

    [Fact]
    public async Task Indicadores_GestorDeOutraComunidade_Retorna403()
    {
        // Arrange
        using var client = Tokens.OutroGestor(_factory);

        // Act
        var response = await client.GetAsync($"/painel/comunidades/{DadosDeTeste.ComunidadeId}/indicadores");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
