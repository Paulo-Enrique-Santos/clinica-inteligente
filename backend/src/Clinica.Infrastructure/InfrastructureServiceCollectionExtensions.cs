using Clinica.Infrastructure.Persistence;
using Clinica.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra o acesso a dados. A connection string aqui deve ser a do usuario de
    /// RUNTIME (<c>clinica_app</c>), que nao e dono das tabelas — caso contrario o
    /// Postgres ignora as policies de RLS e a segunda barreira deixa de existir.
    /// </summary>
    public static IServiceCollection AddClinicaPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<ClinicaDbContext>((serviceProvider, options) =>
        {
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(serviceProvider.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddScoped<DisponibilidadeService>();

        return services;
    }
}
