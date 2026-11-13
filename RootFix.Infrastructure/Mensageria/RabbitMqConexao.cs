using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace RootFix.Infrastructure.Mensageria;

/// <summary>
/// Conexão única com o RabbitMQ, aberta na primeira necessidade e reaberta se cair.
/// Compartilhada pelo publicador e pelo health check.
/// </summary>
/// <remarks>
/// Conexões AMQP são caras e o plano gratuito do CloudAMQP limita quantas ficam abertas, por
/// isso a API mantém uma só em vez de abrir uma por requisição.
/// </remarks>
public sealed class RabbitMqConexao : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;

    public RabbitMqConexao(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IConnection> ObterAsync(CancellationToken cancellationToken = default)
    {
        if (_conexao is { IsOpen: true })
            return _conexao;

        await _trava.WaitAsync(cancellationToken);
        try
        {
            if (_conexao is { IsOpen: true })
                return _conexao;

            if (_conexao is not null)
                await _conexao.DisposeAsync();

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_options.Uri!),
                ClientProvidedName = "rootfix-api-dotnet",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(_options.TimeoutConexaoSegundos),
                AutomaticRecoveryEnabled = true
            };

            _conexao = await factory.CreateConnectionAsync(cancellationToken);
            return _conexao;
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is not null)
            await _conexao.DisposeAsync();

        _trava.Dispose();
    }
}
