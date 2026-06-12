using Npgsql;

namespace CanDoItAll.Economy.Tests;

internal static class PostgreSqlTestDatabase
{
    public static string CreateConnectionString(string baseConnectionString, string suffix)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);

        string baseName = string.IsNullOrWhiteSpace(builder.Database)
            ? "candoitall_economy_test"
            : builder.Database;
        builder.Database = $"{Normalize(baseName, 12)}_{Normalize(suffix, 16)}_{Guid.NewGuid():N}";
        return builder.ConnectionString;
    }

    private static string Normalize(string value, int maxLength)
    {
        char[] normalized = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        string databaseName = new string(normalized).Trim('_');
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return "cda";
        }

        return databaseName.Length <= maxLength
            ? databaseName
            : databaseName[..maxLength].Trim('_');
    }
}
