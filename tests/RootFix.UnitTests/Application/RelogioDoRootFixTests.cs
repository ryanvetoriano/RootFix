using RootFix.Application.Common;

namespace RootFix.UnitTests.Application;

public class RelogioDoRootFixTests
{
    [Fact]
    public void ObterFuso_Brasilia_TemOffsetDeMenosTresHorasSemHorarioDeVerao()
    {
        // Arrange
        var fuso = RelogioDoRootFix.ObterFuso("America/Sao_Paulo");

        // Act
        var janeiro = fuso.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        var julho = fuso.GetUtcOffset(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(TimeSpan.FromHours(-3), janeiro);
        Assert.Equal(TimeSpan.FromHours(-3), julho);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ObterFuso_SemConfiguracao_UsaBrasilia(string? id)
    {
        // Act
        var fuso = RelogioDoRootFix.ObterFuso(id);

        // Assert
        Assert.Equal(TimeSpan.FromHours(-3), fuso.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public void ObterFuso_IdInexistente_CaiParaUtcMenosTres()
    {
        // Act
        var fuso = RelogioDoRootFix.ObterFuso("Fuso/Que_Nao_Existe");

        // Assert
        Assert.Equal(TimeSpan.FromHours(-3), fuso.BaseUtcOffset);
    }

    [Fact]
    public void GetLocalNow_MaquinaEmQualquerFuso_DevolveHoraDeBrasilia()
    {
        // Arrange
        var relogio = new RelogioDoRootFix(RelogioDoRootFix.ObterFuso(null));

        // Act
        var agora = relogio.GetLocalNow();

        // Assert
        Assert.Equal(TimeSpan.FromHours(-3), agora.Offset);
        Assert.InRange(
            (agora.DateTime - DateTime.UtcNow.AddHours(-3)).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1));
    }
}
