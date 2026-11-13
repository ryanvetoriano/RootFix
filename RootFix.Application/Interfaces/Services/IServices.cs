using RootFix.Application.DTOs.Acompanhamento;
using RootFix.Application.DTOs.Common;
using RootFix.Application.DTOs.Painel;

namespace RootFix.Application.Interfaces.Services;

/// <summary>Operações do painel do gestor sobre os problemas adotados.</summary>
public interface IPainelService
{
    Task<IReadOnlyList<ComunidadePainelResponse>> ListarComunidadesAsync(
        long gestorId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ProblemaSupervisionadoResponse>> BuscarSupervisionadosAsync(
        long gestorId,
        long comunidadeId,
        SupervisionadosFiltro filtro,
        CancellationToken cancellationToken = default);

    Task<ProblemaDetalheResponse> ObterDetalheAsync(
        long gestorId,
        long problemaId,
        CancellationToken cancellationToken = default);

    Task<AlteracaoStatusResponse> AlterarStatusAsync(
        long gestorId,
        long problemaId,
        AlterarStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<PrioridadeResponse> DefinirPrioridadeAsync(
        long gestorId,
        long problemaId,
        DefinirPrioridadeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Indicadores de desempenho de uma comunidade.</summary>
public interface IIndicadoresService
{
    Task<IndicadoresResponse> CalcularAsync(
        long gestorId,
        long comunidadeId,
        CancellationToken cancellationToken = default);
}

/// <summary>Acompanhamento de um problema pelo morador.</summary>
public interface IAcompanhamentoService
{
    Task<AcompanhamentoResponse> ObterAsync(
        long usuarioId,
        long problemaId,
        CancellationToken cancellationToken = default);
}
