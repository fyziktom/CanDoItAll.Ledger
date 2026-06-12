using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Persistence.EFCore;

public sealed class EconomyDbContext : DbContext
{
    public EconomyDbContext(DbContextOptions<EconomyDbContext> options)
        : base(options)
    {
    }

    public DbSet<LedgerStateRecord> LedgerStates => Set<LedgerStateRecord>();

    public DbSet<LedgerTransactionRecord> Transactions => Set<LedgerTransactionRecord>();

    public DbSet<LedgerTransactionInputRecord> TransactionInputs => Set<LedgerTransactionInputRecord>();

    public DbSet<LedgerTransactionOutputRecord> TransactionOutputs => Set<LedgerTransactionOutputRecord>();

    public DbSet<UnspentOutputRecord> UnspentOutputs => Set<UnspentOutputRecord>();

    public DbSet<SpentOutputRecord> SpentOutputs => Set<SpentOutputRecord>();

    public DbSet<AddressStateProjectionRecord> AddressStateProjections => Set<AddressStateProjectionRecord>();

    public DbSet<LedgerTokenTypeRecord> LedgerTokenTypes => Set<LedgerTokenTypeRecord>();

    public DbSet<RegisteredAddressRecord> RegisteredAddresses => Set<RegisteredAddressRecord>();

    public DbSet<BusinessObjectRecord> BusinessObjects => Set<BusinessObjectRecord>();

    public DbSet<BusinessObjectVersionRecord> BusinessObjectVersions => Set<BusinessObjectVersionRecord>();

    public DbSet<LedgerSnapshotRecord> LedgerSnapshots => Set<LedgerSnapshotRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EconomyDbContext).Assembly);
    }
}
