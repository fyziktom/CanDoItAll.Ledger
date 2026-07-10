using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Persistence.EFCore;

public interface IEconomySchemaMigrator
{
    Task<EconomySchemaMigrationResult> MigrateAsync(
        EconomyDbContext db,
        CancellationToken cancellationToken);
}

public sealed record EconomySchemaMigrationResult(
    bool BaselineAdopted,
    IReadOnlyList<string> AvailableMigrations,
    IReadOnlyList<string> AppliedMigrations);

public sealed class EconomySchemaMigrationException(string message) : InvalidOperationException(message);

internal sealed class EfEconomySchemaMigrator : IEconomySchemaMigrator
{
    public async Task<EconomySchemaMigrationResult> MigrateAsync(
        EconomyDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        string[] available = db.Database.GetMigrations().ToArray();
        string[] applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        return new EconomySchemaMigrationResult(false, available, applied);
    }
}
