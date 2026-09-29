using ProductCatalog.Application.Contracts;
using ProductCatalog.Application.Services;
using ProductCatalog.Domain.Exceptions;
using ProductCatalog.Tests.Doubles;

namespace ProductCatalog.Tests.Application;

/// <summary>
/// Pruebas de los casos de uso contra un repositorio en memoria. Verifican la
/// orquestación —qué se guarda, qué se devuelve y qué excepción se propaga—
/// sin necesidad de una base de datos real.
/// </summary>
public class ProductServiceTests
{
    private readonly InMemoryProductRepository _repository = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(_repository);
    }

    [Fact]
    public async Task CreateAsync_DevuelveElProductoConSuIdentificador()
    {
        var created = await _service.CreateAsync(NewProduct("Teclado", stock: 5));

        Assert.True(created.Id > 0);
        Assert.Equal("Teclado", created.Name);
        Assert.Equal(5, created.Stock);
    }

    [Fact]
    public async Task CreateAsync_SinPrecio_Falla()
    {
        var request = new CreateProductRequest { Name = "Teclado", Price = null, Stock = 1 };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task GetByIdAsync_CuandoNoExiste_LanzaProductNotFound()
    {
        await Assert.ThrowsAsync<ProductNotFoundException>(() => _service.GetByIdAsync(404));
    }

    [Fact]
    public async Task ListAsync_CalculaLosMetadatosDePaginacion()
    {
        for (var i = 1; i <= 7; i++)
        {
            await _service.CreateAsync(NewProduct($"Producto {i}"));
        }

        var page = await _service.ListAsync(new ProductQuery { Page = 2, PageSize = 3 });

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(7, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task ListAsync_UltimaPagina_DevuelveElResto()
    {
        for (var i = 1; i <= 7; i++)
        {
            await _service.CreateAsync(NewProduct($"Producto {i}"));
        }

        var page = await _service.ListAsync(new ProductQuery { Page = 3, PageSize = 3 });

        Assert.Single(page.Items);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task ListAsync_FiltraPorNombre()
    {
        await _service.CreateAsync(NewProduct("Teclado mecánico"));
        await _service.CreateAsync(NewProduct("Mouse inalámbrico"));

        var page = await _service.ListAsync(new ProductQuery { Search = "teclado" });

        Assert.Equal(1, page.TotalItems);
        Assert.Equal("Teclado mecánico", page.Items[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_ActualizaLosDatosSinTocarElStock()
    {
        var created = await _service.CreateAsync(NewProduct("Teclado", stock: 9));

        var updated = await _service.UpdateAsync(created.Id, new UpdateProductRequest
        {
            Name = "Teclado Pro",
            Description = "Switch rojo",
            Price = 250000m
        });

        Assert.Equal("Teclado Pro", updated.Name);
        Assert.Equal(250000m, updated.Price);
        Assert.Equal(9, updated.Stock);
    }

    [Fact]
    public async Task UpdateAsync_CuandoNoExiste_LanzaProductNotFound()
    {
        var request = new UpdateProductRequest { Name = "Teclado", Price = 1000m };

        await Assert.ThrowsAsync<ProductNotFoundException>(() => _service.UpdateAsync(404, request));
    }

    [Fact]
    public async Task DeleteAsync_EliminaElProducto()
    {
        var created = await _service.CreateAsync(NewProduct("Teclado"));

        await _service.DeleteAsync(created.Id);

        await Assert.ThrowsAsync<ProductNotFoundException>(() => _service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_CuandoNoExiste_LanzaProductNotFound()
    {
        await Assert.ThrowsAsync<ProductNotFoundException>(() => _service.DeleteAsync(404));
    }

    [Fact]
    public async Task AdjustStockAsync_DescuentaUnidades()
    {
        var created = await _service.CreateAsync(NewProduct("Teclado", stock: 10));

        var adjusted = await _service.AdjustStockAsync(created.Id, new AdjustStockRequest { Quantity = -4 });

        Assert.Equal(6, adjusted.Stock);
    }

    [Fact]
    public async Task AdjustStockAsync_CuandoDejariaElStockNegativo_LanzaInsufficientStock()
    {
        var created = await _service.CreateAsync(NewProduct("Teclado", stock: 3));

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => _service.AdjustStockAsync(created.Id, new AdjustStockRequest { Quantity = -4 }));

        var unchanged = await _service.GetByIdAsync(created.Id);
        Assert.Equal(3, unchanged.Stock);
    }

    [Fact]
    public async Task AdjustStockAsync_CuandoNoExiste_LanzaProductNotFound()
    {
        var request = new AdjustStockRequest { Quantity = 1 };

        await Assert.ThrowsAsync<ProductNotFoundException>(() => _service.AdjustStockAsync(404, request));
    }

    private static CreateProductRequest NewProduct(string name, int stock = 1) => new()
    {
        Name = name,
        Description = "Producto de prueba",
        Price = 1000m,
        Stock = stock
    };
}
