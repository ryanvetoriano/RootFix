using System.Globalization;
using Microsoft.Extensions.Options;
using RootFix.Domain.Exceptions;

namespace RootFix.API.Auth;

/// <summary>Identifica o usuário autenticado da requisição atual.</summary>
public interface IUsuarioAtual
{
    /// <summary>ID do usuário no Oracle (o mesmo ID da API Java).</summary>
    Task<long> ObterIdAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lê o ID do usuário do JWT. O Java grava o ID numérico de RF_USUARIO no <c>sub</c>
/// (<c>UsuarioLogado.id()</c> faz o mesmo do lado de lá).
/// </summary>
public sealed class UsuarioAtual : IUsuarioAtual
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JwtOptions _options;

    public UsuarioAtual(IHttpContextAccessor httpContextAccessor, IOptions<JwtOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public Task<long> ObterIdAsync(CancellationToken cancellationToken = default)
    {
        var usuario = _httpContextAccessor.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated != true)
            throw new AcessoNegadoException("Usuário não autenticado.");

        var valor = usuario.FindFirst(_options.ClaimUsuario)?.Value;

        if (long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            return Task.FromResult(id);

        throw new AcessoNegadoException(
            "O token é válido, mas não identifica um usuário do RootFix (sub deve ser o ID numérico).");
    }
}
