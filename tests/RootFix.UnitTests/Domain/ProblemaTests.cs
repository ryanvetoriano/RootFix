using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.UnitTests.Common;

namespace RootFix.UnitTests.Domain;

public class ProblemaTests
{
    [Fact]
    public void AlterarStatus_TransicaoPermitida_AtualizaStatusEDevolveHistorico()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.PENDENTE);

        // Act
        var historico = problema.AlterarStatus(
            StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora, "Equipe agendada para 20/11");

        // Assert
        Assert.Equal(StatusProblema.EM_ANDAMENTO, problema.Status);
        Assert.Equal(problema.Id, historico.ProblemaId);
        Assert.Equal(StatusProblema.PENDENTE, historico.StatusAnterior);
        Assert.Equal(StatusProblema.EM_ANDAMENTO, historico.StatusNovo);
        Assert.Equal(TestData.Agora, historico.DataHora);
        Assert.Equal(TestData.GestorId, historico.UsuarioId);
        Assert.Equal("Equipe agendada para 20/11", historico.Observacao);
    }

    [Fact]
    public void AlterarStatus_ProblemaNaoAdotado_LancaDomainException()
    {
        // Arrange
        var problema = TestData.ProblemaNaoAdotado();

        // Act
        var excecao = Record.Exception(() =>
            problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora));

        // Assert
        Assert.IsType<DomainException>(excecao);
        Assert.Null(problema.Status);
    }

    [Fact]
    public void AlterarStatus_MesmoStatus_LancaConflitoException()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.EM_ANDAMENTO);

        // Act
        var excecao = Record.Exception(() =>
            problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora));

        // Assert
        Assert.IsType<ConflitoException>(excecao);
    }

    [Fact]
    public void AlterarStatus_PularDePendenteParaResolvido_LancaDomainExceptionComOsPossiveis()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.PENDENTE);

        // Act
        var excecao = Record.Exception(() =>
            problema.AlterarStatus(StatusProblema.RESOLVIDO, TestData.GestorId, TestData.Agora));

        // Assert
        var domainException = Assert.IsType<DomainException>(excecao);
        Assert.Contains("EM_ANDAMENTO, ARQUIVADO", domainException.Message);
        Assert.Equal(StatusProblema.PENDENTE, problema.Status);
    }

    [Fact]
    public void AlterarStatus_ProblemaResolvido_LancaDomainExceptionPorqueEhFinal()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.RESOLVIDO);

        // Act
        var excecao = Record.Exception(() =>
            problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora));

        // Assert
        var domainException = Assert.IsType<DomainException>(excecao);
        Assert.Contains("status final", domainException.Message);
    }

    [Fact]
    public void AlterarStatus_ObservacaoComEspacos_GravaTextoSemEspacosNasPontas()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();

        // Act
        var historico = problema.AlterarStatus(
            StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora, "   Obra iniciada   ");

        // Assert
        Assert.Equal("Obra iniciada", historico.Observacao);
    }

    [Fact]
    public void AlterarStatus_ObservacaoEmBranco_GravaNula()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();

        // Act
        var historico = problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora, "   ");

        // Assert
        Assert.Null(historico.Observacao);
    }

    [Fact]
    public void AlterarStatus_ObservacaoLongaDemais_LancaDomainExceptionEMantemStatus()
    {
        // Arrange
        var problema = TestData.ProblemaAdotado();
        var observacao = new string('x', HistoricoStatus.TamanhoMaximoObservacao + 1);

        // Act
        var excecao = Record.Exception(() =>
            problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, TestData.GestorId, TestData.Agora, observacao));

        // Assert
        Assert.IsType<DomainException>(excecao);
        Assert.Equal(StatusProblema.PENDENTE, problema.Status);
    }

    [Fact]
    public void StatusAtual_ProblemaNaoAdotado_EhNulo()
    {
        // Arrange
        var problema = TestData.ProblemaNaoAdotado();

        // Act & Assert
        Assert.Null(problema.StatusAtual);
    }

    [Fact]
    public void MarcarComoAdotado_SemStatus_ComecaComoPendente()
    {
        // Arrange
        var problema = TestData.ProblemaNaoAdotado();

        // Act
        problema.MarcarComoAdotado(TestData.Agora);

        // Assert
        Assert.True(problema.Adotado);
        Assert.Equal(TestData.Agora, problema.AdotadoEm);
        Assert.Equal(StatusProblema.PENDENTE, problema.StatusAtual);
    }

    [Theory]
    [InlineData(Prioridade.URGENTE)]
    [InlineData(Prioridade.BAIXA)]
    public void DefinirPrioridade_ProblemaEmAberto_DefinePrioridade(Prioridade prioridade)
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: StatusProblema.EM_ANDAMENTO);

        // Act
        problema.DefinirPrioridade(prioridade);

        // Assert
        Assert.Equal(prioridade, problema.Prioridade);
    }

    [Fact]
    public void DefinirPrioridade_ProblemaNaoAdotado_LancaDomainException()
    {
        // Arrange
        var problema = TestData.ProblemaNaoAdotado();

        // Act
        var excecao = Record.Exception(() => problema.DefinirPrioridade(Prioridade.ALTA));

        // Assert
        Assert.IsType<DomainException>(excecao);
        Assert.Null(problema.Prioridade);
    }

    [Theory]
    [InlineData(StatusProblema.RESOLVIDO)]
    [InlineData(StatusProblema.ARQUIVADO)]
    public void DefinirPrioridade_ProblemaEncerrado_LancaDomainException(StatusProblema status)
    {
        // Arrange
        var problema = TestData.ProblemaAdotado(status: status);

        // Act
        var excecao = Record.Exception(() => problema.DefinirPrioridade(Prioridade.URGENTE));

        // Assert
        Assert.IsType<DomainException>(excecao);
    }

    [Fact]
    public void EhGerenciadaPor_SoOGestorDaComunidade()
    {
        // Arrange
        var comunidade = TestData.Comunidade(gestorId: TestData.GestorId);

        // Act & Assert
        Assert.True(comunidade.EhGerenciadaPor(TestData.GestorId));
        Assert.False(comunidade.EhGerenciadaPor(TestData.OutroGestorId));
    }
}
