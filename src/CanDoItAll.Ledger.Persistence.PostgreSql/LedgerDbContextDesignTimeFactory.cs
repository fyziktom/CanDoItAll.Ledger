using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Ledger.Persistence.PostgreSql;

public sealed class LedgerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=candoitall_ledger_design;Username=cdaledger;Password=cdaledger";
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(LedgerDbContextDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new LedgerDbContext(options);
    }
}
