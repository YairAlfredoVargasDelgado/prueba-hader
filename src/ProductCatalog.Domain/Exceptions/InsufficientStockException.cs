namespace ProductCatalog.Domain.Exceptions;

/// <summary>
/// Se lanza cuando un movimiento de inventario dejaría el stock en negativo.
/// No es un error de formato sino un conflicto con el estado actual del producto,
/// por eso la API la traduce a un <c>409 Conflict</c>.
/// </summary>
public class InsufficientStockException : DomainException
{
    public InsufficientStockException(int currentStock, int requestedChange)
        : base($"Stock insuficiente: el producto tiene {currentStock} unidades y se solicitó un ajuste de {requestedChange}.")
    {
        CurrentStock = currentStock;
        RequestedChange = requestedChange;
    }

    /// <summary>Unidades disponibles en el momento de intentar el ajuste.</summary>
    public int CurrentStock { get; }

    /// <summary>Ajuste solicitado (positivo suma, negativo resta).</summary>
    public int RequestedChange { get; }
}
