using System.Text;

namespace RootFix.API.Auth;

/// <summary>
/// Validação do JWT emitido pela API Java, lida da seção <c>Jwt</c>.
/// </summary>
/// <remarks>
/// <para>A API .NET <b>não emite</b> tokens: o gestor faz login no Java e usa o mesmo token aqui.</para>
/// <para><b>Contrato com a API Java</b> (<c>TokenService</c> e <c>JwtConfig</c> do Rootfix_Java):</para>
/// <list type="bullet">
///   <item>HS256 com os bytes UTF-8 do <c>JWT_SECRET</c> (32+ caracteres) → <see cref="SigningKey"/>,
///   <see cref="FormatoChave"/> = <c>Texto</c>.</item>
///   <item><c>iss</c> = <c>JWT_ISSUER</c> (padrão <c>rootfix-api</c>) → <see cref="Issuer"/>. Sem <c>aud</c>.</item>
///   <item><c>sub</c> = ID numérico do usuário em RF_USUARIO → <see cref="ClaimUsuario"/>.</item>
///   <item><c>perfil</c> = <c>GESTOR</c> ou <c>MORADOR</c> → <see cref="ClaimsPerfil"/>.</item>
///   <item>Também vêm <c>nome</c>, <c>email</c>, <c>iat</c> e <c>exp</c> (480 min por padrão).</item>
/// </list>
/// <para>Para as duas APIs lerem o mesmo valor, a chave e o emissor também são aceitos pelas
/// variáveis de ambiente do Java (<c>JWT_SECRET</c> e <c>JWT_ISSUER</c>).</para>
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Tamanho mínimo da chave para HMAC-SHA256 (256 bits).</summary>
    public const int TamanhoMinimoDaChaveEmBytes = 32;

    /// <summary>Emissor esperado (<c>iss</c>), o mesmo <c>JWT_ISSUER</c> do Java. Vazio = não valida.</summary>
    public string? Issuer { get; set; } = "rootfix-api";

    /// <summary>Audiência esperada (<c>aud</c>). Vazio = não valida.</summary>
    public string? Audience { get; set; }

    /// <summary>Segredo compartilhado com o Java. Nunca versionado.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// <c>Texto</c> (bytes UTF-8 do segredo, como <c>secret.getBytes()</c>) ou <c>Base64</c>
    /// (como <c>Decoders.BASE64.decode(secret)</c> do jjwt).
    /// </summary>
    public string FormatoChave { get; set; } = "Texto";

    /// <summary>Claim com o ID numérico do usuário (o Java grava em <c>sub</c>).</summary>
    public string ClaimUsuario { get; set; } = "sub";

    /// <summary>Claims onde o perfil pode estar (o Java grava em <c>perfil</c>). A primeira que existir é usada.</summary>
    public string[] ClaimsPerfil { get; set; } = ["perfil"];

    /// <summary>Validade dos tokens de teste emitidos por <c>POST /dev/token</c> (só em Development).</summary>
    public int ExpiracaoTokenDevEmMinutos { get; set; } = 120;

    public byte[] ObterBytesDaChave()
        => FormatoChave.Equals("Base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(SigningKey)
            : Encoding.UTF8.GetBytes(SigningKey);

    public static string GerarChaveAleatoria()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

    /// <summary>
    /// Falhar na inicialização é melhor do que subir aceitando tokens com chave fraca ou
    /// inexistente.
    /// </summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                $"A chave do JWT não foi configurada. Defina '{SectionName}:SigningKey' (a mesma da API Java) "
                + "via User Secrets ou a variável de ambiente 'Jwt__SigningKey'. Ela não deve ser versionada.");
        }

        if (!FormatoChave.Equals("Texto", StringComparison.OrdinalIgnoreCase) &&
            !FormatoChave.Equals("Base64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("'Jwt:FormatoChave' deve ser 'Texto' ou 'Base64'.");
        }

        byte[] bytes;
        try
        {
            bytes = ObterBytesDaChave();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("'Jwt:SigningKey' não é um Base64 válido.");
        }

        if (bytes.Length < TamanhoMinimoDaChaveEmBytes)
        {
            throw new InvalidOperationException(
                $"A chave do JWT precisa de no mínimo {TamanhoMinimoDaChaveEmBytes} bytes para HMAC-SHA256.");
        }

        if (string.IsNullOrWhiteSpace(ClaimUsuario))
            throw new InvalidOperationException("'Jwt:ClaimUsuario' não pode ser vazio.");
    }
}
