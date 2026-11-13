using System.Security.Claims;
using System.Text.Json;

namespace RootFix.API.Auth;

/// <summary>
/// Traduz o perfil que vem no JWT do Java para claims de papel do ASP.NET Core.
/// </summary>
/// <remarks>
/// O Spring Security pode gravar o perfil de vários jeitos: <c>"perfil": "GESTOR"</c>,
/// <c>"role": "ROLE_GESTOR"</c>, <c>"roles": ["GESTOR"]</c>,
/// <c>"authorities": [{"authority": "ROLE_GESTOR"}]</c> ou <c>"scope": "GESTOR outro"</c>.
/// Todos viram o papel <c>GESTOR</c>, que é o que a política <see cref="Politicas.Gestor"/> exige.
/// </remarks>
public static class PerfilDoToken
{
    private const string PrefixoSpring = "ROLE_";

    public static void AdicionarPapeis(ClaimsIdentity identidade, IEnumerable<string> claimsPerfil)
    {
        foreach (var tipo in claimsPerfil)
        {
            var valores = identidade.FindAll(tipo).Select(c => c.Value).ToList();
            if (valores.Count == 0) continue;

            foreach (var papel in valores.SelectMany(Extrair).Distinct())
            {
                if (!identidade.HasClaim(ClaimTypes.Role, papel))
                    identidade.AddClaim(new Claim(ClaimTypes.Role, papel));
            }

            return;
        }
    }

    internal static IEnumerable<string> Extrair(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            yield break;

        var texto = valor.Trim();

        // Objeto ou lista JSON (ex.: authorities serializadas pelo Spring).
        if (texto.StartsWith('{') || texto.StartsWith('['))
        {
            foreach (var item in ExtrairDeJson(texto))
                yield return Normalizar(item);

            yield break;
        }

        // "scope" e listas separadas por espaço ou vírgula.
        foreach (var parte in texto.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
            yield return Normalizar(parte);
    }

    private static IEnumerable<string> ExtrairDeJson(string json)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (documento)
        {
            var elementos = documento.RootElement.ValueKind == JsonValueKind.Array
                ? documento.RootElement.EnumerateArray().ToList()
                : [documento.RootElement];

            foreach (var elemento in elementos)
            {
                if (elemento.ValueKind == JsonValueKind.String)
                    yield return elemento.GetString()!;
                else if (elemento.ValueKind == JsonValueKind.Object &&
                         elemento.TryGetProperty("authority", out var authority) &&
                         authority.ValueKind == JsonValueKind.String)
                    yield return authority.GetString()!;
            }
        }
    }

    private static string Normalizar(string papel)
    {
        var texto = papel.Trim().ToUpperInvariant();
        return texto.StartsWith(PrefixoSpring, StringComparison.Ordinal)
            ? texto[PrefixoSpring.Length..]
            : texto;
    }
}

/// <summary>Políticas de autorização.</summary>
public static class Politicas
{
    /// <summary>Somente usuários com perfil GESTOR. Aplicada a todo o grupo <c>/painel</c>.</summary>
    public const string Gestor = "Gestor";

    /// <summary>Qualquer usuário autenticado (morador ou gestor).</summary>
    public const string Autenticado = "Autenticado";
}
