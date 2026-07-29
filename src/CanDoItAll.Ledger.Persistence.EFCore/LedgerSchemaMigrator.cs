using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Persistence.EFCore;

public interface ILedgerSchemaMigrator
{
    Task<LedgerSchemaMigrationResult> MigrateAsync(
        LedgerDbContext db,
        CancellationToken cancellationToken);
}

public sealed record LedgerSchemaMigrationResult(
    bool BaselineAdopted,
    IReadOnlyList<string> AvailableMigrations,
    IReadOnlyList<string> AppliedMigrations);

public sealed class LedgerSchemaMigrationException(string message) : InvalidOperationException(message);

internal sealed class EfLedgerSchemaMigrator : ILedgerSchemaMigrator
{
    public async Task<LedgerSchemaMigrationResult> MigrateAsync(
        LedgerDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        string[] available = db.Database.GetMigrations().ToArray();
        string[] applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        return new LedgerSchemaMigrationResult(false, available, applied);
    }
}
