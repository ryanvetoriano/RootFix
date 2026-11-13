using System.ComponentModel.DataAnnotations;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;

namespace RootFix.Application.DTOs.Painel;

/// <summary>
/// Corpo de <c>PATCH /painel/problemas/{id}/status</c>.
/// </summary>
/// <param name="Status">Novo status: PENDENTE, EM_ANDAMENTO, RESOLVIDO ou ARQUIVADO.</param>
/// <param name="Observacao">
/// Recado opcional do gestor para os moradores (ex.: "Equipe agendada para 20/11").
/// Aparece no acompanhamento do problema. Máximo de 500 caracteres.
/// </param>
public sealed record AlterarStatusRequest(
    [property: Required(ErrorMessage = "Informe o novo status.")]
    [property: EnumDataType(typeof(StatusProblema), ErrorMessage = "Status inválido.")]
    StatusProblema? Status,

    [property: MaxLength(HistoricoStatus.TamanhoMaximoObservacao,
        ErrorMessage = "A observação deve ter no máximo 500 caracteres.")]
    string? Observacao = null);

/// <summary>
/// Corpo de <c>PATCH /painel/problemas/{id}/prioridade</c>.
/// </summary>
/// <param name="Prioridade">URGENTE, ALTA, MEDIA ou BAIXA.</param>
public sealed record DefinirPrioridadeRequest(
    [property: Required(ErrorMessage = "Informe a prioridade.")]
    [property: EnumDataType(typeof(Prioridade), ErrorMessage = "Prioridade inválida.")]
    Prioridade? Prioridade);
