using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;

namespace RootFix.Domain.Entities;

/// <summary>
/// Registro de uma mudança de status feita pelo gestor no painel. Escrito pela API .NET; a
/// API Java só lê (ex.: para gerar notificações, se essa for a opção do adendo 2).
/// </summary>
public class HistoricoStatus
{
    public const int TamanhoMaximoObservacao = 500;

    public long Id { get; private set; }

    public long ProblemaId { get; private set; }

    /// <summary><c>null</c> quando o problema ainda não tinha status (adotado sem status inicial).</summary>
    public StatusProblema? StatusAnterior { get; private set; }

    public StatusProblema StatusNovo { get; private set; }

    /// <summary>Quando mudou, no horário de Brasília (mesmo fuso do ADOTADO_EM gravado pelo Java).</summary>
    public DateTime DataHora { get; private set; }

    /// <summary>Gestor que fez a alteração (RF_HISTORICO_STATUS.ID_USUARIO, obrigatória).</summary>
    public long UsuarioId { get; private set; }

    /// <summary>
    /// Recado público do gestor para os moradores (ex.: "Equipe agendada para 20/11").
    /// Aparece no acompanhamento do problema.
    /// </summary>
    public string? Observacao { get; private set; }

    private HistoricoStatus() { }

    internal HistoricoStatus(
        long problemaId,
        StatusProblema? statusAnterior,
        StatusProblema statusNovo,
        DateTime dataHora,
        long usuarioId,
        string? observacao)
    {
        var texto = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();

        if (texto is { Length: > TamanhoMaximoObservacao })
        {
            throw new DomainException(
                $"A observação deve ter no máximo {TamanhoMaximoObservacao} caracteres.");
        }

        ProblemaId = problemaId;
        StatusAnterior = statusAnterior;
        StatusNovo = statusNovo;
        DataHora = dataHora;
        UsuarioId = usuarioId;
        Observacao = texto;
    }

    /// <summary>Usado pelos testes e pela carga de dados.</summary>
    public static HistoricoStatus Reconstituir(
        long id,
        long problemaId,
        StatusProblema? statusAnterior,
        StatusProblema statusNovo,
        DateTime dataHora,
        long usuarioId = 0,
        string? observacao = null)
        => new()
        {
            Id = id,
            ProblemaId = problemaId,
            StatusAnterior = statusAnterior,
            StatusNovo = statusNovo,
            DataHora = dataHora,
            UsuarioId = usuarioId,
            Observacao = observacao
        };
}
