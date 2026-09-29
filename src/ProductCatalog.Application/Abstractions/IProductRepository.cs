using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Application.Abstractions;

/// <summary>
/// Puerto de persistencia de productos. La capa de aplicación depende de esta
/// interfaz y no del motor de base de datos concreto, de modo que cambiar MySQL
/// por otro almacenamiento solo obliga a reescribir la infraestructura.
/// </summary>
public interface IProductRepository
{
    /// <summary>Inserta un producto y devuelve la entidad con el identificador asignado.</summary>
    Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto por su identificador, o <c>null</c> si no existe.</summary>
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve una página de productos junto con el total de coincidencias.
    /// </summary>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <param name="search">Filtro opcional por nombre (coincidencia parcial).</param>
    Task<(IReadOnlyList<Product> Items, int TotalItems)> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste los datos descriptivos de un producto.
    /// Devuelve <c>false</c> si el producto ya no existe.
    /// </summary>
    Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Elimina un producto. Devuelve <c>false</c> si no existía.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica un movimiento de inventario de forma atómica y devuelve el producto
    /// ya actualizado, o <c>null</c> si el producto no existe.
    /// </summary>
    /// <remarks>
    /// La operación es responsabilidad del repositorio porque la seguridad frente a
    /// escrituras concurrentes depende del motor: la implementación bloquea la fila
    /// dentro de una transacción antes de leer el stock, de modo que dos peticiones
    /// simultáneas no puedan partir del mismo valor. La regla de negocio (que el
    /// stock no quede negativo) la sigue aplicando la entidad de dominio.
    /// </remarks>
    /// <exception cref="InsufficientStockException">Si el ajuste dejaría el stock en negativo.</exception>
    Task<Product?> AdjustStockAsync(int id, int quantity, CancellationToken cancellationToken = default);
}
