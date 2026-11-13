using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.EntityFrameworkCore.Infrastructure;
using RootFix.Application.Interfaces.Idempotencia;
using RootFix.Application.Interfaces.Mensageria;
using RootFix.Application.Interfaces.Repositories;
using RootFix.Infrastructure.Idempotencia;
using RootFix.Infrastructure.Mensageria;
using RootFix.Infrastructure.Persistence;
using RootFix.Infrastructure.Persistence.Repositories;

namespace RootFix.Infrastructure;

/// <summary>
/// Registro da camada de Infrastructure: Oracle via EF Core, repositórios, idempotência e
/// mensageria.
/// </summary>
public static class DependencyInjection
{
    public const string OracleConnectionStringName = "OracleConnection";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---------- Oracle (EF Core) ----------
        services.AddDbContext<RootFixContext>(options =>
            options.UseOracle(
                configuration.GetConnectionString(OracleConnectionStringName),
                // O Oracle da FIAP é 19c: sem isto, o provider poderia gerar SQL de versões
                // mais novas (ex.: tipo BOOLEAN do 23ai) que o servidor não entende.
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

        services.AddScoped<IProblemaRepository, ProblemaRepository>();
        services.AddScoped<IComunidadeRepository, ComunidadeRepository>();

        // ---------- Idempotência ----------
        services.AddOptions<IdempotenciaOptions>()
            .Bind(configuration.GetSection(IdempotenciaOptions.SectionName))
            .Validate(o => o.ValidadeEmHoras is >= 1 and <= 168,
                "Idempotencia:ValidadeEmHoras deve estar entre 1 e 168.")
            .Validate(o => o.ReservaAbandonadaAposSegundos is >= 5 and <= 3600,
                "Idempotencia:ReservaAbandonadaAposSegundos deve estar entre 5 e 3600.")
            .ValidateOnStart();

        services.AddScoped<IIdempotenciaStore, EfIdempotenciaStore>();

        // ---------- Mensageria (adendo 2 em aberto) ----------
        services.AddMensageria(configuration);

        return services;
    }

    private static void AddMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var secao = configuration.GetSection(RabbitMqOptions.SectionName);
        var options = secao.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        // Mesmo nome de variável da API Java (RABBITMQ_URL), para as duas apontarem para o
        // mesmo CloudAMQP. RabbitMq__Uri continua valendo e tem precedência.
        var uriDoJava = configuration["RABBITMQ_URL"];
        if (!options.EstaConfigurado && !string.IsNullOrWhiteSpace(uriDoJava))
            options.Uri = uriDoJava;

        services.Configure<RabbitMqOptions>(o =>
        {
            secao.Bind(o);
            o.Uri = options.Uri;
        });

        if (options.EstaConfigurado)
        {
            services.AddSingleton<RabbitMqConexao>();
            services.AddSingleton<IPublicadorDeEventos, RabbitMqPublicadorDeEventos>();
        }
        else
        {
            services.AddSingleton<IPublicadorDeEventos, PublicadorDeEventosDesligado>();
        }
    }
}
