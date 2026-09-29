using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Application.Contracts;

/// <summary>
/// Movimiento de inventario sobre un producto.
/// </summary>
public class AdjustStockRequest
{
    /// <summary>
    /// Unidades a aplicar sobre el stock actual: un valor positivo ingresa
    /// mercancía y uno negativo la descuenta. El cero no es un movimiento válido.
    /// </summary>
    /// <example>-3</example>
    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    public int? Quantity { get; init; }
}
