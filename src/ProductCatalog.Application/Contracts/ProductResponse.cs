using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Representación pública de un producto.
/// </summary>
public class ProductResponse
{
    /// <summary>Identificador único del producto.</summary>
    /// <example>1</example>
    public int Id { get; init; }

    /// <summary>Nombre comercial del producto.</summary>
    /// <example>Teclado mecánico RGB</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Descripción del producto. Cadena vacía si no se informó.</summary>
    /// <example>Teclado mecánico switch azul, retroiluminado, layout español.</example>
    public string Description { get; init; } = string.Empty;

    /// <summary>Precio unitario.</summary>
    /// <example>189900.00</example>
    public decimal Price { get; init; }

    /// <summary>Unidades disponibles.</summary>
    /// <example>25</example>
    public int Stock { get; init; }

    /// <summary>Fecha de creación en UTC (ISO 8601).</summary>
    /// <example>2026-09-29T14:30:00Z</example>
    public DateTime CreatedAt { get; init; }

    /// <summary>Fecha de la última modificación en UTC (ISO 8601).</summary>
    /// <example>2026-09-29T15:10:00Z</example>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Proyecta la entidad de dominio al contrato público.</summary>
    public static ProductResponse FromDomain(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Stock = product.Stock,
        CreatedAt = DateTime.SpecifyKind(product.CreatedAtUtc, DateTimeKind.Utc),
        UpdatedAt = DateTime.SpecifyKind(product.UpdatedAtUtc, DateTimeKind.Utc)
    };
}
