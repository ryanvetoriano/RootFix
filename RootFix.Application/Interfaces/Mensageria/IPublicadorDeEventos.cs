using RootFix.Domain.Enums;

namespace RootFix.Application.Interfaces.Mensageria;

/// <summary>
/// Publica eventos para a API Java gerar as notificações dos moradores.
/// </summary>
/// <remarks>
/// Adendo 2 decidido: o .NET publica <c>problema.status-alterado</c> no RabbitMQ e o Java consome.
/// Sem <c>RabbitMq:Uri</c> configurado (ex.: rodando local), é usado um publicador que não faz
/// nada; o histórico continua gravado em RF_HISTORICO_STATUS.
/// </remarks>
public interface IPublicadorDeEventos
{
    Task PublicarStatusAlteradoAsync(
        ProblemaStatusAlteradoEvento evento,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Payload de <c>problema.status-alterado</c>, no formato combinado com a API Java:
/// <code>{ "problemaId": 12, "statusAnterior": "PENDENTE", "statusNovo": "EM_ANDAMENTO", "observacao": "A equipe vai amanhã cedo" }</code>
/// O Java busca no Oracle o resto do que precisa (autor, quem comentou, membros) para aplicar
/// a regra de notificação. <c>observacao</c> vai como <c>null</c> quando o gestor não deixou recado.
/// </summary>
public sealed record ProblemaStatusAlteradoEvento(
    long ProblemaId,
    StatusProblema? StatusAnterior,
    StatusProblema StatusNovo,
    string? Observacao);
