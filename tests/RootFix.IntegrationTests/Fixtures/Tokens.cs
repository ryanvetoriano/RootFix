using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RootFix.API.Auth;
using RootFix.Domain.Enums;

namespace RootFix.IntegrationTests.Fixtures;

/// <summary>
/// Emite JWTs como a API Java emitiria, assinados com a chave de teste da fábrica.
/// </summary>
public static class Tokens
{
    public static string Para(RootFixApiFactory factory, long usuarioId, Perfil perfil)
    {
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        return TokenDeDesenvolvimento.Gerar(options, usuarioId, perfil, DateTime.UtcNow).Token;
    }

    /// <summary>Token com claims arbitrárias (ex.: formato de authorities do Spring Security).</summary>
    public static string Personalizado(
        RootFixApiFactory factory,
        IEnumerable<Claim> claims,
        DateTime? expiraEm = null,
        string? issuer = null,
        byte[]? chave = null,
        string algoritmo = SecurityAlgorithms.HmacSha256)
    {
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

        var token = new JwtSecurityToken(
            issuer: issuer ?? options.Issuer,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiraEm ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(chave ?? options.ObterBytesDaChave()),
                algoritmo));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient Cliente(RootFixApiFactory factory, string? token = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public static HttpClient Gestora(RootFixApiFactory factory)
        => Cliente(factory, Para(factory, DadosDeTeste.GestoraId, Perfil.GESTOR));

    public static HttpClient OutroGestor(RootFixApiFactory factory)
        => Cliente(factory, Para(factory, DadosDeTeste.OutroGestorId, Perfil.GESTOR));

    public static HttpClient Morador(RootFixApiFactory factory)
        => Cliente(factory, Para(factory, DadosDeTeste.MoradorId, Perfil.MORADOR));

    public static HttpClient MoradorDeFora(RootFixApiFactory factory)
        => Cliente(factory, Para(factory, DadosDeTeste.MoradorDeForaId, Perfil.MORADOR));
}
