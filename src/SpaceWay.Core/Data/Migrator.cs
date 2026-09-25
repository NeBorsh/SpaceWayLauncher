using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;

namespace SpaceWay.Core.Data;

/// <summary>
/// Applies schema migrations.
/// </summary>
public static class Migrator
{
    /// <summary>Settings database scripts.</summary>
    public const string LauncherScripts = "SpaceWay.Core.Data.Migrations.";

    /// <summary>Content database scripts.</summary>
    public const string ContentScripts = "SpaceWay.Core.Content.Migrations.";

    public static void Migrate(SqliteConnection connection, string resourcePrefix = LauncherScripts)
    {
        var current = connection.ExecuteScalar<long>("PRAGMA user_version");
        var scripts = GetScripts(resourcePrefix);

        if (current > scripts.Count)
        {
            throw new InvalidOperationException(
                $"Database was created by a newer launcher version (schema {current}, supported {scripts.Count})");
        }

        if (current == scripts.Count)
            return;

        Log.Information("Migrating database: schema {From} → {To}", current, scripts.Count);

        using var transaction = connection.BeginTransaction();

        for (var version = (int)current; version < scripts.Count; version++)
        {
            var (name, sql) = scripts[version];
            Log.Debug("Applying {Script}", name);
            connection.Execute(sql, transaction: transaction);
        }

        connection.Execute($"PRAGMA user_version = {scripts.Count}", transaction: transaction);
        transaction.Commit();
    }

    /// <summary>
    /// Scripts in application order. The file name defines the order,
    /// so numbering is required: Script0000_, Script0001_ and so on.
    /// </summary>
    private static List<(string Name, string Sql)> GetScripts(string resourcePrefix)
    {
        var assembly = Assembly.GetExecutingAssembly();

        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => (Name: n[resourcePrefix.Length..], Sql: ReadResource(assembly, n)))
            .ToList();
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Missing resource{name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
