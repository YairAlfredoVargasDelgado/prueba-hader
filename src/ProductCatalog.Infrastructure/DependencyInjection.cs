using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Infrastructure.Persistence;

namespace ProductCatalog.Infrastructure;

/// <summary>
/// Registro de la capa de infraestructura en el contenedor de dependencias.
/// Mantener el registro aquí evita que la API conozca las clases concretas de
/// acceso a datos: solo pide "la infraestructura" y recibe las implementaciones.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new MySqlConnectionFactory(connectionString));
        services.AddScoped<IProductRepository, ProductRepository>();

        services.AddSingleton(provider => new DatabaseInitializer(
            connectionString,
            provider.GetRequiredService<ILogger<DatabaseInitializer>>()));

        return services;
    }
}
