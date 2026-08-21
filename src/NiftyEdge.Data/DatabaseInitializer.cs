using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace NiftyEdge.Data;

/// <summary>
/// Applies the embedded SQL scripts (schema, table types, stored procedures, seed data) against the
/// configured database on startup. Idempotent: every script uses IF NOT EXISTS / CREATE OR ALTER, so
/// running it repeatedly is safe.
/// </summary>
public class DatabaseInitializer
{
    private static readonly Regex GoBatchSeparator = new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        var scriptResourceNames = GetOrderedScriptResourceNames(assembly);

        await EnsureDatabaseExistsAsync(cancellationToken);

        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var resourceName in scriptResourceNames)
        {
            var script = ReadEmbeddedScript(assembly, resourceName);
            foreach (var batch in SplitIntoBatches(script))
            {
                using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogInformation("Applied database script {ScriptName}", resourceName);
        }
    }

    /// <summary>Creates the target database on first run, connecting to `master` on the same server.
    /// Lets a brand-new LocalDB instance work out of the box without a manual setup step.</summary>
    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        using var probeConnection = (SqlConnection)_connectionFactory.CreateConnection();
        var targetBuilder = new SqlConnectionStringBuilder(probeConnection.ConnectionString);
        var databaseName = targetBuilder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        var masterBuilder = new SqlConnectionStringBuilder(targetBuilder.ConnectionString) { InitialCatalog = "master" };

        using var masterConnection = new SqlConnection(masterBuilder.ConnectionString);
        await masterConnection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(
            $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{databaseName.Replace("'", "''")}') " +
            $"CREATE DATABASE [{databaseName}];",
            masterConnection);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Verified database {DatabaseName} exists.", databaseName);
    }

    private static List<string> GetOrderedScriptResourceNames(Assembly assembly)
    {
        var allSqlResources = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var schemaAndTypes = allSqlResources
            .Where(n =>
                n.Contains(".Sql.01_", StringComparison.OrdinalIgnoreCase) ||
                n.Contains(".Sql.02_", StringComparison.OrdinalIgnoreCase) ||
                n.Contains(".Sql.05_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var storedProcedures = allSqlResources
            .Where(n => n.Contains(".StoredProcedures.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seedData = allSqlResources
            .Where(n =>
                n.Contains(".Sql.04_", StringComparison.OrdinalIgnoreCase) ||
                n.Contains(".Sql.06_", StringComparison.OrdinalIgnoreCase) ||
                n.Contains(".Sql.07_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return schemaAndTypes.Concat(storedProcedures).Concat(seedData).ToList();
    }

    private static string ReadEmbeddedScript(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitIntoBatches(string script)
    {
        return GoBatchSeparator.Split(script)
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0);
    }
}
