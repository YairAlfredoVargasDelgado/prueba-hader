using ProductCatalog.Application.Abstractions;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Tests.Doubles;

/// <summary>
/// Repositorio en memoria que sustituye a MySQL en las pruebas de la capa de
/// aplicación. Se escribe a mano en lugar de usar una librería de mocks para que
/// las pruebas queden legibles y sin dependencias extra.
/// </summary>
public class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<int, Product> _products = new();
    private int _nextId = 1;

    public Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        var stored = Product.Rehydrate(
            _nextId++,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);

        _products[stored.Id] = stored;

        return Task.FromResult(stored);
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_products.GetValueOrDefault(id));
    }

    public Task<(IReadOnlyList<Product> Items, int TotalItems)> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _products.Values.AsEnumerable();

        if (search is not null)
        {
            query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var matches = query.OrderByDescending(p => p.Id).ToList();

        IReadOnlyList<Product> items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult((items, matches.Count));
    }

    public Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (!_products.ContainsKey(product.Id))
        {
            return Task.FromResult(false);
        }

        _products[product.Id] = product;

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_products.Remove(id));
    }

    public Task<Product?> AdjustStockAsync(int id, int quantity, CancellationToken cancellationToken = default)
    {
        if (!_products.TryGetValue(id, out var product))
        {
            return Task.FromResult<Product?>(null);
        }

        // Igual que la implementación real: la regla la aplica el dominio.
        product.AdjustStock(quantity);

        return Task.FromResult<Product?>(product);
    }
}
