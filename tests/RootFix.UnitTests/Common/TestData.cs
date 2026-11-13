using RootFix.Application.Common;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;

namespace RootFix.UnitTests.Common;

/// <summary>Cenários reutilizados pelos testes unitários.</summary>
public static class TestData
{
    public static readonly DateTime Agora = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    public const long GestorId = 1;
    public const long OutroGestorId = 2;
    public const long MoradorId = 3;
    public const long ComunidadeId = 10;

    public static Comunidade Comunidade(long id = ComunidadeId, long gestorId = GestorId)
        => new(id, $"Comunidade {id}", gestorId);

    public static Problema ProblemaNaoAdotado(long id = 100, long comunidadeId = ComunidadeId)
        => new(id, comunidadeId, MoradorId, "Buraco na rua", Agora.AddDays(-3));

    public static Problema ProblemaAdotado(
        long id = 100,
        StatusProblema status = StatusProblema.PENDENTE,
        long comunidadeId = ComunidadeId)
    {
        var problema = ProblemaNaoAdotado(id, comunidadeId);
        problema.MarcarComoAdotado(Agora.AddDays(-2));

        // Leva o problema até o status pedido pelos caminhos permitidos.
        if (status is StatusProblema.EM_ANDAMENTO or StatusProblema.RESOLVIDO)
            problema.AlterarStatus(StatusProblema.EM_ANDAMENTO, GestorId, Agora.AddDays(-1));

        if (status == StatusProblema.RESOLVIDO)
            problema.AlterarStatus(StatusProblema.RESOLVIDO, GestorId, Agora.AddHours(-1));

        if (status == StatusProblema.ARQUIVADO)
            problema.AlterarStatus(StatusProblema.ARQUIVADO, GestorId, Agora.AddHours(-1));

        return problema;
    }

    public static ProblemaResumo Resumo(
        long id,
        StatusProblema? status = StatusProblema.PENDENTE,
        int apoios = 0,
        bool adotado = true,
        Prioridade? prioridade = null,
        DateTime? adotadoEm = null,
        long comunidadeId = ComunidadeId)
        => new(
            id,
            comunidadeId,
            $"Problema {id}",
            null,
            adotado,
            status,
            prioridade,
            null,
            Agora.AddDays(-5),
            adotado ? adotadoEm ?? Agora.AddDays(-4) : null,
            "Morador",
            apoios,
            null,
            null);
}
