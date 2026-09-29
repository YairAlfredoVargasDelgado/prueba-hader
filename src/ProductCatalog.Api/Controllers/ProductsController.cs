using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Contracts;

namespace ProductCatalog.Api.Controllers;

/// <summary>
/// Gestión del catálogo de productos y de su inventario.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Lista los productos del catálogo de forma paginada.
    /// </summary>
    /// <remarks>
    /// Los resultados se ordenan del más reciente al más antiguo. El parámetro
    /// <c>search</c> filtra por coincidencia parcial en el nombre.
    ///
    /// Ejemplo: <c>GET /api/products?page=1&amp;pageSize=10&amp;search=teclado</c>
    /// </remarks>
    /// <param name="query">Página, tamaño de página y filtro opcional por nombre.</param>
    /// <response code="200">Página de productos solicitada.</response>
    /// <response code="400">Los parámetros de paginación no son válidos.</response>
    [HttpGet(Name = "ListProducts")]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> List(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _productService.ListAsync(query, cancellationToken));
    }

    /// <summary>
    /// Consulta un producto por su identificador.
    /// </summary>
    /// <param name="id">Identificador del producto.</param>
    /// <response code="200">Producto encontrado.</response>
    /// <response code="404">No existe un producto con ese identificador.</response>
    [HttpGet("{id:int}", Name = "GetProductById")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _productService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Crea un producto en el catálogo.
    /// </summary>
    /// <remarks>
    /// Cuerpo de ejemplo:
    ///
    ///     POST /api/products
    ///     {
    ///       "name": "Teclado mecánico RGB",
    ///       "description": "Switch azul, retroiluminado, layout español.",
    ///       "price": 189900.00,
    ///       "stock": 25
    ///     }
    ///
    /// La respuesta incluye la cabecera <c>Location</c> con la URL del producto creado.
    /// </remarks>
    /// <param name="request">Datos del producto a crear.</param>
    /// <response code="201">Producto creado.</response>
    /// <response code="400">Los datos enviados no superan las validaciones.</response>
    [HttpPost(Name = "CreateProduct")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateAsync(request, cancellationToken);

        return CreatedAtRoute("GetProductById", new { id = product.Id }, product);
    }

    /// <summary>
    /// Actualiza los datos descriptivos de un producto.
    /// </summary>
    /// <remarks>
    /// El stock no se modifica por este endpoint: los movimientos de inventario
    /// se realizan en <c>PATCH /api/products/{id}/stock</c>, que es el único camino
    /// con control de concurrencia.
    /// </remarks>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Nuevos datos descriptivos.</param>
    /// <response code="200">Producto actualizado.</response>
    /// <response code="400">Los datos enviados no superan las validaciones.</response>
    /// <response code="404">No existe un producto con ese identificador.</response>
    [HttpPut("{id:int}", Name = "UpdateProduct")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _productService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Elimina un producto del catálogo.
    /// </summary>
    /// <param name="id">Identificador del producto.</param>
    /// <response code="204">Producto eliminado; no hay contenido que devolver.</response>
    /// <response code="404">No existe un producto con ese identificador.</response>
    [HttpDelete("{id:int}", Name = "DeleteProduct")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _productService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Suma o resta unidades al stock de un producto.
    /// </summary>
    /// <remarks>
    /// Se usa PATCH porque la operación es un ajuste relativo sobre el stock actual,
    /// no el reemplazo del recurso completo que exigiría PUT.
    ///
    /// Un valor positivo ingresa mercancía y uno negativo la descuenta:
    ///
    ///     PATCH /api/products/1/stock
    ///     { "quantity": -3 }
    ///
    /// La operación es atómica: la fila se bloquea dentro de una transacción, por lo
    /// que varias plataformas pueden descontar inventario a la vez sin perder ajustes.
    /// </remarks>
    /// <param name="id">Identificador del producto.</param>
    /// <param name="request">Unidades a sumar (positivo) o restar (negativo).</param>
    /// <response code="200">Stock actualizado; se devuelve el producto con su stock vigente.</response>
    /// <response code="400">La cantidad es cero, falta, o no es un entero válido.</response>
    /// <response code="404">No existe un producto con ese identificador.</response>
    /// <response code="409">El ajuste dejaría el stock en negativo.</response>
    [HttpPatch("{id:int}/stock", Name = "AdjustProductStock")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> AdjustStock(
        int id,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _productService.AdjustStockAsync(id, request, cancellationToken));
    }
}
