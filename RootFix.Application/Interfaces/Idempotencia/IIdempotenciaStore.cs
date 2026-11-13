namespace RootFix.Application.Interfaces.Idempotencia;

/// <summary>
/// Guarda as respostas das requisições com <c>Idempotency-Key</c>, para que uma repetição
/// (ex.: o app reenviando depois de um timeout) devolva a mesma resposta sem executar a
/// operação de novo.
/// </summary>
public interface IIdempotenciaStore
{
    /// <summary>
    /// Tenta reservar a chave para esta requisição. A reserva é atômica: se duas requisições
    /// com a mesma chave chegarem juntas, só uma consegue.
    /// </summary>
    /// <param name="usuarioId">Dono da chave (o usuário autenticado). Chaves de usuários diferentes não colidem.</param>
    /// <param name="chave">Valor do cabeçalho <c>Idempotency-Key</c>.</param>
    /// <param name="rota">Método e caminho da requisição, gravados para consulta.</param>
    /// <param name="impressaoDigital">Hash do método, da rota e do corpo da requisição.</param>
    Task<ResultadoReserva> ReservarAsync(
        long usuarioId,
        string chave,
        string rota,
        string impressaoDigital,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda a resposta de sucesso para ser repetida nas próximas tentativas.</summary>
    Task ConcluirAsync(
        long usuarioId,
        string chave,
        int statusCode,
        string corpoJson,
        CancellationToken cancellationToken = default);

    /// <summary>Desfaz a reserva quando a operação falha, permitindo nova tentativa com a mesma chave.</summary>
    Task LiberarAsync(long usuarioId, string chave, CancellationToken cancellationToken = default);
}

public enum SituacaoReserva
{
    /// <summary>Primeira vez que a chave aparece: a operação deve ser executada.</summary>
    Reservada,

    /// <summary>A chave já foi usada com o mesmo conteúdo: devolver a resposta guardada.</summary>
    JaConcluida,

    /// <summary>Outra requisição com a mesma chave ainda está em andamento.</summary>
    EmProcessamento,

    /// <summary>A chave já foi usada com outro conteúdo (outro corpo ou outra rota).</summary>
    ConteudoDiferente
}

public sealed record ResultadoReserva(
    SituacaoReserva Situacao,
    int? StatusCode = null,
    string? CorpoJson = null);

/// <summary>Configuração da idempotência, lida da seção <c>Idempotencia</c>.</summary>
public sealed class IdempotenciaOptions
{
    public const string SectionName = "Idempotencia";

    /// <summary>Por quanto tempo uma chave concluída continua valendo.</summary>
    public int ValidadeEmHoras { get; set; } = 24;

    /// <summary>
    /// Depois desse tempo, uma reserva que nunca foi concluída (ex.: a instância caiu no meio)
    /// é considerada abandonada e pode ser assumida por uma nova tentativa.
    /// </summary>
    public int ReservaAbandonadaAposSegundos { get; set; } = 60;
}
