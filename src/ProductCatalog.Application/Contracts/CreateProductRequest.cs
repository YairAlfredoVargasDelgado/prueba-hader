using System.ComponentModel.DataAnnotations;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Datos necesarios para dar de alta un producto.
/// </summary>
/// <remarks>
/// Las anotaciones documentan el contrato en Swagger y permiten a ASP.NET Core
/// devolver un 400 con el detalle por campo antes de llegar al dominio.
/// La validación definitiva sigue estando en la entidad <see cref="Product"/>.
/// Los campos numéricos son anulables a propósito: así se distingue "no lo enviaron"
/// de "enviaron cero".
/// </remarks>
public class CreateProductRequest
{
    /// <summary>Nombre comercial del producto.</summary>
    /// <example>Teclado mecánico RGB</example>
    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [MaxLength(Product.MaxNameLength)]
    public string? Name { get; init; }

    /// <summary>Descripción del producto. Opcional.</summary>
    /// <example>Teclado mecánico switch azul, retroiluminado, layout español.</example>
    [MaxLength(Product.MaxDescriptionLength)]
    public string? Description { get; init; }

    /// <summary>Precio unitario. No admite valores negativos ni más de 2 decimales.</summary>
    /// <example>189900.00</example>
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0, (double)Product.MaxPrice, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal? Price { get; init; }

    /// <summary>Unidades disponibles en el momento del alta.</summary>
    /// <example>25</example>
    [Required(ErrorMessage = "El stock inicial es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El stock inicial no puede ser negativo.")]
    public int? Stock { get; init; }
}
