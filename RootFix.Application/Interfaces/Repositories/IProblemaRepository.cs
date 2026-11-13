using RootFix.Application.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;

namespace RootFix.Application.Interfaces.Repositories;

public interface IProblemaRepository
{
    /// <summary>Problema com a comunidade carregada, rastreado para alteração.</summary>
    Task<Problema?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Página de problemas adotados de uma comunidade, com autor e total de apoios.</summary>
    Task<PagedResult<ProblemaResumo>> BuscarSupervisionadosAsync(
        long comunidadeId,
        SupervisionadosFiltro filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os problemas adotados de uma comunidade (usado nos indicadores).</summary>
    Task<IReadOnlyList<ProblemaResumo>> ListarSupervisionadosAsync(
        long comunidadeId,
        CancellationToken cancellationToken = default);

    /// <summary>Resumo de um único problema (autor e apoios incluídos).</summary>
    Task<ProblemaResumo?> ObterResumoAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Quantidade de adotados por comunidade e status.</summary>
    Task<IReadOnlyList<ContagemPorStatus>> ContarSupervisionadosPorStatusAsync(
        IReadOnlyCollection<long> comunidadeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Linha do tempo de um problema, do mais antigo para o mais recente.</summary>
    Task<IReadOnlyList<HistoricoStatus>> ListarHistoricoAsync(
        long problemaId,
        CancellationToken cancellationToken = default);

    /// <summary>Registros de histórico de uma comunidade que levaram ao status informado.</summary>
    Task<IReadOnlyList<HistoricoStatus>> ListarHistoricoDaComunidadeAsync(
        long comunidadeId,
        StatusProblema statusNovo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a mudança de status e o histórico na mesma transação.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ConflitoException">
    /// O status foi alterado por outra requisição entre a leitura e a gravação.
    /// </exception>
    Task SalvarAlteracaoDeStatusAsync(
        Problema problema,
        HistoricoStatus historico,
        CancellationToken cancellationToken = default);

    Task SalvarAsync(Problema problema, CancellationToken cancellationToken = default);
}
