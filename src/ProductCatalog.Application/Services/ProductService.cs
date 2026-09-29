using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Contracts;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Application.Services;

/// <summary>
/// Implementación de los casos de uso del catálogo.
/// Orquesta: traduce contratos a dominio, delega las reglas en la entidad,
/// pide la persistencia al repositorio y proyecta el resultado de vuelta.
/// No contiene reglas de negocio propias ni conoce el motor de base de datos.
/// </summary>
public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var price = request.Price ?? throw new BusinessRuleException("El precio es obligatorio.");
        var stock = request.Stock ?? throw new BusinessRuleException("El stock inicial es obligatorio.");

        var product = Product.Create(request.Name ?? string.Empty, request.Description, price, stock);
        var created = await _repository.AddAsync(product, cancellationToken);

        return ProductResponse.FromDomain(created);
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken)
                      ?? throw new ProductNotFoundException(id);

        return ProductResponse.FromDomain(product);
    }

    public async Task<PagedResponse<ProductResponse>> ListAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, totalItems) = await _repository.ListAsync(
            query.Page,
            query.PageSize,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            cancellationToken);

        var responses = items.Select(ProductResponse.FromDomain).ToList();

        return new PagedResponse<ProductResponse>(responses, query.Page, query.PageSize, totalItems);
    }

    public async Task<ProductResponse> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var price = request.Price ?? throw new BusinessRuleException("El precio es obligatorio.");

        var product = await _repository.GetByIdAsync(id, cancellationToken)
                      ?? throw new ProductNotFoundException(id);

        product.UpdateDetails(request.Name ?? string.Empty, request.Description, price);

        var updated = await _repository.UpdateAsync(product, cancellationToken);

        if (!updated)
        {
            // La fila desapareció entre la lectura y la escritura.
            throw new ProductNotFoundException(id);
        }

        return ProductResponse.FromDomain(product);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            throw new ProductNotFoundException(id);
        }
    }

    public async Task<ProductResponse> AdjustStockAsync(
        int id,
        AdjustStockRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quantity = request.Quantity ?? throw new BusinessRuleException("La cantidad es obligatoria.");

        var product = await _repository.AdjustStockAsync(id, quantity, cancellationToken)
                      ?? throw new ProductNotFoundException(id);

        return ProductResponse.FromDomain(product);
    }
}
