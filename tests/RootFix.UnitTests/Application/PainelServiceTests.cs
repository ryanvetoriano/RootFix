using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using RootFix.Application.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Mensageria;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Services;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.UnitTests.Common;

namespace RootFix.UnitTests.Application;

public class PainelServiceTests
{
    private readonly Mock<IProblemaRepository> _problemas = new(MockBehavior.Strict);
    private readonly Mock<IComunidadeRepository> _comunidades = new(MockBehavior.Strict);
    private readonly Mock<IPublicadorDeEventos> _publicador = new(MockBehavior.Strict);
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(TestData.Agora));
    private readonly PainelService _service;

    public PainelServiceTests()
    {
        _service = new PainelService(
            _problemas.Object,
            _comunidades.Object,
            _publicador.Object,
            _relogio,
            NullLogger<PainelService>.Instance);
    }

    private void ConfigurarProblema(Problema problema)
    {
        _problemas.Setup(x => x.ObterPorIdAsync(problema.Id, It.IsAny<CancellationToken>())).ReturnsAsync(problema);
        _comunidades
            .Setup(x => x.ObterPorIdAsync(problema.ComunidadeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Comunidade(problema.ComunidadeId));
    }

    [Fact]
    public async Task AlterarStatusAsync_GestorDaComunidade_GravaHistoricoEPublicaEvento()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);

        HistoricoStatus? gravado = null;
        _problemas
            .Setup(x => x.SalvarAlteracaoDeStatusAsync(problema, It.IsAny<HistoricoStatus>(), It.IsAny<CancellationToken>()))
            .Callback<Problema, HistoricoStatus, CancellationToken>((_, h, _) => gravado = h)
            .Returns(Task.CompletedTask);

        ProblemaStatusAlteradoEvento? publicado = null;
        _publicador
            .Setup(x => x.PublicarStatusAlteradoAsync(It.IsAny<ProblemaStatusAlteradoEvento>(), It.IsAny<CancellationToken>()))
            .Callback<ProblemaStatusAlteradoEvento, CancellationToken>((e, _) => publicado = e)
            .Returns(Task.CompletedTask);

        var request = new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO, "Equipe a caminho");

        // Act
        var resposta = await _service.AlterarStatusAsync(TestData.GestorId, problema.Id, request);

        // Assert
        Assert.NotNull(gravado);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, gravado.StatusNovo);
        Assert.Equal(TestData.Agora, gravado.DataHora);

        Assert.Equal(StatusProblema.PENDENTE, resposta.StatusAnterior);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, resposta.StatusNovo);
        Assert.Equal("Equipe a caminho", resposta.Observacao);
        Assert.Contains(StatusProblema.RESOLVIDO, resposta.ProximosStatusPossiveis);

        Assert.NotNull(publicado);
        // Contrato combinado com o Java: só problemaId, statusAnterior, statusNovo e observacao.
        Assert.Equal(
            new ProblemaStatusAlteradoEvento(problema.Id, StatusProblema.PENDENTE, StatusProblema.EM_ANDAMENTO, "Equipe a caminho"),
            publicado);
    }

    [Fact]
    public async Task AlterarStatusAsync_GestorDeOutraComunidade_LancaAcessoNegadoENaoGrava()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.AlterarStatusAsync(
            TestData.OutroGestorId, problema.Id, new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO)));

        // Assert
        Assert.IsType<AcessoNegadoException>(excecao);
        Assert.Equal(StatusProblema.PENDENTE, problema.Status);
        _problemas.Verify(
            x => x.SalvarAlteracaoDeStatusAsync(It.IsAny<Problema>(), It.IsAny<HistoricoStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AlterarStatusAsync_ProblemaInexistente_LancaNotFoundException()
    {
        // Arrange
        _problemas.Setup(x => x.ObterPorIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Problema?)null);

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.AlterarStatusAsync(
            TestData.GestorId, 999, new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO)));

        // Assert
        Assert.IsType<NotFoundException>(excecao);
    }

    [Fact]
    public async Task AlterarStatusAsync_TransicaoProibida_LancaDomainExceptionENaoPublica()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.PENDENTE);
        ConfigurarProblema(problema);

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.AlterarStatusAsync(
            TestData.GestorId, problema.Id, new AlterarStatusRequest(StatusProblema.RESOLVIDO)));

        // Assert
        Assert.IsType<DomainException>(excecao);
        _publicador.Verify(
            x => x.PublicarStatusAlteradoAsync(It.IsAny<ProblemaStatusAlteradoEvento>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AlterarStatusAsync_FilaForaDoAr_MantemAlteracaoGravadaERetornaSucesso()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);

        _problemas
            .Setup(x => x.SalvarAlteracaoDeStatusAsync(problema, It.IsAny<HistoricoStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publicador
            .Setup(x => x.PublicarStatusAlteradoAsync(It.IsAny<ProblemaStatusAlteradoEvento>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ indisponível"));

        // Act
        var resposta = await _service.AlterarStatusAsync(
            TestData.GestorId, problema.Id, new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO));

        // Assert
        // O Oracle é a fonte da verdade: a falha da fila não desfaz a mudança de status.
        Assert.Equal(StatusProblema.EM_ANDAMENTO, resposta.StatusNovo);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, problema.Status);
    }

    [Fact]
    public async Task AlterarStatusAsync_ClienteDesconectaDepoisDoCommit_AindaRetornaSucesso()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);
        using var requisicaoCancelada = new CancellationTokenSource();

        _problemas
            .Setup(x => x.SalvarAlteracaoDeStatusAsync(problema, It.IsAny<HistoricoStatus>(), It.IsAny<CancellationToken>()))
            .Callback(() => requisicaoCancelada.Cancel()) // o app caiu logo após o commit
            .Returns(Task.CompletedTask);

        CancellationToken tokenDaPublicacao = default;
        _publicador
            .Setup(x => x.PublicarStatusAlteradoAsync(It.IsAny<ProblemaStatusAlteradoEvento>(), It.IsAny<CancellationToken>()))
            .Callback<ProblemaStatusAlteradoEvento, CancellationToken>((_, ct) => tokenDaPublicacao = ct)
            .Returns(Task.CompletedTask);

        // Act
        var resposta = await _service.AlterarStatusAsync(
            TestData.GestorId, problema.Id, new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO), requisicaoCancelada.Token);

        // Assert
        // A publicação não herda o cancelamento da requisição: a mudança já está gravada.
        Assert.Equal(StatusProblema.EM_ANDAMENTO, resposta.StatusNovo);
        Assert.False(tokenDaPublicacao.IsCancellationRequested);
    }

    [Fact]
    public async Task AlterarStatusAsync_PublicacaoCancelada_NaoPropagaOCancelamento()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);

        _problemas
            .Setup(x => x.SalvarAlteracaoDeStatusAsync(problema, It.IsAny<HistoricoStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publicador
            .Setup(x => x.PublicarStatusAlteradoAsync(It.IsAny<ProblemaStatusAlteradoEvento>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var excecao = await Record.ExceptionAsync(() => _service.AlterarStatusAsync(
            TestData.GestorId, problema.Id, new AlterarStatusRequest(StatusProblema.EM_ANDAMENTO)));

        // Assert
        Assert.Null(excecao);
    }

    [Fact]
    public async Task DefinirPrioridadeAsync_ProblemaEmAberto_GravaEDevolveAnterior()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        ConfigurarProblema(problema);
        _problemas.Setup(x => x.SalvarAsync(problema, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var resposta = await _service.DefinirPrioridadeAsync(
            TestData.GestorId, problema.Id, new DefinirPrioridadeRequest(Prioridade.URGENTE));

        // Assert
        Assert.Null(resposta.PrioridadeAnterior);
        Assert.Equal(Prioridade.URGENTE, resposta.Prioridade);
        _problemas.Verify(x => x.SalvarAsync(problema, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuscarSupervisionadosAsync_ComunidadeInexistente_LancaNotFoundException()
    {
        // Arrange
        _comunidades.Setup(x => x.ObterPorIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync((Comunidade?)null);

        // Act
        var excecao = await Record.ExceptionAsync(() =>
            _service.BuscarSupervisionadosAsync(TestData.GestorId, 77, new SupervisionadosFiltro()));

        // Assert
        Assert.IsType<NotFoundException>(excecao);
    }

    [Fact]
    public async Task BuscarSupervisionadosAsync_GestorDaComunidade_MapeiaPaginaComProximosStatus()
    {
        // Arrange
        var filtro = new SupervisionadosFiltro { Pagina = 1, TamanhoPagina = 2 };
        _comunidades
            .Setup(x => x.ObterPorIdAsync(TestData.ComunidadeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.Comunidade());
        _problemas
            .Setup(x => x.BuscarSupervisionadosAsync(TestData.ComunidadeId, filtro, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProblemaResumo>(
                [TestData.Resumo(1, StatusProblema.PENDENTE, apoios: 4), TestData.Resumo(2, null)], 3));

        // Act
        var pagina = await _service.BuscarSupervisionadosAsync(TestData.GestorId, TestData.ComunidadeId, filtro);

        // Assert
        Assert.Equal(3, pagina.TotalItens);
        Assert.Equal(2, pagina.TotalPaginas);
        Assert.True(pagina.TemProximaPagina);
        Assert.Equal(4, pagina.Itens[0].TotalApoios);
        // Adotado sem status gravado aparece como PENDENTE.
        Assert.Equal(StatusProblema.PENDENTE, pagina.Itens[1].Status);
        Assert.Equal([StatusProblema.EM_ANDAMENTO, StatusProblema.ARQUIVADO], pagina.Itens[1].ProximosStatusPossiveis);
    }

    [Fact]
    public async Task ListarComunidadesAsync_ContaAdotadoSemStatusComoPendente()
    {
        // Arrange
        _comunidades
            .Setup(x => x.ListarDoGestorAsync(TestData.GestorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([TestData.Comunidade()]);
        _problemas
            .Setup(x => x.ContarSupervisionadosPorStatusAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.Single() == TestData.ComunidadeId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ContagemPorStatus(TestData.ComunidadeId, StatusProblema.PENDENTE, 2),
                new ContagemPorStatus(TestData.ComunidadeId, null, 1),
                new ContagemPorStatus(TestData.ComunidadeId, StatusProblema.RESOLVIDO, 4)
            ]);

        // Act
        var comunidades = await _service.ListarComunidadesAsync(TestData.GestorId);

        // Assert
        var comunidade = Assert.Single(comunidades);
        Assert.Equal(7, comunidade.TotalSupervisionados);
        Assert.Equal(3, comunidade.Pendentes);
        Assert.Equal(4, comunidade.Resolvidos);
        Assert.Equal(0, comunidade.EmAndamento);
    }

    [Fact]
    public async Task ListarComunidadesAsync_GestorSemComunidades_NaoConsultaProblemas()
    {
        // Arrange
        _comunidades
            .Setup(x => x.ListarDoGestorAsync(TestData.GestorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var comunidades = await _service.ListarComunidadesAsync(TestData.GestorId);

        // Assert
        Assert.Empty(comunidades);
    }
}
