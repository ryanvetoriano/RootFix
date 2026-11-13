using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using RootFix.API.Auth;
using RootFix.Domain.Enums;

namespace RootFix.API.Endpoints;

/// <summary>
/// Endpoints de apoio ao desenvolvimento. Só são mapeados quando o ambiente é Development:
/// em produção (Render) a rota não existe.
/// </summary>
public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/token", GerarToken)
            .WithTags("Desenvolvimento")
            .AllowAnonymous()
            .WithName("GerarTokenDeTeste")
            .WithSummary("Gera um JWT de teste (somente Development)")
            .WithDescription(
                "Emite um token assinado com a chave configurada, no formato esperado do Java, para testar "
                + "o painel pelo Swagger antes de o login do Java estar disponível. Informe o ID de um usuário "
                + "que exista no Oracle.")
            .ProducesValidationProblem();

        return app;
    }

    private static Results<Ok<TokenDeTesteResponse>, ValidationProblem> GerarToken(
        TokenDeTesteRequest request,
        TokenDeDesenvolvimento tokens)
    {
        if (request.UsuarioId <= 0 || !Enum.IsDefined(request.Perfil))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["usuarioId"] = ["Informe o ID de um usuário existente e o perfil GESTOR ou MORADOR."]
            });
        }

        var (token, expiraEm) = tokens.Gerar(request.UsuarioId, request.Perfil);
        return TypedResults.Ok(new TokenDeTesteResponse(token, expiraEm));
    }
}

/// <param name="UsuarioId">ID do usuário no Oracle.</param>
/// <param name="Perfil">GESTOR ou MORADOR.</param>
public sealed record TokenDeTesteRequest([property: Range(1, long.MaxValue)] long UsuarioId, Perfil Perfil);

public sealed record TokenDeTesteResponse(string Token, DateTime ExpiraEm);
