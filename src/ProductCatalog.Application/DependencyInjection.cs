using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Services;

namespace ProductCatalog.Application;

/// <summary>
/// Registro de los casos de uso en el contenedor de dependencias.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();

        return services;
    }
}
