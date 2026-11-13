using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using RootFix.API.Auth;
using RootFix.Application.Interfaces.Idempotencia;
using RootFix.Infrastructure.Idempotencia;

namespace RootFix.API.Filters;

/// <summary>
/// Implementa o cabeçalho <c>Idempotency-Key</c> nas operações de escrita do painel.
/// </summary>
/// <remarks>
/// <para>O caso real: o gestor toca em "Marcar como resolvido", a rede do celular cai antes da
/// resposta e o app reenvia. Sem idempotência, a segunda requisição gravaria outro registro no
/// histórico e dispararia outra rodada de notificações aos moradores.</para>
/// <para>Funcionamento:</para>
/// <list type="number">
///   <item>Primeira vez da chave: a operação executa e a resposta de sucesso é guardada.</item>
///   <item>Mesma chave e mesmo conteúdo: devolve a resposta guardada, sem executar de novo,
///   com o cabeçalho <c>Idempotent-Replayed: true</c>.</item>
///   <item>Mesma chave com outro conteúdo: <c>422</c> (a chave foi reaproveitada por engano).</item>
///   <item>Mesma chave enquanto a primeira ainda executa: <c>409</c>.</item>
///   <item>Se a operação falhar (4xx/5xx), a reserva é desfeita e o cliente pode tentar de novo.</item>
/// </list>
/// <para>As chaves são por usuário: a mesma chave enviada por dois gestores não colide.</para>
/// </remarks>
public sealed class IdempotenciaFilter : IEndpointFilter
{
    public const string Cabecalho = "Idempotency-Key";
    public const string CabecalhoRepeticao = "Idempotent-Replayed";

    private readonly bool _obrigatoria;

    public IdempotenciaFilter(bool obrigatoria)
    {
        _obrigatoria = obrigatoria;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var chave = http.Request.Headers[Cabecalho].ToString().Trim();

        if (chave.Length == 0)
        {
            return _obrigatoria
                ? Problema(StatusCodes.Status400BadRequest,
                    "Cabeçalho Idempotency-Key obrigatório",
                    $"Envie um identificador único da operação no cabeçalho {Cabecalho} "
                    + "(ex.: um UUID gerado pelo app). Repita o mesmo valor ao reenviar a mesma operação.")
                : await next(context);
        }

        if (chave.Length > RegistroIdempotenciaConfiguration.TamanhoMaximoChave || chave.Any(char.IsControl))
        {
            return Problema(StatusCodes.Status400BadRequest,
                "Idempotency-Key inválida",
                $"A chave deve ter até {RegistroIdempotenciaConfiguration.TamanhoMaximoChave} caracteres visíveis.");
        }

        var servicos = http.RequestServices;
        var json = servicos.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        var store = servicos.GetRequiredService<IIdempotenciaStore>();
        var usuarioId = await servicos.GetRequiredService<IUsuarioAtual>().ObterIdAsync(http.RequestAborted);

        var rota = $"{http.Request.Method} {http.Request.Path}";
        var impressao = CalcularImpressaoDigital(http, context.Arguments, json);

        var reserva = await store.ReservarAsync(usuarioId, chave, rota, impressao, http.RequestAborted);

        switch (reserva.Situacao)
        {
            case SituacaoReserva.JaConcluida:
                http.Response.Headers[CabecalhoRepeticao] = "true";
                return Results.Content(
                    reserva.CorpoJson ?? string.Empty,
                    "application/json; charset=utf-8",
                    Encoding.UTF8,
                    reserva.StatusCode ?? StatusCodes.Status200OK);

            case SituacaoReserva.EmProcessamento:
                return Problema(StatusCodes.Status409Conflict,
                    "Requisição em andamento",
                    "Uma requisição com esta Idempotency-Key ainda está sendo processada. Aguarde e tente de novo.");

            case SituacaoReserva.ConteudoDiferente:
                return Problema(StatusCodes.Status422UnprocessableEntity,
                    "Idempotency-Key reutilizada",
                    "Esta Idempotency-Key já foi usada em outra operação. Gere uma chave nova para cada operação diferente.");
        }

        object? resultado;
        try
        {
            resultado = await next(context);
        }
        catch
        {
            // Falha (regra de negócio, conflito, banco fora...): libera para nova tentativa.
            await store.LiberarAsync(usuarioId, chave, CancellationToken.None);
            throw;
        }

        if (resultado is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } sucesso)
        {
            var valor = (resultado as IValueHttpResult)?.Value;
            var corpo = valor is null ? string.Empty : JsonSerializer.Serialize(valor, valor.GetType(), json);

            try
            {
                await store.ConcluirAsync(
                    usuarioId, chave, sucesso.StatusCode ?? StatusCodes.Status200OK, corpo, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // A operação já foi gravada: devolver erro agora faria o app achar que falhou.
                // A resposta segue normalmente; só a repetição desta chave fica sem replay.
                http.RequestServices.GetRequiredService<ILogger<IdempotenciaFilter>>().LogWarning(
                    ex, "Operação concluída, mas a resposta da Idempotency-Key não foi guardada.");
            }
        }
        else
        {
            await store.LiberarAsync(usuarioId, chave, CancellationToken.None);
        }

        return resultado;
    }

    /// <summary>
    /// Hash de método + rota + DTOs da requisição. Duas requisições só são "a mesma operação"
    /// se baterem nos três.
    /// </summary>
    internal static string CalcularImpressaoDigital(
        HttpContext http,
        IEnumerable<object?> argumentos,
        JsonSerializerOptions json)
    {
        var conteudo = new StringBuilder()
            .Append(http.Request.Method).Append('\n')
            .Append(http.Request.Path.Value?.ToLowerInvariant()).Append('\n');

        foreach (var argumento in argumentos)
        {
            if (argumento is not null && ValidacaoFilter.EhDto(argumento.GetType()))
                conteudo.Append(JsonSerializer.Serialize(argumento, argumento.GetType(), json)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(conteudo.ToString())));
    }

    private static IResult Problema(int status, string titulo, string detalhe)
        => TypedResults.Problem(detalhe, statusCode: status, title: titulo);
}

/// <summary>Marca o endpoint para o Swagger documentar o cabeçalho.</summary>
public sealed record IdempotenciaMetadata(bool Obrigatoria);

public static class IdempotenciaEndpointExtensions
{
    /// <summary>Aplica o <see cref="IdempotenciaFilter"/> ao endpoint.</summary>
    public static RouteHandlerBuilder ComIdempotencia(this RouteHandlerBuilder builder, bool obrigatoria = true)
        => builder
            .AddEndpointFilter(new IdempotenciaFilter(obrigatoria))
            .WithMetadata(new IdempotenciaMetadata(obrigatoria))
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
