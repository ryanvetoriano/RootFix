using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RootFix.API.Filters;

/// <summary>
/// Valida, antes de chegar ao serviço, os DTOs de entrada do endpoint (corpo e filtros de
/// query) com as Data Annotations e o <see cref="IValidatableObject"/> de cada um.
/// </summary>
/// <remarks>
/// Uma entrada inválida devolve <c>400</c> com <c>ValidationProblemDetails</c>, listando o erro
/// de cada campo. Isso cobre o que a validação de tipos do binding não pega: campo obrigatório
/// ausente, texto longo demais, página fora do intervalo, ordenação desconhecida etc.
/// </remarks>
public sealed class ValidacaoFilter : IEndpointFilter
{
    private const string NamespaceDosDtos = "RootFix.Application.DTOs";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var erros = new Dictionary<string, List<string>>();

        foreach (var argumento in context.Arguments)
        {
            if (argumento is null || !EhDto(argumento.GetType()))
                continue;

            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(
                argumento,
                new ValidationContext(argumento, context.HttpContext.RequestServices, null),
                resultados,
                validateAllProperties: true);

            foreach (var resultado in resultados)
            {
                var campos = resultado.MemberNames.Any() ? resultado.MemberNames : [string.Empty];

                foreach (var campo in campos)
                {
                    var chave = JsonNamingPolicy.CamelCase.ConvertName(campo);
                    if (!erros.TryGetValue(chave, out var lista))
                        erros[chave] = lista = [];

                    lista.Add(resultado.ErrorMessage ?? "Valor inválido.");
                }
            }
        }

        if (erros.Count > 0)
        {
            return TypedResults.ValidationProblem(
                erros.ToDictionary(e => e.Key, e => e.Value.ToArray()),
                title: "Dados inválidos",
                detail: "Um ou mais campos não passaram na validação.");
        }

        return await next(context);
    }

    internal static bool EhDto(Type tipo)
        => tipo.Namespace?.StartsWith(NamespaceDosDtos, StringComparison.Ordinal) == true;
}
