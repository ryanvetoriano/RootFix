using System.ComponentModel.DataAnnotations;
using RootFix.Application.DTOs.Common;
using RootFix.Domain.Enums;

namespace RootFix.Application.DTOs.Painel;

/// <summary>
/// Filtros, paginação e ordenação de
/// <c>GET /painel/comunidades/{comunidadeId}/supervisionados</c>.
/// Ordenação: <c>adotadoEm</c> (padrão, mais recentes primeiro), <c>apoios</c>,
/// <c>prioridade</c>, <c>gravidade</c> ou <c>criadoEm</c>.
/// </summary>
public sealed class SupervisionadosFiltro : PaginacaoQuery
{
    private static readonly string[] Campos = ["adotadoem", "apoios", "prioridade", "gravidade", "criadoem"];

    /// <summary>Somente problemas neste status (PENDENTE, EM_ANDAMENTO, RESOLVIDO, ARQUIVADO).</summary>
    public StatusProblema? Status { get; set; }

    /// <summary>Somente problemas com esta prioridade (URGENTE, ALTA, MEDIA, BAIXA).</summary>
    public Prioridade? Prioridade { get; set; }

    /// <summary>Quando <c>true</c>, só os problemas que ainda não receberam prioridade.</summary>
    public bool? SemPrioridade { get; set; }

    /// <summary>Parte do título, sem diferenciar maiúsculas.</summary>
    [MaxLength(100, ErrorMessage = "A busca por título aceita no máximo 100 caracteres.")]
    public string? Titulo { get; set; }

    protected override IReadOnlyCollection<string> CamposOrdenacao => Campos;

    protected override bool DescendentePorPadrao => true;

    protected override IEnumerable<ValidationResult> ValidarFiltros()
    {
        if (Status.HasValue && !Enum.IsDefined(Status.Value))
            yield return new ValidationResult("Status inválido.", [nameof(Status)]);

        if (Prioridade.HasValue && !Enum.IsDefined(Prioridade.Value))
            yield return new ValidationResult("Prioridade inválida.", [nameof(Prioridade)]);

        if (Prioridade.HasValue && SemPrioridade == true)
        {
            yield return new ValidationResult(
                "Use 'prioridade' ou 'semPrioridade=true', não os dois juntos.",
                [nameof(Prioridade), nameof(SemPrioridade)]);
        }
    }
}
