using RootFix.Application.Common;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Rules;

namespace RootFix.Application.DTOs.Painel;

/// <summary>Problema na lista de supervisionados do painel.</summary>
/// <param name="Categoria">Nome da categoria (RF_CATEGORIA), quando classificada.</param>
/// <param name="GravidadeIa">Gravidade sugerida pelo Spring AI: BAIXA, MEDIA, ALTA ou CRITICA.</param>
/// <param name="JustificativaIa">Por que a IA sugeriu essa gravidade.</param>
public sealed record ProblemaSupervisionadoResponse(
    long Id,
    long ComunidadeId,
    string Titulo,
    string? Descricao,
    StatusProblema? Status,
    Prioridade? Prioridade,
    string? Categoria,
    string? GravidadeIa,
    string? JustificativaIa,
    string AutorNome,
    int TotalApoios,
    DateTime CriadoEm,
    DateTime? AdotadoEm,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<StatusProblema> ProximosStatusPossiveis)
{
    public static ProblemaSupervisionadoResponse De(ProblemaResumo p)
        => new(
            p.Id,
            p.ComunidadeId,
            p.Titulo,
            p.Descricao,
            p.StatusAtual,
            p.Prioridade,
            p.Categoria,
            p.GravidadeIa,
            p.JustificativaIa,
            p.AutorNome,
            p.TotalApoios,
            p.CriadoEm,
            p.AdotadoEm,
            p.Latitude,
            p.Longitude,
            p.StatusAtual is { } status ? TransicaoDeStatus.ProximosPossiveis(status) : []);
}

/// <summary>Uma mudança de status na linha do tempo do problema.</summary>
public sealed record HistoricoStatusResponse(
    long Id,
    StatusProblema? StatusAnterior,
    StatusProblema StatusNovo,
    DateTime DataHora,
    string? Observacao)
{
    public static HistoricoStatusResponse De(HistoricoStatus h)
        => new(h.Id, h.StatusAnterior, h.StatusNovo, h.DataHora, h.Observacao);
}

/// <summary>Detalhe de um problema no painel, com a linha do tempo completa.</summary>
public sealed record ProblemaDetalheResponse(
    ProblemaSupervisionadoResponse Problema,
    string ComunidadeNome,
    IReadOnlyList<HistoricoStatusResponse> Historico);

/// <summary>Resultado de uma mudança de status.</summary>
public sealed record AlteracaoStatusResponse(
    long ProblemaId,
    StatusProblema? StatusAnterior,
    StatusProblema StatusNovo,
    DateTime DataHora,
    string? Observacao,
    long HistoricoId,
    IReadOnlyList<StatusProblema> ProximosStatusPossiveis);

/// <summary>Resultado da definição de prioridade.</summary>
public sealed record PrioridadeResponse(
    long ProblemaId,
    Prioridade? PrioridadeAnterior,
    Prioridade Prioridade);

/// <summary>Comunidade gerenciada pelo gestor, com o resumo do painel.</summary>
public sealed record ComunidadePainelResponse(
    long Id,
    string Nome,
    string? Descricao,
    int TotalSupervisionados,
    int Pendentes,
    int EmAndamento,
    int Resolvidos,
    int Arquivados);

/// <summary>Indicadores de uma comunidade, calculados sobre os problemas supervisionados.</summary>
/// <param name="TempoMedioResolucaoHoras">
/// Média, em horas, entre a adoção e a mudança para RESOLVIDO. <c>null</c> se nada foi resolvido.
/// </param>
/// <param name="TaxaResolucaoPercentual">Resolvidos sobre o total supervisionado, de 0 a 100.</param>
/// <param name="MaisApoiadosEmAberto">Até 5 problemas ainda abertos com mais apoios.</param>
public sealed record IndicadoresResponse(
    long ComunidadeId,
    string ComunidadeNome,
    int TotalSupervisionados,
    ContagemStatusResponse PorStatus,
    ContagemPrioridadeResponse PorPrioridade,
    double? TempoMedioResolucaoHoras,
    double TaxaResolucaoPercentual,
    int TotalApoiosEmAberto,
    IReadOnlyList<ProblemaSupervisionadoResponse> MaisApoiadosEmAberto);

public sealed record ContagemStatusResponse(int Pendentes, int EmAndamento, int Resolvidos, int Arquivados);

public sealed record ContagemPrioridadeResponse(int Urgente, int Alta, int Media, int Baixa, int SemPrioridade);
