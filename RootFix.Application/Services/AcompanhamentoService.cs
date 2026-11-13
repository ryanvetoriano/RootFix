using RootFix.Application.DTOs.Acompanhamento;
using RootFix.Application.DTOs.Painel;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Application.Interfaces.Services;
using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;

namespace RootFix.Application.Services;

/// <summary>
/// A parte do .NET voltada ao cidadão: o morador acompanha em que pé está o problema que
/// publicou ou apoiou, com os recados que o gestor deixou a cada mudança de status.
/// </summary>
public sealed class AcompanhamentoService : IAcompanhamentoService
{
    private readonly IProblemaRepository _problemas;
    private readonly IComunidadeRepository _comunidades;

    public AcompanhamentoService(IProblemaRepository problemas, IComunidadeRepository comunidades)
    {
        _problemas = problemas;
        _comunidades = comunidades;
    }

    public async Task<AcompanhamentoResponse> ObterAsync(
        long usuarioId,
        long problemaId,
        CancellationToken cancellationToken = default)
    {
        var problema = await _problemas.ObterResumoAsync(problemaId, cancellationToken)
            ?? throw NotFoundException.For("Problema", problemaId);

        var comunidade = await _comunidades.ObterPorIdAsync(problema.ComunidadeId, cancellationToken)
            ?? throw NotFoundException.For("Comunidade", problema.ComunidadeId);

        var podeVer = comunidade.EhGerenciadaPor(usuarioId)
            || await _comunidades.EhMembroAsync(comunidade.Id, usuarioId, cancellationToken);

        if (!podeVer)
        {
            throw new AcessoNegadoException(
                "Só membros desta comunidade podem acompanhar os problemas dela.");
        }

        var historico = problema.Adotado
            ? await _problemas.ListarHistoricoAsync(problemaId, cancellationToken)
            : [];

        return new AcompanhamentoResponse(
            problema.Id,
            problema.Titulo,
            comunidade.Id,
            comunidade.Nome,
            problema.Adotado,
            problema.AdotadoEm,
            problema.StatusAtual,
            problema.Prioridade,
            DescreverSituacao(problema.StatusAtual),
            historico.Select(HistoricoStatusResponse.De).ToList());
    }

    /// <summary>Frase pronta para o app exibir ao morador.</summary>
    internal static string DescreverSituacao(StatusProblema? status) => status switch
    {
        null => "Aguardando o gestor da comunidade adotar o problema.",
        StatusProblema.PENDENTE => "Adotado pelo gestor e na fila de atendimento.",
        StatusProblema.EM_ANDAMENTO => "Em andamento: o gestor está cuidando do problema.",
        StatusProblema.RESOLVIDO => "Resolvido.",
        StatusProblema.ARQUIVADO => "Arquivado pelo gestor.",
        _ => "Situação desconhecida."
    };
}
