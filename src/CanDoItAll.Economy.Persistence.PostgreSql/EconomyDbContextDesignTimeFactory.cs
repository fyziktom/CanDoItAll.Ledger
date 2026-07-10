using CanDoItAll.Economy.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Economy.Persistence.PostgreSql;

public sealed class EconomyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EconomyDbContext>
{
    public EconomyDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=candoitall_economy_design;Username=cdaeconomy;Password=cdaeconomy";
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(EconomyDbContextDesignTimeFactory).Assembly.GetName().Name))
            .Options;
        return new EconomyDbContext(options);
    }
}
