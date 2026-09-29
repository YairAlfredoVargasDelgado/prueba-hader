using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace ProductCatalog.Infrastructure.Persistence;

/// <summary>
/// Prepara la base de datos al arrancar la API: crea el esquema si no existe.
/// </summary>
/// <remarks>
/// Existe para que el proyecto sea reproducible sin pasos manuales: basta con
/// tener un MySQL accesible y la API deja el esquema listo. El script embebido es
/// idempotente, así que ejecutarlo en cada arranque es seguro. Además reintenta,
/// porque al levantar con Docker Compose la API suele estar lista antes que MySQL.
/// </remarks>
public class DatabaseInitializer
{
    private const string ScriptResourceName = "ProductCatalog.Infrastructure.Scripts.schema.sql";

    /// <summary>Solo se aceptan nombres de base de datos con caracteres seguros.</summary>
    private static readonly Regex SafeDatabaseName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Crea la base de datos y las tablas si hacen falta.
    /// </summary>
    /// <param name="maxAttempts">Intentos antes de darse por vencido.</param>
    /// <param name="delayBetweenAttempts">Espera entre intentos.</param>
    public async Task InitializeAsync(
        int maxAttempts = 10,
        TimeSpan? delayBetweenAttempts = null,
        CancellationToken cancellationToken = default)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(3);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await EnsureDatabaseExistsAsync(cancellationToken);
                await RunSchemaScriptAsync(cancellationToken);

                _logger.LogInformation("Esquema de base de datos verificado correctamente.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo preparar la base de datos (intento {Attempt} de {MaxAttempts}). Reintentando en {Delay}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Se conecta al servidor sin seleccionar esquema para poder crearlo si falta.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("La cadena de conexión no indica una base de datos.");
        }

        if (!SafeDatabaseName.IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                $"El nombre de base de datos '{databaseName}' contiene caracteres no permitidos.");
        }

        builder.Database = string.Empty;

        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` " +
                  "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RunSchemaScriptAsync(CancellationToken cancellationToken)
    {
        var script = ReadEmbeddedScript();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ReadEmbeddedScript()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(ScriptResourceName)
                           ?? throw new InvalidOperationException(
                               $"No se encontró el recurso embebido '{ScriptResourceName}'.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
