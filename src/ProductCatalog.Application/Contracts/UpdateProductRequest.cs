using System.ComponentModel.DataAnnotations;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Datos descriptivos de un producto existente.
/// </summary>
/// <remarks>
/// El stock no se modifica por aquí de forma deliberada: todo movimiento de
/// inventario pasa por <c>PATCH /api/products/{id}/stock</c>, que es el único
/// camino con control de concurrencia.
/// </remarks>
public class UpdateProductRequest
{
    /// <summary>Nombre comercial del producto.</summary>
    /// <example>Teclado mecánico RGB Pro</example>
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [MaxLength(Product.MaxNameLength)]
    public string? Name { get; init; }

    /// <summary>Descripción del producto. Opcional.</summary>
    /// <example>Teclado mecánico switch rojo, retroiluminado, layout español.</example>
    [MaxLength(Product.MaxDescriptionLength)]
    public string? Description { get; init; }

    /// <summary>Precio unitario. No admite valores negativos ni más de 2 decimales.</summary>
    /// <example>209900.00</example>
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0, (double)Product.MaxPrice, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal? Price { get; init; }
}
