using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Domain.Entities;

/// <summary>
/// Producto del catálogo. Es la única pieza autorizada para decidir si un dato
/// es válido: el resto de capas delega en ella en lugar de repetir validaciones.
/// Las propiedades tienen <c>private set</c> para que un producto nunca pueda
/// quedar en un estado inconsistente desde fuera del dominio.
/// </summary>
public class Product
{
    /// <summary>Longitud máxima del nombre, alineada con la columna VARCHAR(150).</summary>
    public const int MaxNameLength = 150;

    /// <summary>Longitud máxima de la descripción, alineada con la columna VARCHAR(500).</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>Precio máximo admitido, alineado con la columna DECIMAL(12,2).</summary>
    public const decimal MaxPrice = 9_999_999_999.99m;

    private Product()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public int Stock { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Crea un producto nuevo (aún sin identificador; lo asigna la base de datos).
    /// </summary>
    /// <exception cref="BusinessRuleException">Si algún dato incumple las reglas del dominio.</exception>
    public static Product Create(string name, string? description, decimal price, int initialStock)
    {
        var now = DateTime.UtcNow;

        return new Product
        {
            Name = NormalizeName(name),
            Description = NormalizeDescription(description),
            Price = NormalizePrice(price),
            Stock = NormalizeInitialStock(initialStock),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Reconstruye un producto ya persistido a partir de los datos de la base.
    /// No revalida: los datos provienen de un estado que ya fue válido al guardarse.
    /// Uso exclusivo de la capa de infraestructura.
    /// </summary>
    public static Product Rehydrate(
        int id,
        string name,
        string description,
        decimal price,
        int stock,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            Stock = stock,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    /// <summary>
    /// Actualiza los datos descriptivos del producto. El stock no se toca aquí:
    /// el inventario se mueve únicamente con <see cref="AdjustStock"/>, para que
    /// todo movimiento quede sujeto a la misma regla de no negatividad.
    /// </summary>
    /// <exception cref="BusinessRuleException">Si algún dato incumple las reglas del dominio.</exception>
    public void UpdateDetails(string name, string? description, decimal price)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Price = NormalizePrice(price);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suma o resta unidades al stock actual.
    /// </summary>
    /// <param name="quantity">Positivo para ingresar unidades, negativo para descontarlas. No admite cero.</param>
    /// <exception cref="BusinessRuleException">Si la cantidad es cero o desborda el rango admitido.</exception>
    /// <exception cref="InsufficientStockException">Si el ajuste dejaría el stock en negativo.</exception>
    public void AdjustStock(int quantity)
    {
        if (quantity == 0)
        {
            throw new BusinessRuleException("La cantidad a ajustar debe ser distinta de cero.");
        }

        // Se calcula en long para detectar el desbordamiento antes de que ocurra.
        var newStock = (long)Stock + quantity;

        if (newStock < 0)
        {
            throw new InsufficientStockException(Stock, quantity);
        }

        if (newStock > int.MaxValue)
        {
            throw new BusinessRuleException($"El stock resultante supera el máximo admitido ({int.MaxValue}).");
        }

        Stock = (int)newStock;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("El nombre del producto es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new BusinessRuleException($"El nombre no puede superar los {MaxNameLength} caracteres.");
        }

        return trimmed;
    }

    private static string NormalizeDescription(string? description)
    {
        var trimmed = (description ?? string.Empty).Trim();

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new BusinessRuleException($"La descripción no puede superar los {MaxDescriptionLength} caracteres.");
        }

        return trimmed;
    }

    private static decimal NormalizePrice(decimal price)
    {
        if (price < 0)
        {
            throw new BusinessRuleException("El precio no puede ser negativo.");
        }

        if (price > MaxPrice)
        {
            throw new BusinessRuleException($"El precio no puede superar {MaxPrice:N2}.");
        }

        if (decimal.Round(price, 2) != price)
        {
            throw new BusinessRuleException("El precio admite máximo 2 decimales.");
        }

        // Sumar 0.00 fuerza la escala a 2 decimales sin alterar el valor, de modo que
        // la API siempre devuelve el precio con el mismo formato (1000 -> 1000.00).
        return price + 0.00m;
    }

    private static int NormalizeInitialStock(int initialStock)
    {
        if (initialStock < 0)
        {
            throw new BusinessRuleException("El stock inicial no puede ser negativo.");
        }

        return initialStock;
    }
}
