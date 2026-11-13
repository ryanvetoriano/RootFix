using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFix.Application.Interfaces.Idempotencia;
using RootFix.Domain.Exceptions;
using RootFix.Infrastructure.Persistence;

namespace RootFix.Infrastructure.Idempotencia;

/// <summary>
/// Implementação de <see cref="IIdempotenciaStore"/> sobre a tabela RF_IDEMPOTENCIA.
/// </summary>
public sealed class EfIdempotenciaStore : IIdempotenciaStore
{
    private readonly RootFixContext _context;
    private readonly TimeProvider _relogio;
    private readonly IdempotenciaOptions _options;
    private readonly ILogger<EfIdempotenciaStore> _logger;

    public EfIdempotenciaStore(
        RootFixContext context,
        TimeProvider relogio,
        IOptions<IdempotenciaOptions> options,
        ILogger<EfIdempotenciaStore> logger)
    {
        _context = context;
        _relogio = relogio;
        _options = options.Value;
        _logger = logger;
    }

    private DbSet<RegistroIdempotencia> Registros => _context.RegistrosIdempotencia;

    public async Task<ResultadoReserva> ReservarAsync(
        long usuarioId,
        string chave,
        string rota,
        string impressaoDigital,
        CancellationToken cancellationToken = default)
    {
        var agora = Agora();

        var existente = await Registros.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Chave == chave && x.UsuarioId == usuarioId, cancellationToken);

        if (existente is not null)
        {
            if (existente.CriadoEm.AddHours(_options.ValidadeEmHoras) > agora)
                return await AvaliarExistenteAsync(existente, impressaoDigital, agora, cancellationToken);

            // Chave vencida: é como se nunca tivesse sido usada.
            await Registros.Where(x => x.Id == existente.Id).ExecuteDeleteAsync(cancellationToken);
        }

        // ID_USUARIO é FK para RF_USUARIO. Um token válido de usuário inexistente viraria erro
        // de integridade (500); aqui ele vira 403, como nos demais endpoints.
        if (!await _context.Usuarios.AnyAsync(u => u.Id == usuarioId, cancellationToken))
        {
            throw new AcessoNegadoException(
                "O token é válido, mas não identifica um usuário cadastrado no RootFix.");
        }

        var novo = new RegistroIdempotencia
        {
            Chave = chave,
            UsuarioId = usuarioId,
            Rota = rota.Length > RegistroIdempotenciaConfiguration.TamanhoMaximoRota
                ? rota[..RegistroIdempotenciaConfiguration.TamanhoMaximoRota]
                : rota,
            ImpressaoDigital = impressaoDigital,
            StatusHttp = RegistroIdempotencia.EmProcessamento,
            CriadoEm = agora
        };

        Registros.Add(novo);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new ResultadoReserva(SituacaoReserva.Reservada);
        }
        catch (DbUpdateException ex)
        {
            // Outra requisição com a mesma chave inseriu primeiro (UK_RF_IDEMPOTENCIA). O registro
            // que falhou sai do change tracker para não ser reenviado no próximo SaveChanges.
            _context.Entry(novo).State = EntityState.Detached;

            var vencedor = await Registros.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Chave == chave && x.UsuarioId == usuarioId, cancellationToken);

            if (vencedor is null)
                throw;

            _logger.LogInformation(
                ex,
                "Corrida na reserva da chave de idempotência: outra requisição reservou primeiro.");

            return await AvaliarExistenteAsync(vencedor, impressaoDigital, agora, cancellationToken);
        }
    }

    public async Task ConcluirAsync(
        long usuarioId,
        string chave,
        int statusCode,
        string corpoJson,
        CancellationToken cancellationToken = default)
    {
        var registro = await Registros
            .FirstOrDefaultAsync(x => x.Chave == chave && x.UsuarioId == usuarioId, cancellationToken);

        if (registro is null)
        {
            _logger.LogWarning(
                "Reserva da chave de idempotência não encontrada ao concluir; a resposta não será repetida.");
            return;
        }

        registro.StatusHttp = statusCode;
        registro.Resposta = corpoJson;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LiberarAsync(long usuarioId, string chave, CancellationToken cancellationToken = default)
    {
        // Só apaga reservas em andamento: uma resposta já concluída nunca é descartada.
        await Registros
            .Where(x => x.Chave == chave
                        && x.UsuarioId == usuarioId
                        && x.StatusHttp == RegistroIdempotencia.EmProcessamento)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<ResultadoReserva> AvaliarExistenteAsync(
        RegistroIdempotencia existente,
        string impressaoDigital,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        if (existente.ImpressaoDigital != impressaoDigital)
            return new ResultadoReserva(SituacaoReserva.ConteudoDiferente);

        if (existente.StatusHttp != RegistroIdempotencia.EmProcessamento)
            return new ResultadoReserva(SituacaoReserva.JaConcluida, existente.StatusHttp, existente.Resposta);

        var abandonadaAntesDe = agora.AddSeconds(-_options.ReservaAbandonadaAposSegundos);
        if (existente.CriadoEm >= abandonadaAntesDe)
            return new ResultadoReserva(SituacaoReserva.EmProcessamento);

        // A requisição original nunca terminou (instância caiu no meio). A condição em CRIADO_EM
        // garante que só uma nova tentativa assuma a reserva; a validade recomeça a contar.
        var assumidas = await Registros
            .Where(x => x.Id == existente.Id
                        && x.StatusHttp == RegistroIdempotencia.EmProcessamento
                        && x.CriadoEm == existente.CriadoEm)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEm, agora), cancellationToken);

        return assumidas == 1
            ? new ResultadoReserva(SituacaoReserva.Reservada)
            : new ResultadoReserva(SituacaoReserva.EmProcessamento);
    }

    // Mesmo fuso das demais datas do banco (Brasília), para o CRIADO_EM fazer sentido num SELECT.
    private DateTime Agora() => _relogio.GetLocalNow().DateTime;
}
