using Moq;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Services;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.UnitTests.Common;

namespace RootFix.UnitTests.Application;

public class AcompanhamentoServiceTests
{
    private const long ProblemaId = 100;
    private const long Forasteiro = 99;

    private readonly Mock<IProblemaRepository> _problemas = new(MockBehavior.Strict);
    private readonly Mock<IComunidadeRepository> _comunidades = new(MockBehavior.Strict);
    private readonly AcompanhamentoService _service;

    public AcompanhamentoServiceTests()
    {
        _service = new AcompanhamentoService(_problemas.Object, _comunidades.Object);

        _comunidades
            .Setup(x => x.ObterPorIdAsync(TestData.ComunidadeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Comunidade());
        _comunidades
            .Setup(x => x.EhMembroAsync(TestData.ComunidadeId, TestData.MoradorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comunidades
            .Setup(x => x.EhMembroAsync(TestData.ComunidadeId, Forasteiro, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task ObterAsync_MembroDaComunidade_DevolveStatusEHistoricoComRecados()
    {
        // Arrange
        _problemas
            .Setup(x => x.ObterResumoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Resumo(ProblemaId, StatusProblema.EM_ANDAMENTO));
        _problemas
            .Setup(x => x.ListarHistoricoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                HistoricoStatus.Reconstituir(1, ProblemaId, StatusProblema.PENDENTE, StatusProblema.EM_ANDAMENTO,
                    TestData.Agora, TestData.GestorId, "Equipe agendada para 20/11")
            ]);

        // Act
        var acompanhamento = await _service.ObterAsync(TestData.MoradorId, ProblemaId);

        // Assert
        Assert.True(acompanhamento.Adotado);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, acompanhamento.Status);
        Assert.Contains("andamento", acompanhamento.Situacao);
        var evento = Assert.Single(acompanhamento.Historico);
        Assert.Equal("Equipe agendada para 20/11", evento.Observacao);
    }

    [Fact]
    public async Task ObterAsync_ProblemaNaoAdotado_InformaQueAguardaAdocaoSemConsultarHistorico()
    {
        // Arrange
        _problemas
            .Setup(x => x.ObterResumoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Resumo(ProblemaId, status: null, adotado: false));

        // Act
        var acompanhamento = await _service.ObterAsync(TestData.MoradorId, ProblemaId);

        // Assert
        Assert.False(acompanhamento.Adotado);
        Assert.Null(acompanhamento.Status);
        Assert.Empty(acompanhamento.Historico);
        Assert.Contains("Aguardando", acompanhamento.Situacao);
        _problemas.Verify(x => x.ListarHistoricoAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObterAsync_GestorDaComunidade_PodeAcompanharSemSerMembro()
    {
        // Arrange
        _problemas
            .Setup(x => x.ObterResumoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Resumo(ProblemaId));
        _problemas
            .Setup(x => x.ListarHistoricoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var acompanhamento = await _service.ObterAsync(TestData.GestorId, ProblemaId);

        // Assert
        Assert.Equal(ProblemaId, acompanhamento.ProblemaId);
    }

    [Fact]
    public async Task ObterAsync_UsuarioDeForaDaComunidade_LancaAcessoNegado()
    {
        // Arrange
        _problemas
            .Setup(x => x.ObterResumoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Resumo(ProblemaId));

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.ObterAsync(Forasteiro, ProblemaId));

        // Assert
        Assert.IsType<AcessoNegadoException>(excecao);
    }

    [Fact]
    public async Task ObterAsync_ProblemaInexistente_LancaNotFound()
    {
        // Arrange
        _problemas
            .Setup(x => x.ObterResumoAsync(ProblemaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RootFix.Application.Common.ProblemaResumo?)null);

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.ObterAsync(TestData.MoradorId, ProblemaId));

        // Assert
        Assert.IsType<NotFoundException>(excecao);
    }

    [Theory]
    [InlineData(null, "Aguardando")]
    [InlineData(StatusProblema.PENDENTE, "fila")]
    [InlineData(StatusProblema.RESOLVIDO, "Resolvido")]
    [InlineData(StatusProblema.ARQUIVADO, "Arquivado")]
    public void DescreverSituacao_TraduzStatusParaOMorador(StatusProblema? status, string trecho)
    {
        // Act
        var frase = AcompanhamentoService.DescreverSituacao(status);

        // Assert
        Assert.Contains(trecho, frase);
    }
}
