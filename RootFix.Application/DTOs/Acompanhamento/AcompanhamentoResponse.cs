using RootFix.Application.DTOs.Painel;
using RootFix.Domain.Enums;

namespace RootFix.Application.DTOs.Acompanhamento;

/// <summary>
/// O que o morador vê ao acompanhar um problema: se foi adotado, em que status está e a linha
/// do tempo com os recados do gestor.
/// </summary>
public sealed record AcompanhamentoResponse(
    long ProblemaId,
    string Titulo,
    long ComunidadeId,
    string ComunidadeNome,
    bool Adotado,
    DateTime? AdotadoEm,
    StatusProblema? Status,
    Prioridade? Prioridade,
    string Situacao,
    IReadOnlyList<HistoricoStatusResponse> Historico);
