using System.Data;
using MySqlConnector;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Domain.Entities;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>
/// Repositorio de productos sobre MySQL usando ADO.NET puro (sin ORM).
/// Todas las sentencias usan parámetros, de modo que no hay concatenación de
/// valores en el SQL y la inyección de SQL queda descartada por construcción.
/// </summary>
public class ProductRepository : IProductRepository
{
    private const string ColumnList = "id, name, description, price, stock, created_at, updated_at";

    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO products (name, description, price, stock, created_at, updated_at)
            VALUES (@name, @description, @price, @stock, @createdAt, @updatedAt);
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@name", product.Name);
        command.Parameters.AddWithValue("@description", product.Description);
        command.Parameters.AddWithValue("@price", product.Price);
        command.Parameters.AddWithValue("@stock", product.Stock);
        command.Parameters.AddWithValue("@createdAt", product.CreatedAtUtc);
        command.Parameters.AddWithValue("@updatedAt", product.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);

        // La base asigna el identificador; se reconstruye la entidad ya persistida.
        return Product.Rehydrate(
            (int)command.LastInsertedId,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {ColumnList} FROM products WHERE id = @id;";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalItems)> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var filter = search is null ? string.Empty : " WHERE name LIKE @search";

        // Se ordena por id descendente (lo más reciente primero). Al ser la clave
        // primaria, el orden es estable y la paginación no repite ni omite filas.
        var pageSql = $"SELECT {ColumnList} FROM products{filter} ORDER BY id DESC LIMIT @limit OFFSET @offset;";
        var countSql = $"SELECT COUNT(*) FROM products{filter};";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var items = new List<Product>();

        await using (var pageCommand = new MySqlCommand(pageSql, connection))
        {
            pageCommand.Parameters.AddWithValue("@limit", pageSize);
            pageCommand.Parameters.AddWithValue("@offset", (page - 1) * (long)pageSize);

            if (search is not null)
            {
                pageCommand.Parameters.AddWithValue("@search", BuildLikePattern(search));
            }

            await using var reader = await pageCommand.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(Map(reader));
            }
        }

        await using var countCommand = new MySqlCommand(countSql, connection);

        if (search is not null)
        {
            countCommand.Parameters.AddWithValue("@search", BuildLikePattern(search));
        }

        var totalItems = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        return (items, totalItems);
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE products
               SET name        = @name,
                   description = @description,
                   price       = @price,
                   updated_at  = @updatedAt
             WHERE id = @id;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@name", product.Name);
        command.Parameters.AddWithValue("@description", product.Description);
        command.Parameters.AddWithValue("@price", product.Price);
        command.Parameters.AddWithValue("@updatedAt", product.UpdatedAtUtc);
        command.Parameters.AddWithValue("@id", product.Id);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affected > 0)
        {
            return true;
        }

        // MySQL informa 0 filas afectadas cuando el UPDATE no cambió ningún valor,
        // lo que no significa que el producto haya desaparecido. Se confirma.
        return await ExistsAsync(connection, product.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM products WHERE id = @id;";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// Ajusta el stock dentro de una transacción con bloqueo de fila.
    /// </summary>
    /// <remarks>
    /// El <c>SELECT ... FOR UPDATE</c> bloquea la fila hasta el commit, así que dos
    /// peticiones concurrentes sobre el mismo producto se serializan en lugar de leer
    /// ambas el mismo stock y pisarse (condición de carrera "lost update"). La
    /// validación de que el resultado no sea negativo la aplica la entidad de dominio;
    /// si falla, la excepción propaga y la transacción se deshace al liberarse.
    /// </remarks>
    public async Task<Product?> AdjustStockAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var selectSql = $"SELECT {ColumnList} FROM products WHERE id = @id FOR UPDATE;";

        const string updateSql = """
            UPDATE products
               SET stock      = @stock,
                   updated_at = @updatedAt
             WHERE id = @id;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        Product? product;

        await using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
        {
            selectCommand.Parameters.AddWithValue("@id", id);

            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            product = await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }

        if (product is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        // Regla de negocio: puede lanzar InsufficientStockException y abortar la transacción.
        product.AdjustStock(quantity);

        await using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("@stock", product.Stock);
            updateCommand.Parameters.AddWithValue("@updatedAt", product.UpdatedAtUtc);
            updateCommand.Parameters.AddWithValue("@id", product.Id);

            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return product;
    }

    private static async Task<bool> ExistsAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM products WHERE id = @id;", connection);
        command.Parameters.AddWithValue("@id", id);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static Product Map(MySqlDataReader reader) => Product.Rehydrate(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetDecimal(3),
        reader.GetInt32(4),
        reader.GetDateTime(5),
        reader.GetDateTime(6));

    /// <summary>
    /// Convierte el texto buscado en un patrón LIKE, neutralizando los comodines
    /// que el usuario pudiera enviar para que se busquen de forma literal.
    /// </summary>
    private static string BuildLikePattern(string search)
    {
        var escaped = search
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
