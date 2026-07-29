using System.Data.Common;
using System.Data;
using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Ledger.Persistence.PostgreSql;

public sealed class PostgreSqlLedgerSchemaMigrator : ILedgerSchemaMigrator
{
    public const string LegacyBaselineMigrationId = "20260710114728_InitialLegacySchemaBaseline";
    private const string ProductVersion = "10.0.4";
    private const string ProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly string[] RequiredTables =
    [
        "Economy_AddressStateProjections",
        "Economy_BusinessObjects",
        "Economy_BusinessObjectVersions",
        "Economy_LedgerSnapshots",
        "Economy_LedgerStates",
        "Economy_LedgerTokenTypes",
        "Economy_LedgerTransactionInputs",
        "Economy_LedgerTransactionOutputs",
        "Economy_LedgerTransactions",
        "Economy_RegisteredAddresses",
        "Economy_SpentOutputs",
        "Economy_UnspentOutputs"
    ];

    public async Task<LedgerSchemaMigrationResult> MigrateAsync(
        LedgerDbContext db,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(db.Database.ProviderName, ProviderName, StringComparison.Ordinal))
        {
            throw new LedgerSchemaMigrationException(
                $"{nameof(PostgreSqlLedgerSchemaMigrator)} requires the PostgreSQL EF provider, found '{db.Database.ProviderName ?? "unknown"}'.");
        }

        bool openedHere = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (openedHere)
        {
            await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        await ExecuteNonQueryAsync(
                db,
                "SELECT pg_advisory_lock(hashtext('candoitall.economy.schema-migration'))",
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            bool baselineAdopted = await AdoptExistingSchemaIfNeededAsync(db, cancellationToken).ConfigureAwait(false);
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            string[] available = db.Database.GetMigrations().ToArray();
            string[] applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            if (!available.SequenceEqual(applied, StringComparer.Ordinal))
            {
                throw new LedgerSchemaMigrationException(
                    $"PostgreSQL migration did not reach the current schema. Available=[{string.Join(",", available)}], Applied=[{string.Join(",", applied)}].");
            }

            return new LedgerSchemaMigrationResult(baselineAdopted, available, applied);
        }
        finally
        {
            try
            {
                await ExecuteNonQueryAsync(
                        db,
                        "SELECT pg_advisory_unlock(hashtext('candoitall.economy.schema-migration'))",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (openedHere)
                {
                    await db.Database.CloseConnectionAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<bool> AdoptExistingSchemaIfNeededAsync(
        LedgerDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool hasLedgerSchema = await RelationExistsAsync(db, "Economy_LedgerStates", cancellationToken).ConfigureAwait(false);
            if (!hasLedgerSchema)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            bool baselineApplied = await MigrationHistoryContainsAsync(db, LegacyBaselineMigrationId, cancellationToken).ConfigureAwait(false);
            if (baselineApplied)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            await AuditExistingSchemaAsync(db, cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                    db,
                    "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) NOT NULL, \"ProductVersion\" character varying(32) NOT NULL, CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\"))",
                    cancellationToken)
                .ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                    db,
                    $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{LegacyBaselineMigrationId}', '{ProductVersion}') ON CONFLICT (\"MigrationId\") DO NOTHING",
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task AuditExistingSchemaAsync(LedgerDbContext db, CancellationToken cancellationToken)
    {
        var missingTables = new List<string>();
        foreach (string table in RequiredTables)
        {
            if (!await RelationExistsAsync(db, table, cancellationToken).ConfigureAwait(false))
            {
                missingTables.Add(table);
            }
        }

        if (missingTables.Count > 0)
        {
            throw new LedgerSchemaMigrationException(
                $"Existing ledger schema is incomplete; baseline adoption refused. Missing tables: {string.Join(", ", missingTables)}.");
        }

        (string Name, string Sql)[] checks =
        [
            ("transaction output negative main", "SELECT count(*) FROM \"Economy_LedgerTransactionOutputs\" WHERE \"MainAmount\" < 0"),
            ("transaction output token pair", "SELECT count(*) FROM \"Economy_LedgerTransactionOutputs\" WHERE (\"TokenId\" IS NULL) <> (\"TokenAmount\" IS NULL)"),
            ("transaction output non-positive token", "SELECT count(*) FROM \"Economy_LedgerTransactionOutputs\" WHERE \"TokenAmount\" <= 0"),
            ("transaction output mixed main/token", "SELECT count(*) FROM \"Economy_LedgerTransactionOutputs\" WHERE \"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL"),
            ("transaction output without value", "SELECT count(*) FROM \"Economy_LedgerTransactionOutputs\" WHERE \"MainAmount\" <= 0 AND \"TokenId\" IS NULL"),
            ("UTXO negative main", "SELECT count(*) FROM \"Economy_UnspentOutputs\" WHERE \"MainAmount\" < 0"),
            ("UTXO token pair", "SELECT count(*) FROM \"Economy_UnspentOutputs\" WHERE (\"TokenId\" IS NULL) <> (\"TokenAmount\" IS NULL)"),
            ("UTXO non-positive token", "SELECT count(*) FROM \"Economy_UnspentOutputs\" WHERE \"TokenAmount\" <= 0"),
            ("UTXO mixed main/token", "SELECT count(*) FROM \"Economy_UnspentOutputs\" WHERE \"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL"),
            ("UTXO without value", "SELECT count(*) FROM \"Economy_UnspentOutputs\" WHERE \"MainAmount\" <= 0 AND \"TokenId\" IS NULL")
        ];

        var violations = new List<string>();
        foreach ((string name, string sql) in checks)
        {
            long count = await ExecuteScalarInt64Async(db, sql, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                violations.Add($"{name}={count}");
            }
        }

        if (violations.Count > 0)
        {
            throw new LedgerSchemaMigrationException(
                $"Existing ledger data violates current invariants; baseline adoption refused: {string.Join(", ", violations)}.");
        }
    }

    private static async Task<bool> RelationExistsAsync(LedgerDbContext db, string relation, CancellationToken cancellationToken)
    {
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT to_regclass(@relation) IS NOT NULL";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "relation";
        parameter.Value = $"\"{relation}\"";
        command.Parameters.Add(parameter);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<bool> MigrationHistoryContainsAsync(
        LedgerDbContext db,
        string migrationId,
        CancellationToken cancellationToken)
    {
        if (!await RelationExistsAsync(db, "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @migrationId)";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "migrationId";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task<long> ExecuteScalarInt64Async(
        LedgerDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteNonQueryAsync(
        LedgerDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
