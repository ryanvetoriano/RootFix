using RootFix.Domain.Enums;

namespace RootFix.Application.Common;

/// <summary>
/// Projeção de leitura de um problema com os dados que o painel exibe junto: nome do autor e
/// total de apoios. Montada direto na consulta SQL, sem carregar as entidades relacionadas.
/// </summary>
public sealed record ProblemaResumo(
    long Id,
    long ComunidadeId,
    string Titulo,
    string? Descricao,
    bool Adotado,
    StatusProblema? Status,
    Prioridade? Prioridade,
    string? GravidadeIa,
    DateTime CriadoEm,
    DateTime? AdotadoEm,
    string AutorNome,
    int TotalApoios,
    double? Latitude,
    double? Longitude,
    string? Categoria = null,
    string? JustificativaIa = null)
{
    /// <summary>Mesma regra de <c>Problema.StatusAtual</c>: adotado sem status conta como PENDENTE.</summary>
    public StatusProblema? StatusAtual => Adotado ? Status ?? StatusProblema.PENDENTE : null;
}

/// <summary>Quantidade de problemas supervisionados de uma comunidade em um status.</summary>
public sealed record ContagemPorStatus(long ComunidadeId, StatusProblema? Status, int Total);
