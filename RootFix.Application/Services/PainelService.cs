using Microsoft.Extensions.Logging;
using RootFix.Application.Common;
using RootFix.Application.DTOs.Common;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Mensageria;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Interfaces.Services;
using RootFix.Domain.Entities;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.Domain.Rules;

namespace RootFix.Application.Services;

public sealed class PainelService : IPainelService
{
    private readonly IProblemaRepository _problemas;
    private readonly IComunidadeRepository _comunidades;
    private readonly IPublicadorDeEventos _publicador;
    private readonly TimeProvider _relogio;
    private readonly ILogger<PainelService> _logger;

    public PainelService(
        IProblemaRepository problemas,
        IComunidadeRepository comunidades,
        IPublicadorDeEventos publicador,
        TimeProvider relogio,
        ILogger<PainelService> logger)
    {
        _problemas = problemas;
        _comunidades = comunidades;
        _publicador = publicador;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ComunidadePainelResponse>> ListarComunidadesAsync(
        long gestorId,
        CancellationToken cancellationToken = default)
    {
        var comunidades = await _comunidades.ListarDoGestorAsync(gestorId, cancellationToken);
        if (comunidades.Count == 0) return [];

        var contagens = await _problemas.ContarSupervisionadosPorStatusAsync(
            comunidades.Select(c => c.Id).ToList(), cancellationToken);

        return comunidades
            .Select(c =>
            {
                var daComunidade = contagens.Where(x => x.ComunidadeId == c.Id).ToList();

                // Adotado sem status gravado conta como PENDENTE (mesma regra do domínio).
                int Contar(StatusProblema status) => daComunidade
                    .Where(x => (x.Status ?? StatusProblema.PENDENTE) == status)
                    .Sum(x => x.Total);

                return new ComunidadePainelResponse(
                    c.Id,
                    c.Nome,
                    c.Descricao,
                    daComunidade.Sum(x => x.Total),
                    Contar(StatusProblema.PENDENTE),
                    Contar(StatusProblema.EM_ANDAMENTO),
                    Contar(StatusProblema.RESOLVIDO),
                    Contar(StatusProblema.ARQUIVADO));
            })
            .ToList();
    }

    public async Task<PagedResponse<ProblemaSupervisionadoResponse>> BuscarSupervisionadosAsync(
        long gestorId,
        long comunidadeId,
        SupervisionadosFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizacaoComunidade.ObterComoGestorAsync(
            _comunidades, comunidadeId, gestorId, cancellationToken);

        var pagina = await _problemas.BuscarSupervisionadosAsync(comunidadeId, filtro, cancellationToken);

        return PagedResponse<ProblemaSupervisionadoResponse>.De(
            pagina, filtro, ProblemaSupervisionadoResponse.De);
    }

    public async Task<ProblemaDetalheResponse> ObterDetalheAsync(
        long gestorId,
        long problemaId,
        CancellationToken cancellationToken = default)
    {
        var resumo = await _problemas.ObterResumoAsync(problemaId, cancellationToken)
            ?? throw NotFoundException.For("Problema", problemaId);

        var comunidade = await AutorizacaoComunidade.ObterComoGestorAsync(
            _comunidades, resumo.ComunidadeId, gestorId, cancellationToken);

        var historico = await _problemas.ListarHistoricoAsync(problemaId, cancellationToken);

        return new ProblemaDetalheResponse(
            ProblemaSupervisionadoResponse.De(resumo),
            comunidade.Nome,
            historico.Select(HistoricoStatusResponse.De).ToList());
    }

    public async Task<AlteracaoStatusResponse> AlterarStatusAsync(
        long gestorId,
        long problemaId,
        AlterarStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var problema = await ObterProblemaDoGestorAsync(gestorId, problemaId, cancellationToken);

        // As regras (adotado, transição permitida, status repetido) ficam na entidade.
        var historico = problema.AlterarStatus(
            request.Status!.Value,
            gestorId,
            // Hora de Brasília, o mesmo fuso que o Java usa em ADOTADO_EM (ver RelogioDoRootFix).
            _relogio.GetLocalNow().DateTime,
            request.Observacao);

        await _problemas.SalvarAlteracaoDeStatusAsync(problema, historico, cancellationToken);

        _logger.LogInformation(
            "Problema {ProblemaId} mudou de {StatusAnterior} para {StatusNovo} pelo gestor {GestorId}.",
            problema.Id, historico.StatusAnterior, historico.StatusNovo, gestorId);

        // Depois do commit, o token da requisição não é mais usado: se o app desconectar agora
        // (rede do celular caiu), a mudança já está gravada e precisa ser respondida/repetida
        // como sucesso pela idempotência, não virar erro de cancelamento.
        await PublicarEventoAsync(problema, historico);

        return new AlteracaoStatusResponse(
            problema.Id,
            historico.StatusAnterior,
            historico.StatusNovo,
            historico.DataHora,
            historico.Observacao,
            historico.Id,
            TransicaoDeStatus.ProximosPossiveis(historico.StatusNovo));
    }

    public async Task<PrioridadeResponse> DefinirPrioridadeAsync(
        long gestorId,
        long problemaId,
        DefinirPrioridadeRequest request,
        CancellationToken cancellationToken = default)
    {
        var problema = await ObterProblemaDoGestorAsync(gestorId, problemaId, cancellationToken);

        var anterior = problema.Prioridade;
        problema.DefinirPrioridade(request.Prioridade!.Value);

        await _problemas.SalvarAsync(problema, cancellationToken);

        return new PrioridadeResponse(problema.Id, anterior, problema.Prioridade!.Value);
    }

    private async Task<Problema> ObterProblemaDoGestorAsync(
        long gestorId,
        long problemaId,
        CancellationToken cancellationToken)
    {
        var problema = await _problemas.ObterPorIdAsync(problemaId, cancellationToken)
            ?? throw NotFoundException.For("Problema", problemaId);

        var comunidade = problema.Comunidade
            ?? await _comunidades.ObterPorIdAsync(problema.ComunidadeId, cancellationToken)
            ?? throw NotFoundException.For("Comunidade", problema.ComunidadeId);

        AutorizacaoComunidade.GarantirGestor(comunidade, gestorId);

        return problema;
    }

    /// <summary>
    /// O status já está gravado quando o evento sai. Se a fila estiver fora do ar, a mudança
    /// não é desfeita: o histórico no Oracle continua sendo a fonte da verdade e o Java pode
    /// lê-lo. Por isso uma falha aqui vira apenas um aviso no log.
    /// </summary>
    private async Task PublicarEventoAsync(
        Problema problema,
        HistoricoStatus historico)
    {
        // Prazo próprio, independente da requisição: a fila não pode segurar a resposta.
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var evento = new ProblemaStatusAlteradoEvento(
            problema.Id,
            historico.StatusAnterior,
            historico.StatusNovo,
            historico.Observacao);

        try
        {
            await _publicador.PublicarStatusAlteradoAsync(evento, prazo.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Status do problema {ProblemaId} gravado, mas o evento não foi publicado.",
                problema.Id);
        }
    }
}
