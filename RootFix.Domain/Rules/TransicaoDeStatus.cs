using RootFix.Domain.Enums;

namespace RootFix.Domain.Rules;

/// <summary>
/// Máquina de estados do status de um problema adotado.
/// </summary>
/// <remarks>
/// <code>
/// PENDENTE ──► EM_ANDAMENTO ──► RESOLVIDO
///    │  ▲            │
///    │  └────────────┤ (volta para a fila)
///    ▼               ▼
/// ARQUIVADO ◄────────┘
/// </code>
/// <list type="bullet">
///   <item>PENDENTE → EM_ANDAMENTO ou ARQUIVADO.</item>
///   <item>EM_ANDAMENTO → RESOLVIDO, ARQUIVADO ou de volta a PENDENTE (ex.: obra suspensa).</item>
///   <item>RESOLVIDO e ARQUIVADO são finais.</item>
/// </list>
/// A regra é a mesma do plano de ação (PENDENTE → EM_ANDAMENTO → RESOLVIDO, ou ARQUIVADO).
/// Ela vive no domínio para ser testada sem banco; se o Raul implementar a mesma validação no
/// procedimento PL/SQL, as duas precisam continuar iguais.
/// </remarks>
public static class TransicaoDeStatus
{
    private static readonly IReadOnlyDictionary<StatusProblema, StatusProblema[]> Permitidas =
        new Dictionary<StatusProblema, StatusProblema[]>
        {
            [StatusProblema.PENDENTE] = [StatusProblema.EM_ANDAMENTO, StatusProblema.ARQUIVADO],
            [StatusProblema.EM_ANDAMENTO] =
                [StatusProblema.RESOLVIDO, StatusProblema.ARQUIVADO, StatusProblema.PENDENTE],
            [StatusProblema.RESOLVIDO] = [],
            [StatusProblema.ARQUIVADO] = []
        };

    public static bool EhPermitida(StatusProblema atual, StatusProblema novo)
        => Permitidas.TryGetValue(atual, out var destinos) && destinos.Contains(novo);

    public static bool EhFinal(StatusProblema status)
        => Permitidas.TryGetValue(status, out var destinos) && destinos.Length == 0;

    /// <summary>Status para os quais o problema pode ir a partir de <paramref name="atual"/>.</summary>
    public static IReadOnlyList<StatusProblema> ProximosPossiveis(StatusProblema atual)
        => Permitidas.TryGetValue(atual, out var destinos) ? destinos : [];
}
