using RootFix.Application.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Interfaces.Services;
using RootFix.Domain.Enums;

namespace RootFix.Application.Services;

public sealed class IndicadoresService : IIndicadoresService
{
    public const int QuantidadeMaisApoiados = 5;

    private readonly IProblemaRepository _problemas;
    private readonly IComunidadeRepository _comunidades;

    public IndicadoresService(IProblemaRepository problemas, IComunidadeRepository comunidades)
    {
        _problemas = problemas;
        _comunidades = comunidades;
    }

    public async Task<IndicadoresResponse> CalcularAsync(
        long gestorId,
        long comunidadeId,
        CancellationToken cancellationToken = default)
    {
        var comunidade = await AutorizacaoComunidade.ObterComoGestorAsync(
            _comunidades, comunidadeId, gestorId, cancellationToken);

        var supervisionados = await _problemas.ListarSupervisionadosAsync(comunidadeId, cancellationToken);
        var resolucoes = await _problemas.ListarHistoricoDaComunidadeAsync(
            comunidadeId, StatusProblema.RESOLVIDO, cancellationToken);

        int Status(StatusProblema s) => supervisionados.Count(p => p.StatusAtual == s);
        int ComPrioridade(Prioridade? p) => supervisionados.Count(x => x.Prioridade == p);

        var resolvidos = Status(StatusProblema.RESOLVIDO);
        var total = supervisionados.Count;

        var emAberto = supervisionados.Where(EstaEmAberto).ToList();

        return new IndicadoresResponse(
            comunidade.Id,
            comunidade.Nome,
            total,
            new ContagemStatusResponse(
                Status(StatusProblema.PENDENTE),
                Status(StatusProblema.EM_ANDAMENTO),
                resolvidos,
                Status(StatusProblema.ARQUIVADO)),
            new ContagemPrioridadeResponse(
                ComPrioridade(Prioridade.URGENTE),
                ComPrioridade(Prioridade.ALTA),
                ComPrioridade(Prioridade.MEDIA),
                ComPrioridade(Prioridade.BAIXA),
                ComPrioridade(null)),
            TempoMedioResolucaoEmHoras(supervisionados, resolucoes),
            total == 0 ? 0 : Math.Round(resolvidos * 100.0 / total, 1),
            emAberto.Sum(p => p.TotalApoios),
            emAberto
                .OrderByDescending(p => p.TotalApoios)
                .ThenBy(p => p.AdotadoEm)
                .Take(QuantidadeMaisApoiados)
                .Select(ProblemaSupervisionadoResponse.De)
                .ToList());
    }

    private static bool EstaEmAberto(ProblemaResumo p)
        => p.StatusAtual is StatusProblema.PENDENTE or StatusProblema.EM_ANDAMENTO;

    /// <summary>
    /// Para cada problema hoje RESOLVIDO, mede da adoção até a última mudança para RESOLVIDO.
    /// </summary>
    internal static double? TempoMedioResolucaoEmHoras(
        IReadOnlyList<ProblemaResumo> supervisionados,
        IReadOnlyList<Domain.Entities.HistoricoStatus> resolucoes)
    {
        var ultimaResolucao = resolucoes
            .GroupBy(h => h.ProblemaId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.DataHora));

        var duracoes = supervisionados
            .Where(p => p.StatusAtual == StatusProblema.RESOLVIDO && p.AdotadoEm.HasValue)
            .Where(p => ultimaResolucao.ContainsKey(p.Id))
            .Select(p => (ultimaResolucao[p.Id] - p.AdotadoEm!.Value).TotalHours)
            .Where(horas => horas >= 0)
            .ToList();

        return duracoes.Count == 0 ? null : Math.Round(duracoes.Average(), 1);
    }
}
