using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerPersistenceConstraintTests
{
    [Fact]
    public async Task SqliteSchema_RejectsOutputThatMixesMainCurrencyAndToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new EconomyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.TransactionOutputs.Add(new LedgerTransactionOutputRecord
        {
            TxId = "invalid-mixed-output",
            OutputIndex = 0,
            Address = "addr_test",
            MainAmount = 1m,
            TokenId = "test-token",
            TokenAmount = 1m
        });

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        Assert.Contains("CK_Economy_TxOutput_AssetSeparation", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SqliteSchema_RejectsEmptyUnspentOutput()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new EconomyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.UnspentOutputs.Add(new UnspentOutputRecord
        {
            OutPointKey = new string('1', 64),
            LedgerId = "hardening",
            TxId = "invalid-empty-utxo",
            OutputIndex = 0,
            Address = "addr_test",
            MainAmount = 0m
        });

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        Assert.Contains("CK_Economy_Utxo_HasValue", error.InnerException?.Message ?? error.Message, StringComparison.Ordinal);
    }
}
