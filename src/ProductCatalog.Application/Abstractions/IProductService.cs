using ProductCatalog.Application.Contracts;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Application.Abstractions;

/// <summary>
/// Casos de uso del catálogo de productos. Es la frontera que consumen los
/// controladores: reciben y devuelven contratos, nunca entidades de dominio.
/// </summary>
public interface IProductService
{
    /// <summary>Da de alta un producto.</summary>
    /// <exception cref="BusinessRuleException">Si los datos incumplen las reglas del dominio.</exception>
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto por su identificador.</summary>
    /// <exception cref="ProductNotFoundException">Si el producto no existe.</exception>
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Devuelve un listado paginado de productos.</summary>
    Task<PagedResponse<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default);

    /// <summary>Actualiza los datos descriptivos de un producto.</summary>
    /// <exception cref="ProductNotFoundException">Si el producto no existe.</exception>
    /// <exception cref="BusinessRuleException">Si los datos incumplen las reglas del dominio.</exception>
    Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Elimina un producto.</summary>
    /// <exception cref="ProductNotFoundException">Si el producto no existe.</exception>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Suma o resta unidades al stock de un producto.</summary>
    /// <exception cref="ProductNotFoundException">Si el producto no existe.</exception>
    /// <exception cref="InsufficientStockException">Si el ajuste dejaría el stock en negativo.</exception>
    Task<ProductResponse> AdjustStockAsync(int id, AdjustStockRequest request, CancellationToken cancellationToken = default);
}
