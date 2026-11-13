namespace RootFix.Domain.Enums;

/// <summary>
/// Prioridade definida pelo gestor para um problema supervisionado.
/// </summary>
/// <remarks>
/// Gravada como texto na coluna <c>PRIORIDADE</c>. A ordem numérica vai da mais urgente para
/// a menos urgente e é usada na ordenação do painel.
/// </remarks>
public enum Prioridade
{
    URGENTE = 1,
    ALTA = 2,
    MEDIA = 3,
    BAIXA = 4
}
