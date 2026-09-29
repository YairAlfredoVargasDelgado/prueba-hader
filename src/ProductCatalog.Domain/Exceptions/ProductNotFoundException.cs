namespace ProductCatalog.Domain.Exceptions;

/// <summary>
/// Se lanza cuando se referencia un producto que no existe.
/// La API la traduce a un <c>404 Not Found</c>.
/// </summary>
public class ProductNotFoundException : DomainException
{
    public ProductNotFoundException(int productId)
        : base($"No existe un producto con el identificador {productId}.")
    {
        ProductId = productId;
    }

    public int ProductId { get; }
}
