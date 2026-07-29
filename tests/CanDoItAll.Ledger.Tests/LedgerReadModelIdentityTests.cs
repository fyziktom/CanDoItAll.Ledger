using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Node;
using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerReadModelIdentityTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("identity-ledger"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("identity-alice"));
    private static readonly DateTimeOffset ConfirmedAt = DateTimeOffset.Parse("2026-07-11T12:00:00Z");

    [Fact]
    public async Task AddressHoldings_ExposeOutPointId_WhileSpendablesRetainLegacyLineage()
    {
        string txId = new('a', 64);
        const int outputIndex = 3;
        const string legacyLineage = "bo:post:user-provided-name";
        const string dataHash = "payload-hash";
        string sourceId = $"{new string('b', 64)}:1";
        string payload = Payload(sourceId);
        TestDbContextFactory factory = CreateFactory();

        await using (LedgerDbContext db = factory.CreateDbContext())
        {
            db.Transactions.Add(Transaction(txId, 1, dataHash, payload));
            db.UnspentOutputs.Add(Unspent(txId, outputIndex, legacyLineage, dataHash));
            db.BusinessObjects.Add(CurrentProjection(txId, outputIndex, legacyLineage, dataHash));
            db.BusinessObjectVersions.Add(Version(txId, outputIndex, legacyLineage, dataHash));
            await db.SaveChangesAsync();
        }

        var service = new LedgerReadModelService(factory);
        LedgerBusinessObjectHolding holding = Assert.Single(await service.GetBusinessObjectHoldingsAsync(
            new LedgerAddressQuery(Context, Alice),
            CancellationToken.None));

        Assert.Equal($"{txId}:{outputIndex}", holding.ObjectId.Value);
        Assert.Equal(new OutPoint(new TxId(txId), outputIndex), holding.CurrentOutPoint);
        BusinessObjectDataReference reference = Assert.Single(holding.References!);
        Assert.Equal(BusinessObjectReferenceRelationships.CloneSource, reference.Relationship);
        Assert.Equal(sourceId, reference.ObjectId.Value);

        LedgerSpendableOutput spendable = Assert.Single(await service.GetSpendableOutputsAsync(
            new LedgerAddressQuery(Context, Alice),
            CancellationToken.None));
        Assert.Equal(legacyLineage, spendable.BusinessObject!.LineageKey.Value);
    }

    [Fact]
    public async Task BusinessObjectQuery_UsesCanonicalCurrentOutPoint_NotStoredLineage()
    {
        string txId = new('c', 64);
        const int outputIndex = 2;
        const string legacyLineage = "bo:image:old-display-name";
        const string dataHash = "current-hash";
        string sourceId = $"{new string('d', 64)}:7";
        TestDbContextFactory factory = CreateFactory();

        await using (LedgerDbContext db = factory.CreateDbContext())
        {
            db.Transactions.Add(Transaction(txId, 2, dataHash, Payload(sourceId)));
            db.BusinessObjects.Add(CurrentProjection(txId, outputIndex, legacyLineage, dataHash));
            db.BusinessObjectVersions.Add(Version(txId, outputIndex, legacyLineage, dataHash));
            await db.SaveChangesAsync();
        }

        var service = new LedgerReadModelService(factory);
        var canonicalId = new BusinessObjectId($"{txId}:{outputIndex}");
        LedgerBusinessObjectHolding holding = Assert.Single(await service.QueryBusinessObjectHoldingsAsync(
            new LedgerBusinessObjectQuery(Context, ObjectId: canonicalId),
            CancellationToken.None));

        Assert.Equal(canonicalId, holding.ObjectId);
        Assert.Equal(sourceId, Assert.Single(holding.References!).ObjectId.Value);

        await Assert.ThrowsAsync<FormatException>(() => service.QueryBusinessObjectHoldingsAsync(
            new LedgerBusinessObjectQuery(Context, ObjectId: new BusinessObjectId(legacyLineage)),
            CancellationToken.None));
    }

    [Fact]
    public async Task TransactionHistoryQuery_ResolvesCanonicalOutputToLineage_AndPublicDtoHidesLegacyLineage()
    {
        string firstTxId = new('e', 64);
        string currentTxId = new('f', 64);
        const string legacyLineage = "bo:post:stable-wire-correlation";
        TestDbContextFactory factory = CreateFactory();

        await using (LedgerDbContext db = factory.CreateDbContext())
        {
            db.Transactions.AddRange(
                Transaction(firstTxId, 1, "first-hash", null),
                Transaction(currentTxId, 2, "current-hash", null));
            db.TransactionOutputs.AddRange(
                Output(firstTxId, 0, legacyLineage, "first-hash", 1),
                Output(currentTxId, 4, legacyLineage, "current-hash", 2));
            await db.SaveChangesAsync();
        }

        var service = new LedgerReadModelService(factory);
        string canonicalId = $"{currentTxId}:4";
        IReadOnlyList<LedgerTransactionSummary> history = await service.QueryLedgerTransactionsAsync(
            new LedgerTransactionQuery(Context, BusinessObjectId: new BusinessObjectId(canonicalId)),
            CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, transaction => transaction.TxId.Value == firstTxId);
        LedgerTransactionSummary currentTransaction = Assert.Single(
            history,
            transaction => transaction.TxId.Value == currentTxId);
        LedgerTransactionOutputSummary output = Assert.Single(currentTransaction.Outputs);
        Assert.Equal(legacyLineage, output.BusinessObject!.LineageKey.Value);

        var publicDetails = await service.GetTransactionAsync(currentTxId, includeData: false, CancellationToken.None);
        Assert.NotNull(publicDetails);
        Assert.Equal(canonicalId, Assert.Single(publicDetails.Outputs).BusinessObjectId);

        await using LedgerDbContext verificationDb = factory.CreateDbContext();
        string? storedLineage = await verificationDb.TransactionOutputs
            .Where(x => x.TxId == currentTxId && x.OutputIndex == 4)
            .Select(x => x.BusinessObjectId)
            .SingleAsync();
        Assert.Equal(legacyLineage, storedLineage);
    }

    private static TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestDbContextFactory(options);
    }

    private static LedgerTransactionRecord Transaction(
        string txId,
        long sequence,
        string? dataHash,
        string? inlineJson)
        => new()
        {
            TxId = txId,
            LedgerId = Context.LedgerId.Value,
            LedgerSequence = sequence,
            Version = 1,
            Kind = (int)TransactionKind.BusinessObject,
            SigningMode = (int)SigningMode.Independent,
            Status = (int)TransactionStatus.Confirmed,
            Timestamp = ConfirmedAt,
            ConfirmedAt = ConfirmedAt,
            DataItemType = (int)TransactionDataItemType.BusinessObject,
            DataHash = dataHash,
            InlineJson = inlineJson
        };

    private static LedgerTransactionOutputRecord Output(
        string txId,
        int outputIndex,
        string lineage,
        string dataHash,
        int version)
        => new()
        {
            TxId = txId,
            OutputIndex = outputIndex,
            Address = Alice.Value,
            MainAmount = 0m,
            BusinessObjectId = lineage,
            BusinessObjectVersion = version,
            BusinessObjectOperation = (int)(version == 1 ? BusinessObjectOperation.Create : BusinessObjectOperation.Update),
            BusinessObjectDataHash = dataHash
        };

    private static UnspentOutputRecord Unspent(
        string txId,
        int outputIndex,
        string lineage,
        string dataHash)
        => new()
        {
            OutPointKey = $"key-{txId}-{outputIndex}",
            LedgerId = Context.LedgerId.Value,
            TxId = txId,
            OutputIndex = outputIndex,
            Address = Alice.Value,
            BusinessObjectId = lineage,
            BusinessObjectVersion = 1,
            BusinessObjectOperation = (int)BusinessObjectOperation.Create,
            BusinessObjectDataHash = dataHash,
            CreatedAt = ConfirmedAt
        };

    private static BusinessObjectRecord CurrentProjection(
        string txId,
        int outputIndex,
        string lineage,
        string dataHash)
        => new()
        {
            BusinessObjectKey = $"{Context.LedgerId.Value}||{lineage}",
            LedgerId = Context.LedgerId.Value,
            ObjectId = lineage,
            ObjectType = "post",
            CurrentVersion = 1,
            CurrentOwnerAddress = Alice.Value,
            CurrentState = "{\"name\":\"Visible name in data\"}",
            CurrentHash = dataHash,
            CurrentOutPointTxId = txId,
            CurrentOutPointIndex = outputIndex,
            CreatedTxId = txId,
            UpdatedTxId = txId
        };

    private static BusinessObjectVersionRecord Version(
        string txId,
        int outputIndex,
        string lineage,
        string dataHash)
        => new()
        {
            LedgerId = Context.LedgerId.Value,
            ObjectId = lineage,
            Version = 1,
            TxId = txId,
            OutPointTxId = txId,
            OutPointIndex = outputIndex,
            OwnerAddress = Alice.Value,
            State = "{\"name\":\"Visible name in data\"}",
            SchemaVersion = "1.0",
            DataHash = dataHash,
            CreatedAt = ConfirmedAt
        };

    private static string Payload(string sourceObjectId)
        => $$"""
            {
              "objectType": "post",
              "schemaVersion": "1.0",
              "stateJson": { "name": "Visible name in data" },
              "references": [
                { "relationship": "clone-source", "objectId": "{{sourceObjectId}}" }
              ]
            }
            """;

    private sealed class TestDbContextFactory(DbContextOptions<LedgerDbContext> options)
        : IDbContextFactory<LedgerDbContext>
    {
        public LedgerDbContext CreateDbContext() => new(options);

        public Task<LedgerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
