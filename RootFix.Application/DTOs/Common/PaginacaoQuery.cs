using System.ComponentModel.DataAnnotations;

namespace RootFix.Application.DTOs.Common;

/// <summary>
/// Parâmetros de paginação e ordenação comuns às listagens. Cada recurso herda esta classe,
/// acrescenta os próprios filtros e declara os campos pelos quais aceita ser ordenado.
/// </summary>
/// <remarks>
/// A validação roda no filtro de validação dos endpoints: um parâmetro inválido devolve
/// <c>400</c> com <c>ValidationProblemDetails</c> antes de chegar ao serviço.
/// </remarks>
public abstract class PaginacaoQuery : IValidatableObject
{
    public const int TamanhoPaginaPadrao = 10;
    public const int TamanhoPaginaMaximo = 50;

    // Limite que mantém (Pagina - 1) * TamanhoPagina dentro de um int.
    public const int PaginaMaxima = 1_000_000;

    // Anuláveis de propósito: com [AsParameters], uma propriedade int não anulável vira
    // parâmetro obrigatório na query. Ausente = valor padrão (ver PaginaAtual/TamanhoPaginaAtual).

    /// <summary>Número da página, a partir de 1 (padrão 1).</summary>
    [Range(1, PaginaMaxima, ErrorMessage = "A página deve estar entre 1 e 1000000.")]
    public int? Pagina { get; set; }

    /// <summary>Quantidade de itens por página, de 1 a 50 (padrão 10).</summary>
    [Range(1, TamanhoPaginaMaximo, ErrorMessage = "O tamanho da página deve estar entre 1 e 50.")]
    public int? TamanhoPagina { get; set; }

    /// <summary>Página efetiva, com o padrão aplicado.</summary>
    public int PaginaAtual => Pagina ?? 1;

    /// <summary>Tamanho de página efetivo, com o padrão aplicado.</summary>
    public int TamanhoPaginaAtual => TamanhoPagina ?? TamanhoPaginaPadrao;

    /// <summary>Campo de ordenação. Os valores aceitos variam por recurso.</summary>
    public string? OrdenarPor { get; set; }

    /// <summary>Direção da ordenação: <c>asc</c> ou <c>desc</c>.</summary>
    public string? Direcao { get; set; }

    /// <summary>Campos aceitos em <see cref="OrdenarPor"/>, sem diferenciar maiúsculas.</summary>
    protected abstract IReadOnlyCollection<string> CamposOrdenacao { get; }

    /// <summary>Direção usada quando o cliente não informa <see cref="Direcao"/>.</summary>
    protected virtual bool DescendentePorPadrao => false;

    /// <summary>Quantidade de registros a pular para chegar à página solicitada.</summary>
    public int Deslocamento() => (PaginaAtual - 1) * TamanhoPaginaAtual;

    /// <summary>
    /// Campo de ordenação normalizado em minúsculas, ou <c>null</c> quando o cliente não
    /// informou nenhum — caso em que o repositório aplica a ordenação padrão do recurso.
    /// </summary>
    public string? CampoOrdenacao()
        => string.IsNullOrWhiteSpace(OrdenarPor)
            ? null
            : OrdenarPor.Trim().ToLowerInvariant();

    public bool OrdemDescendente()
        => string.IsNullOrWhiteSpace(Direcao)
            ? DescendentePorPadrao
            : Direcao.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var campo = CampoOrdenacao();
        if (campo != null &&
            !CamposOrdenacao.Contains(campo, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"Campo de ordenação inválido. Valores aceitos: {string.Join(", ", CamposOrdenacao)}.",
                [nameof(OrdenarPor)]);
        }

        if (!string.IsNullOrWhiteSpace(Direcao) &&
            !Direcao.Trim().Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !Direcao.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Direção de ordenação inválida. Valores aceitos: asc, desc.",
                [nameof(Direcao)]);
        }

        foreach (var erro in ValidarFiltros())
            yield return erro;
    }

    /// <summary>Validações dos filtros específicos de cada recurso.</summary>
    protected virtual IEnumerable<ValidationResult> ValidarFiltros() => [];
}
