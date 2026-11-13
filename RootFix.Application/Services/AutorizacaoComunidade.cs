using RootFix.Application.Interfaces.Repositories;
using RootFix.Domain.Entities;
using RootFix.Domain.Exceptions;

namespace RootFix.Application.Services;

/// <summary>
/// Regras de acesso por comunidade, compartilhadas pelos serviços.
/// </summary>
/// <remarks>
/// O JWT diz <b>quem</b> é o usuário e <b>qual</b> o perfil dele; a política <c>Gestor</c>
/// barra moradores no painel antes de chegar aqui. O que só o banco sabe é <b>de qual</b>
/// comunidade ele é gestor — essa checagem fica aqui.
/// </remarks>
internal static class AutorizacaoComunidade
{
    public static async Task<Comunidade> ObterComoGestorAsync(
        IComunidadeRepository comunidades,
        long comunidadeId,
        long gestorId,
        CancellationToken cancellationToken)
    {
        var comunidade = await comunidades.ObterPorIdAsync(comunidadeId, cancellationToken)
            ?? throw NotFoundException.For("Comunidade", comunidadeId);

        GarantirGestor(comunidade, gestorId);

        return comunidade;
    }

    public static void GarantirGestor(Comunidade comunidade, long gestorId)
    {
        if (!comunidade.EhGerenciadaPor(gestorId))
        {
            throw new AcessoNegadoException(
                "Apenas o gestor desta comunidade pode acessar o painel dela.");
        }
    }
}
