using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Parámetros de consulta del listado de productos.
/// </summary>
public class ProductQuery
{
    /// <summary>Tamaño de página máximo permitido, para proteger a la base de datos.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Número de página, empezando en 1.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor o igual a 1.")]
    public int Page { get; init; } = 1;

    /// <summary>Cantidad de elementos por página (entre 1 y 100).</summary>
    /// <example>10</example>
    [Range(1, MaxPageSize, ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int PageSize { get; init; } = 10;

    /// <summary>Filtro opcional: devuelve los productos cuyo nombre contenga este texto.</summary>
    /// <example>teclado</example>
    [MaxLength(150)]
    public string? Search { get; init; }
}
