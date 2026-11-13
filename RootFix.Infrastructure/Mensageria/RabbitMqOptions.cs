namespace RootFix.Infrastructure.Mensageria;

/// <summary>
/// Configuração do RabbitMQ, lida da seção <c>RabbitMq</c>.
/// </summary>
/// <remarks>
/// Os padrões são os do <c>RabbitConfig</c> da API Java: exchange <c>rootfix.eventos</c> (topic,
/// durável), routing key <c>problema.status-alterado</c>, ligada pelo Java à fila
/// <c>rootfix.problema.status-alterado</c>. A <see cref="Uri"/> (usuário e senha do CloudAMQP)
/// nunca vai para o repositório: vem de <c>RabbitMq__Uri</c> ou de <c>RABBITMQ_URL</c>, a mesma
/// variável do Java.
/// </remarks>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// <c>amqps://usuario:senha@host/vhost</c>. Vazio = mensageria desligada (o Java lê o
    /// histórico no Oracle).
    /// </summary>
    public string? Uri { get; set; }

    public string Exchange { get; set; } = "rootfix.eventos";

    /// <summary>Tipo da exchange declarada caso ainda não exista (<c>topic</c>, <c>direct</c>...).</summary>
    public string TipoExchange { get; set; } = "topic";

    public string RoutingKeyStatusAlterado { get; set; } = "problema.status-alterado";

    /// <summary>
    /// Declara a exchange ao conectar. Desligue se o Java for o dono da declaração e usar
    /// argumentos diferentes (o RabbitMQ recusa redeclarar com outra configuração).
    /// </summary>
    public bool DeclararExchange { get; set; } = true;

    public int TimeoutConexaoSegundos { get; set; } = 5;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Uri);
}
