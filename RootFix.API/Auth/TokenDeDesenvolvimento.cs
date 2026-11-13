using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RootFix.Domain.Enums;

namespace RootFix.API.Auth;

/// <summary>
/// Emite tokens no mesmo formato esperado do Java, assinados com a chave configurada.
/// </summary>
/// <remarks>
/// Existe só para testar a API sozinha (Swagger, Postman, testes de integração) antes de o
/// login do Java estar no ar. O endpoint <c>POST /dev/token</c> só é mapeado em Development.
/// </remarks>
public sealed class TokenDeDesenvolvimento
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _relogio;

    public TokenDeDesenvolvimento(IOptions<JwtOptions> options, TimeProvider relogio)
    {
        _options = options.Value;
        _relogio = relogio;
    }

    public (string Token, DateTime ExpiraEm) Gerar(long usuarioId, Perfil perfil)
        => Gerar(_options, usuarioId, perfil, _relogio.GetUtcNow().UtcDateTime);

    public static (string Token, DateTime ExpiraEm) Gerar(
        JwtOptions options,
        long usuarioId,
        Perfil perfil,
        DateTime agora)
    {
        var expiraEm = agora.AddMinutes(options.ExpiracaoTokenDevEmMinutos);

        // Mesmas claims do TokenService do Java: sub (ID), perfil e iat. nome/email ficam de
        // fora porque a API .NET não os usa.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new(options.ClaimsPerfil.FirstOrDefault() ?? "perfil", perfil.ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(agora).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(options.ObterBytesDaChave()),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(options.Issuer) ? null : options.Issuer,
            audience: string.IsNullOrWhiteSpace(options.Audience) ? null : options.Audience,
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
