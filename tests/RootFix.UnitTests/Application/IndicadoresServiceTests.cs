using Moq;
using RootFix.Application.Common;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Services;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.UnitTests.Common;

namespace RootFix.UnitTests.Application;

public class IndicadoresServiceTests
{
    private readonly Mock<IProblemaRepository> _problemas = new(MockBehavior.Strict);
    private readonly Mock<IComunidadeRepository> _comunidades = new(MockBehavior.Strict);
    private readonly IndicadoresService _service;

    public IndicadoresServiceTests()
    {
        _service = new IndicadoresService(_problemas.Object, _comunidades.Object);

        _comunidades
            .Setup(x => x.ObterPorIdAsync(TestData.ComunidadeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Comunidade());
    }

    private void Configurar(IReadOnlyList<ProblemaResumo> supervisionados, IReadOnlyList<HistoricoStatus> resolucoes)
    {
        _problemas
            .Setup(x => x.ListarSupervisionadosAsync(TestData.ComunidadeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supervisionados);
        _problemas
            .Setup(x => x.ListarHistoricoDaComunidadeAsync(
                TestData.ComunidadeId, StatusProblema.RESOLVIDO, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolucoes);
    }

    [Fact]
    public async Task CalcularAsync_ComunidadeComProblemas_ContaStatusPrioridadeETaxa()
    {
        // Arrange
        Configurar(
        [
            TestData.Resumo(1, StatusProblema.PENDENTE, apoios: 2, prioridade: Prioridade.URGENTE),
            TestData.Resumo(2, null, apoios: 9),
            TestData.Resumo(3, StatusProblema.EM_ANDAMENTO, apoios: 5, prioridade: Prioridade.ALTA),
            TestData.Resumo(4, StatusProblema.RESOLVIDO, apoios: 50, prioridade: Prioridade.ALTA),
            TestData.Resumo(5, StatusProblema.ARQUIVADO, apoios: 1)
        ], []);

        // Act
        var indicadores = await _service.CalcularAsync(TestData.GestorId, TestData.ComunidadeId);

        // Assert
        Assert.Equal(5, indicadores.TotalSupervisionados);
        Assert.Equal(2, indicadores.PorStatus.Pendentes);
        Assert.Equal(1, indicadores.PorStatus.EmAndamento);
        Assert.Equal(1, indicadores.PorStatus.Resolvidos);
        Assert.Equal(1, indicadores.PorStatus.Arquivados);
        Assert.Equal(1, indicadores.PorPrioridade.Urgente);
        Assert.Equal(2, indicadores.PorPrioridade.Alta);
        Assert.Equal(2, indicadores.PorPrioridade.SemPrioridade);
        Assert.Equal(20.0, indicadores.TaxaResolucaoPercentual);
        Assert.Equal(16, indicadores.TotalApoiosEmAberto);
    }

    [Fact]
    public async Task CalcularAsync_MaisApoiados_IgnoraResolvidosEArquivados()
    {
        // Arrange
        Configurar(
        [
            TestData.Resumo(1, StatusProblema.RESOLVIDO, apoios: 100),
            TestData.Resumo(2, StatusProblema.ARQUIVADO, apoios: 90),
            TestData.Resumo(3, StatusProblema.PENDENTE, apoios: 3),
            TestData.Resumo(4, StatusProblema.EM_ANDAMENTO, apoios: 7)
        ], []);

        // Act
        var indicadores = await _service.CalcularAsync(TestData.GestorId, TestData.ComunidadeId);

        // Assert
        Assert.Equal([4L, 3L], indicadores.MaisApoiadosEmAberto.Select(p => p.Id));
    }

    [Fact]
    public async Task CalcularAsync_ProblemasResolvidos_CalculaTempoMedioDesdeAAdocao()
    {
        // Arrange
        var adocao = TestData.Agora.AddDays(-10);
        Configurar(
        [
            TestData.Resumo(1, StatusProblema.RESOLVIDO, adotadoEm: adocao),
            TestData.Resumo(2, StatusProblema.RESOLVIDO, adotadoEm: adocao)
        ],
        [
            HistoricoStatus.Reconstituir(1, 1, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, adocao.AddHours(10)),
            HistoricoStatus.Reconstituir(2, 2, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, adocao.AddHours(30))
        ]);

        // Act
        var indicadores = await _service.CalcularAsync(TestData.GestorId, TestData.ComunidadeId);

        // Assert
        Assert.Equal(20.0, indicadores.TempoMedioResolucaoHoras);
        Assert.Equal(100.0, indicadores.TaxaResolucaoPercentual);
    }

    [Fact]
    public async Task CalcularAsync_ComunidadeSemSupervisionados_RetornaZerosSemDividirPorZero()
    {
        // Arrange
        Configurar([], []);

        // Act
        var indicadores = await _service.CalcularAsync(TestData.GestorId, TestData.ComunidadeId);

        // Assert
        Assert.Equal(0, indicadores.TotalSupervisionados);
        Assert.Equal(0, indicadores.TaxaResolucaoPercentual);
        Assert.Null(indicadores.TempoMedioResolucaoHoras);
        Assert.Empty(indicadores.MaisApoiadosEmAberto);
    }

    [Fact]
    public async Task CalcularAsync_GestorDeOutraComunidade_LancaAcessoNegado()
    {
        // Act
        var excecao = await Record.ExceptionAsync(() =>
            _service.CalcularAsync(TestData.OutroGestorId, TestData.ComunidadeId));

        // Assert
        Assert.IsType<AcessoNegadoException>(excecao);
    }

    [Fact]
    public void TempoMedioResolucaoEmHoras_UsaAUltimaResolucaoDeCadaProblema()
    {
        // Arrange
        var adocao = TestData.Agora.AddDays(-3);
        var supervisionados = new[] { TestData.Resumo(1, StatusProblema.RESOLVIDO, adotadoEm: adocao) };
        var resolucoes = new[]
        {
            HistoricoStatus.Reconstituir(1, 1, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, adocao.AddHours(5)),
            HistoricoStatus.Reconstituir(2, 1, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, adocao.AddHours(8))
        };

        // Act
        var media = IndicadoresService.TempoMedioResolucaoEmHoras(supervisionados, resolucoes);

        // Assert
        Assert.Equal(8.0, media);
    }
}
