using MySqlConnector;

namespace ProductCatalog.Infrastructure.Persistence;

/// <inheritdoc cref="IDbConnectionFactory"/>
public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "La cadena de conexión no está configurada. Defina ConnectionStrings:ProductCatalog.",
                nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
