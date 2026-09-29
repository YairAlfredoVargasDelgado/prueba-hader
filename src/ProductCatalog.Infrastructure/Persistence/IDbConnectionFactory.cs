using MySqlConnector;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>
/// Abre conexiones contra MySQL. Se aísla en una fábrica para que el repositorio
/// no conozca la cadena de conexión y para poder sustituirla en pruebas.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Crea y abre una conexión. Quien la recibe es responsable de liberarla.</summary>
    Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
