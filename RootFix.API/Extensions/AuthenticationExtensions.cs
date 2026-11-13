using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RootFix.API.Auth;
using RootFix.Domain.Enums;
using Serilog;

namespace RootFix.API.Extensions;

/// <summary>
/// Autenticação por JWT Bearer, validando os tokens emitidos pela API Java.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // Mesmos nomes de variável da API Java: no Render, as duas leem JWT_SECRET e JWT_ISSUER,
        // e não tem como uma ficar com um valor diferente da outra. Jwt__SigningKey continua valendo.
        if (string.IsNullOrWhiteSpace(options.SigningKey) && !string.IsNullOrWhiteSpace(configuration["JWT_SECRET"]))
            options.SigningKey = configuration["JWT_SECRET"]!;

        if (!string.IsNullOrWhiteSpace(configuration["JWT_ISSUER"]))
            options.Issuer = configuration["JWT_ISSUER"];

        // Em Development a API sobe sem chave configurada (gera uma por processo), para quem
        // clonou o repositório conseguir explorar pelo Swagger com POST /dev/token.
        if (string.IsNullOrWhiteSpace(options.SigningKey) && environment.IsDevelopment())
        {
            options.SigningKey = JwtOptions.GerarChaveAleatoria();
            options.FormatoChave = "Base64";

            Log.Warning(
                "Nenhuma chave do JWT configurada: gerada uma chave temporária só para este processo. "
                + "Tokens da API Java NÃO serão aceitos. Configure Jwt:SigningKey com o segredo do Java.");
        }

        // Fora de Development a falha é imediata.
        options.Validar();

        // O DI precisa carregar a MESMA configuração usada na validação abaixo.
        services.Configure<JwtOptions>(jwt =>
        {
            jwt.Issuer = options.Issuer;
            jwt.Audience = options.Audience;
            jwt.SigningKey = options.SigningKey;
            jwt.FormatoChave = options.FormatoChave;
            jwt.ClaimUsuario = options.ClaimUsuario;
            jwt.ClaimsPerfil = options.ClaimsPerfil;
            jwt.ExpiracaoTokenDevEmMinutos = options.ExpiracaoTokenDevEmMinutos;
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                // Mantém os nomes das claims como vieram do Java ("sub", "perfil"...), sem a
                // tradução para os URIs longos do .NET.
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
                    ValidIssuer = options.Issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(options.ObterBytesDaChave()),
                    // O Java assina só com HS256 (MacAlgorithm.HS256): qualquer outro é recusado.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = options.ClaimUsuario,
                    // Pequena tolerância: os relógios do Render (Java) e da hospedagem do .NET
                    // não são sincronizados entre si.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identidade)
                            PerfilDoToken.AdicionarPapeis(identidade, options.ClaimsPerfil);

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Gestor, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(nameof(Perfil.GESTOR)))
            .AddPolicy(Politicas.Autenticado, policy => policy.RequireAuthenticatedUser());

        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioAtual, UsuarioAtual>();
        services.AddSingleton<TokenDeDesenvolvimento>();

        return services;
    }
}
