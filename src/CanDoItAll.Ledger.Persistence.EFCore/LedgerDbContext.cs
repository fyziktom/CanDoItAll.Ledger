using CanDoItAll.Ledger.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Persistence.EFCore;

public sealed class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options)
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
    }
}
