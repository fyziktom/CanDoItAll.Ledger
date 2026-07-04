using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EfLedgerProjectionRebuilderTests
{
    private static readonly LedgerId LedgerId = new("rebuild-ledger");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task RebuildAsync_RecreatesDerivedRowsFromConfirmedTransactions()
    {
        ITransactionHasher hasher = CreateHasher();
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-12-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);
        var tokenId = new TokenId("ops.energy.kwh");

        LedgerSubmitResult genesis = await executor.SubmitAsync(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                timeProvider.UtcNow,
                Array.Empty<TransactionInput>(),
                new[]
                {
                    new TransactionOutput(0, Alice, Amount.FromInt64(100)),
                    new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(10)))
                },
                null),
            CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult transfer = await executor.SubmitAsync(
            Sign(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Transfer,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, new OutPoint(genesis.TxId!.Value, 0), Alice, SignatureBundle.Empty) },
                    new[]
                    {
                        new TransactionOutput(0, Bob, Amount.FromInt64(40)),
                        new TransactionOutput(1, Alice, Amount.FromInt64(60))
                    },
                    null),
                hasher),
            CancellationToken.None);
        Assert.True(transfer.Accepted, string.Join("; ", transfer.Errors));

        TransactionData createData = CreateBusinessObjectEnvelopeData(
            "invoice",
            "1.0",
            "{\"status\":\"draft\"}");
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult createBusinessObject = await executor.SubmitAsync(
            Sign(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.BusinessObject,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, new OutPoint(transfer.TxId!.Value, 1), Alice, SignatureBundle.Empty) },
                    new[]
                    {
                        new TransactionOutput(
                            0,
                            Alice,
                            Amount.FromInt64(60),
                            null,
                            new BusinessObjectRef(
                                new BusinessObjectId("invoice-1"),
                                1,
                                BusinessObjectOperation.Create,
                                createData.DataHash!.Value))
                    },
                    createData),
                hasher),
            CancellationToken.None);
        Assert.True(createBusinessObject.Accepted, string.Join("; ", createBusinessObject.Errors));

        TransactionData updateData = CreateBusinessObjectStateData("{\"status\":\"approved\"}");
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult updateBusinessObject = await executor.SubmitAsync(
            Sign(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.BusinessObject,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, new OutPoint(createBusinessObject.TxId!.Value, 0), Alice, SignatureBundle.Empty) },
                    new[]
                    {
                        new TransactionOutput(
                            0,
                            Alice,
                            Amount.FromInt64(60),
                            null,
                            new BusinessObjectRef(
                                new BusinessObjectId("invoice-1"),
                                2,
                                BusinessObjectOperation.Update,
                                updateData.DataHash!.Value))
                    },
                    updateData),
                hasher),
            CancellationToken.None);
        Assert.True(updateBusinessObject.Accepted, string.Join("; ", updateBusinessObject.Errors));

        ProjectionSnapshot expected = await SnapshotAsync(factory);
        await DeleteDerivedRowsAsync(factory);

        var rebuilder = new EfLedgerProjectionRebuilder(factory, hasher);
        EfLedgerProjectionRebuildResult result = await rebuilder.RebuildAsync(LedgerId, null, CancellationToken.None);

        ProjectionSnapshot actual = await SnapshotAsync(factory);
        Assert.Equal(4, result.TransactionCount);
        Assert.Equal(expected.UnspentOutputs.Count, result.UnspentOutputCount);
        Assert.Equal(expected.SpentOutputs.Count, result.SpentOutputCount);
        Assert.Equal(expected.AddressProjections.Count, result.AddressProjectionCount);
        Assert.Equal(expected.TokenTypes.Count, result.TokenTypeCount);
        Assert.Equal(expected.BusinessObjects.Count, result.BusinessObjectCount);
        Assert.Equal(expected.BusinessObjectVersions.Count, result.BusinessObjectVersionCount);
        Assert.Equal(expected.LedgerState.CurrentSequence, result.CurrentSequence);
        Assert.Equal(expected.LedgerState.CurrentLedgerHash, result.CurrentLedgerHash.Value);
        Assert.Equal(expected.UnspentOutputs, actual.UnspentOutputs);
        Assert.Equal(expected.SpentOutputs, actual.SpentOutputs);
        Assert.Equal(expected.AddressProjections, actual.AddressProjections);
        Assert.Equal(expected.TokenTypes, actual.TokenTypes);
        Assert.Equal(expected.BusinessObjects, actual.BusinessObjects);
        Assert.Equal(expected.BusinessObjectVersions, actual.BusinessObjectVersions);
        Assert.Equal(expected.LedgerState, actual.LedgerState);

        BusinessObjectSnapshot currentBusinessObject = Assert.Single(actual.BusinessObjects);
        Assert.Equal("invoice-1", currentBusinessObject.ObjectId);
        Assert.Equal(2, currentBusinessObject.CurrentVersion);
        Assert.Equal("{\"status\":\"approved\"}", currentBusinessObject.CurrentState);
        Assert.Equal(updateData.DataHash!.Value.Value, currentBusinessObject.CurrentHash);
        Assert.Equal(2, actual.BusinessObjectVersions.Count);
        BusinessObjectVersionSnapshot createdBusinessObject = actual.BusinessObjectVersions.Single(x => x.Version == 1);
        Assert.Equal("{\"status\":\"draft\"}", createdBusinessObject.State);
        Assert.Equal(createData.DataHash!.Value.Value, createdBusinessObject.DataHash);

        var projectionVerifier = new EfLedgerProjectionVerifier(factory);
        EfLedgerProjectionVerificationReport cleanProjection = await projectionVerifier.VerifyAsync(LedgerId, null, CancellationToken.None);
        Assert.True(cleanProjection.IsValid, string.Join("; ", cleanProjection.Issues.Select(x => x.Message)));
        Assert.Equal(expected.LedgerState.CurrentSequence, cleanProjection.HashSet.CurrentSequence);
        Assert.Equal(expected.LedgerState.CurrentLedgerHash, cleanProjection.HashSet.CurrentLedgerHash.Value);

        (byte[] adminPublicKey, byte[] adminPrivateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        var snapshotService = new EfLedgerSnapshotService(
            factory,
            projectionVerifier,
            new EcdsaSnapshotAdminSigner("snapshot-admin", adminPublicKey, adminPrivateKey),
            new SignatureProviderRegistry(new ISignatureProvider[] { new EcdsaP256SignatureProvider() }));
        LedgerSnapshot snapshot = await snapshotService.CreateSnapshotAsync(LedgerId, null, CancellationToken.None);
        EfLedgerSnapshotVerificationReport cleanSnapshot = await snapshotService.VerifySnapshotAsync(snapshot.SnapshotId, CancellationToken.None);
        Assert.True(cleanSnapshot.IsValid, string.Join("; ", cleanSnapshot.Issues.Select(x => x.Message)));

        await using (EconomyDbContext corruptDb = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            UnspentOutputRecord corruptOutput = await corruptDb.UnspentOutputs
                .OrderBy(x => x.OutPointKey)
                .FirstAsync(CancellationToken.None);
            corruptOutput.MainAmount += 1m;
            await corruptDb.SaveChangesAsync(CancellationToken.None);
        }

        EfLedgerProjectionVerificationReport corruptProjection = await projectionVerifier.VerifyAsync(LedgerId, null, CancellationToken.None);
        Assert.False(corruptProjection.IsValid);
        Assert.Contains(corruptProjection.Issues, x => x.Code == EfLedgerProjectionVerificationIssueCode.AddressProjectionBalanceMismatch);

        EfLedgerSnapshotVerificationReport corruptSnapshot = await snapshotService.VerifySnapshotAsync(snapshot.SnapshotId, CancellationToken.None);
        Assert.False(corruptSnapshot.IsValid);
        Assert.Contains(corruptSnapshot.Issues, x => x.Code == EfLedgerSnapshotVerificationIssueCode.SnapshotProjectionHashMismatch);
    }

    private static async Task<ProjectionSnapshot> SnapshotAsync(TestDbContextFactory factory)
    {
        await using EconomyDbContext db = await factory.CreateDbContextAsync(CancellationToken.None);
        return new ProjectionSnapshot(
            await db.UnspentOutputs
                .AsNoTracking()
                .OrderBy(x => x.OutPointKey)
                .Select(x => new UnspentOutputSnapshot(
                    x.OutPointKey,
                    x.TxId,
                    x.OutputIndex,
                    x.Address,
                    x.MainAmount,
                    x.TokenId,
                    x.TokenAmount))
                .ToListAsync(),
            await db.SpentOutputs
                .AsNoTracking()
                .OrderBy(x => x.OutPointKey)
                .Select(x => new SpentOutputSnapshot(
                    x.OutPointKey,
                    x.TxId,
                    x.OutputIndex,
                    x.Address,
                    x.SpentInTxId))
                .ToListAsync(),
            await db.AddressStateProjections
                .AsNoTracking()
                .OrderBy(x => x.Address)
                .Select(x => new AddressProjectionSnapshot(
                    x.Address,
                    x.MainBalance,
                    x.TokenBalancesJson,
                    x.UtxoCount,
                    x.LastLedgerSequence,
                    x.StateHash))
                .ToListAsync(),
            await db.LedgerTokenTypes
                .AsNoTracking()
                .OrderBy(x => x.TokenTypeKey)
                .Select(x => new TokenTypeSnapshot(
                    x.TokenTypeKey,
                    x.TokenId,
                    x.CreatedTxId,
                    x.CreatedOutputIndex))
                .ToListAsync(),
            await db.BusinessObjects
                .AsNoTracking()
                .OrderBy(x => x.ObjectId)
                .Select(x => new BusinessObjectSnapshot(
                    x.ObjectId,
                    x.ObjectType,
                    x.CurrentVersion,
                    x.CurrentOwnerAddress,
                    x.CurrentState,
                    x.CurrentHash,
                    x.CurrentOutPointTxId,
                    x.CurrentOutPointIndex,
                    x.CreatedTxId,
                    x.UpdatedTxId))
                .ToListAsync(),
            await db.BusinessObjectVersions
                .AsNoTracking()
                .OrderBy(x => x.ObjectId)
                .ThenBy(x => x.Version)
                .Select(x => new BusinessObjectVersionSnapshot(
                    x.ObjectId,
                    x.Version,
                    x.TxId,
                    x.OutPointTxId,
                    x.OutPointIndex,
                    x.OwnerAddress,
                    x.State,
                    x.SchemaVersion,
                    x.DataHash,
                    x.DataJson))
                .ToListAsync(),
            await db.LedgerStates
                .AsNoTracking()
                .Where(x => x.StateKey == $"{LedgerId.Value}|")
                .Select(x => new LedgerStateSnapshot(x.CurrentSequence, x.CurrentLedgerHash))
                .SingleAsync());
    }

    private static async Task DeleteDerivedRowsAsync(TestDbContextFactory factory)
    {
        await using EconomyDbContext db = await factory.CreateDbContextAsync(CancellationToken.None);
        db.UnspentOutputs.RemoveRange(db.UnspentOutputs);
        db.SpentOutputs.RemoveRange(db.SpentOutputs);
        db.AddressStateProjections.RemoveRange(db.AddressStateProjections);
        db.LedgerTokenTypes.RemoveRange(db.LedgerTokenTypes);
        db.BusinessObjects.RemoveRange(db.BusinessObjects);
        db.BusinessObjectVersions.RemoveRange(db.BusinessObjectVersions);
        db.LedgerStates.RemoveRange(db.LedgerStates);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static LedgerExecutor CreateExecutor(ILedgerStore store, ITransactionHasher hasher, ITimeProvider timeProvider)
        => new(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }));

    private static ITransactionHasher CreateHasher()
        => new TransactionHasher(new DefaultTransactionCanonicalizer());

    private static TransactionData CreateBusinessObjectEnvelopeData(string objectType, string schemaVersion, string stateJson)
    {
        using JsonDocument document = JsonDocument.Parse(stateJson);
        string inlineJson = JsonSerializer.Serialize(
            new BusinessObjectStructuredEnvelopeData(
                objectType,
                schemaVersion,
                document.RootElement.Clone()),
            JsonOptions);

        return CreateBusinessObjectStateData(inlineJson);
    }

    private sealed record BusinessObjectStructuredEnvelopeData(
        string ObjectType,
        string SchemaVersion,
        JsonElement StateJson);

    private static TransactionData CreateBusinessObjectStateData(string stateJson)
        => new(
            "application/json",
            Hex.Sha256Hex(Hex.Utf8(stateJson)),
            stateJson,
            null,
            TransactionDataItemType.BusinessObject);

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = Hex.Utf8("alice-public");
                return input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "alice",
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8("alice-signature"),
                            publicKey)
                    })
                };
            })
            .ToArray();

        return withTxId with { Inputs = inputs };
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex("admin-public"u8.ToArray()),
                    payloadHash,
                    Hex.Utf8("admin-signature"))
            });
    }

    private sealed class EcdsaSnapshotAdminSigner(
        string keyId,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial) : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
        {
            var provider = new EcdsaP256SignatureProvider();
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(payloadHash.Value);
            return new SignatureBundle(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    keyId,
                    Address.CalculatePublicKeyHash(publicKeyMaterial),
                    payloadHash,
                    provider.Sign(payload, privateKeyMaterial),
                    publicKeyMaterial)
            });
        }
    }

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("alice-signature");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => signature.SequenceEqual(Hex.Utf8("alice-signature"));
    }

    private sealed class TestDbContextFactory(DbContextOptions<EconomyDbContext> options) : IDbContextFactory<EconomyDbContext>
    {
        public EconomyDbContext CreateDbContext()
            => new(options);

        public Task<EconomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed record ProjectionSnapshot(
        IReadOnlyList<UnspentOutputSnapshot> UnspentOutputs,
        IReadOnlyList<SpentOutputSnapshot> SpentOutputs,
        IReadOnlyList<AddressProjectionSnapshot> AddressProjections,
        IReadOnlyList<TokenTypeSnapshot> TokenTypes,
        IReadOnlyList<BusinessObjectSnapshot> BusinessObjects,
        IReadOnlyList<BusinessObjectVersionSnapshot> BusinessObjectVersions,
        LedgerStateSnapshot LedgerState);

    private sealed record UnspentOutputSnapshot(
        string OutPointKey,
        string TxId,
        int OutputIndex,
        string Address,
        decimal MainAmount,
        string? TokenId,
        decimal? TokenAmount);

    private sealed record SpentOutputSnapshot(
        string OutPointKey,
        string TxId,
        int OutputIndex,
        string Address,
        string SpentInTxId);

    private sealed record AddressProjectionSnapshot(
        string Address,
        decimal MainBalance,
        string TokenBalancesJson,
        int UtxoCount,
        long LastLedgerSequence,
        string StateHash);

    private sealed record TokenTypeSnapshot(
        string TokenTypeKey,
        string TokenId,
        string CreatedTxId,
        int CreatedOutputIndex);

    private sealed record BusinessObjectSnapshot(
        string ObjectId,
        string ObjectType,
        int CurrentVersion,
        string CurrentOwnerAddress,
        string CurrentState,
        string CurrentHash,
        string CurrentOutPointTxId,
        int CurrentOutPointIndex,
        string CreatedTxId,
        string UpdatedTxId);

    private sealed record BusinessObjectVersionSnapshot(
        string ObjectId,
        int Version,
        string TxId,
        string OutPointTxId,
        int OutPointIndex,
        string OwnerAddress,
        string State,
        string SchemaVersion,
        string DataHash,
        string? DataJson);

    private sealed record LedgerStateSnapshot(long CurrentSequence, string CurrentLedgerHash);
}
