using RootFix.Domain.Enums;
using RootFix.Domain.Rules;

namespace RootFix.UnitTests.Domain;

public class TransicaoDeStatusTests
{
    [Theory]
    [InlineData(StatusProblema.PENDENTE, StatusProblema.EM_ANDAMENTO)]
    [InlineData(StatusProblema.PENDENTE, StatusProblema.ARQUIVADO)]
    [InlineData(StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO)]
    [InlineData(StatusProblema.EM_ANDAMENTO, StatusProblema.ARQUIVADO)]
    [InlineData(StatusProblema.EM_ANDAMENTO, StatusProblema.PENDENTE)]
    public void EhPermitida_TransicaoDoFluxo_RetornaTrue(StatusProblema atual, StatusProblema novo)
    {
        // Act
        var permitida = TransicaoDeStatus.EhPermitida(atual, novo);

        // Assert
        Assert.True(permitida);
    }

    [Theory]
    [InlineData(StatusProblema.PENDENTE, StatusProblema.RESOLVIDO)]
    [InlineData(StatusProblema.PENDENTE, StatusProblema.PENDENTE)]
    [InlineData(StatusProblema.RESOLVIDO, StatusProblema.EM_ANDAMENTO)]
    [InlineData(StatusProblema.RESOLVIDO, StatusProblema.PENDENTE)]
    [InlineData(StatusProblema.RESOLVIDO, StatusProblema.ARQUIVADO)]
    [InlineData(StatusProblema.ARQUIVADO, StatusProblema.PENDENTE)]
    [InlineData(StatusProblema.ARQUIVADO, StatusProblema.EM_ANDAMENTO)]
    [InlineData(StatusProblema.ARQUIVADO, StatusProblema.RESOLVIDO)]
    public void EhPermitida_TransicaoForaDoFluxo_RetornaFalse(StatusProblema atual, StatusProblema novo)
    {
        // Act
        var permitida = TransicaoDeStatus.EhPermitida(atual, novo);

        // Assert
        Assert.False(permitida);
    }

    [Theory]
    [InlineData(StatusProblema.RESOLVIDO, true)]
    [InlineData(StatusProblema.ARQUIVADO, true)]
    [InlineData(StatusProblema.PENDENTE, false)]
    [InlineData(StatusProblema.EM_ANDAMENTO, false)]
    public void EhFinal_IdentificaStatusSemSaida(StatusProblema status, bool esperado)
    {
        // Act
        var final = TransicaoDeStatus.EhFinal(status);

        // Assert
        Assert.Equal(esperado, final);
    }

    [Fact]
    public void ProximosPossiveis_APartirDePendente_ListaEmAndamentoEArquivado()
    {
        // Act
        var proximos = TransicaoDeStatus.ProximosPossiveis(StatusProblema.PENDENTE);

        // Assert
        Assert.Equal([StatusProblema.EM_ANDAMENTO, StatusProblema.ARQUIVADO], proximos);
    }
}
