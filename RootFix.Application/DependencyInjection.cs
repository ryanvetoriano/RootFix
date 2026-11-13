using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RootFix.Application.Common;
using RootFix.Application.Interfaces.Services;
using RootFix.Application.Services;

namespace RootFix.Application;

/// <summary>
/// Registro dos serviços da camada de Application.
/// </summary>
/// <remarks>
/// Cada camada declara as próprias dependências: o <c>Program.cs</c> apenas compõe as
/// camadas e não precisa conhecer as implementações concretas de cada uma.
/// </remarks>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Relógio injetável: os testes controlam "agora" sem depender da hora da máquina. A API
        // registra antes o RelogioDoRootFix (horário de Brasília); este é só o padrão.
        services.TryAddSingleton<TimeProvider>(new RelogioDoRootFix(RelogioDoRootFix.ObterFuso(null)));

        services.AddScoped<IPainelService, PainelService>();
        services.AddScoped<IIndicadoresService, IndicadoresService>();
        services.AddScoped<IAcompanhamentoService, AcompanhamentoService>();

        return services;
    }
}
