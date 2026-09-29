namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Envoltorio estándar de los listados paginados.
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
public class PagedResponse<T>
{
    public PagedResponse(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    /// <summary>Elementos de la página solicitada.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Número de página devuelta (empieza en 1).</summary>
    /// <example>1</example>
    public int Page { get; }

    /// <summary>Tamaño de página aplicado.</summary>
    /// <example>10</example>
    public int PageSize { get; }

    /// <summary>Total de elementos que cumplen el filtro, en todas las páginas.</summary>
    /// <example>42</example>
    public int TotalItems { get; }

    /// <summary>Número total de páginas disponibles.</summary>
    /// <example>5</example>
    public int TotalPages { get; }

    /// <summary>Indica si existe una página anterior.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Indica si existe una página siguiente.</summary>
    public bool HasNextPage => Page < TotalPages;
}
