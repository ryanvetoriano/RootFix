using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RootFix.Application.Interfaces.Mensageria;

namespace RootFix.Infrastructure.Mensageria;

/// <summary>
/// Publica <c>problema.status-alterado</c> no RabbitMQ (CloudAMQP no Render, container na Azure).
/// </summary>
public sealed class RabbitMqPublicadorDeEventos : IPublicadorDeEventos
{
    /// <summary>
    /// JSON em camelCase com enums como texto, o formato que o Jackson do Java lê por padrão.
    /// Campos nulos são enviados (ex.: <c>"observacao": null</c>), como no contrato combinado.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly RabbitMqConexao _conexao;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublicadorDeEventos> _logger;

    public RabbitMqPublicadorDeEventos(
        RabbitMqConexao conexao,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqPublicadorDeEventos> logger)
    {
        _conexao = conexao;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublicarStatusAlteradoAsync(
        ProblemaStatusAlteradoEvento evento,
        CancellationToken cancellationToken = default)
    {
        var conexao = await _conexao.ObterAsync(cancellationToken);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: cancellationToken);

        if (_options.DeclararExchange)
        {
            await canal.ExchangeDeclareAsync(
                _options.Exchange,
                _options.TipoExchange,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        var propriedades = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Type = _options.RoutingKeyStatusAlterado,
            AppId = "rootfix-api-dotnet",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var corpo = JsonSerializer.SerializeToUtf8Bytes(evento, Json);

        await canal.BasicPublishAsync(
            _options.Exchange,
            _options.RoutingKeyStatusAlterado,
            mandatory: false,
            basicProperties: propriedades,
            body: corpo,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Evento {RoutingKey} publicado para o problema {ProblemaId} ({StatusAnterior} → {StatusNovo}).",
            _options.RoutingKeyStatusAlterado, evento.ProblemaId, evento.StatusAnterior, evento.StatusNovo);
    }
}

/// <summary>
/// Usado quando <c>RabbitMq:Uri</c> não está configurado (ex.: rodando local): a mudança de
/// status fica só no Oracle (RF_HISTORICO_STATUS) e nenhum evento sai para o Java.
/// </summary>
public sealed class PublicadorDeEventosDesligado : IPublicadorDeEventos
{
    private readonly ILogger<PublicadorDeEventosDesligado> _logger;

    public PublicadorDeEventosDesligado(ILogger<PublicadorDeEventosDesligado> logger)
    {
        _logger = logger;
    }

    public Task PublicarStatusAlteradoAsync(
        ProblemaStatusAlteradoEvento evento,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Mensageria desligada: mudança do problema {ProblemaId} registrada só no histórico.",
            evento.ProblemaId);

        return Task.CompletedTask;
    }
}
