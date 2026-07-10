using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Tests;

public sealed class TokenDefinitionProtocolTests
{
    private static readonly Address Issuer = Address.FromPublicKey(Hex.Utf8("v2-token-issuer"));

    [Fact]
    public void TokenDefinitionV2_IdIsDeterministicAndExcludesDisplayMetadata()
    {
        ITokenDefinitionHasher hasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenDefinition definition = CreateDefinition(TokenClassification.BusinessObjectCarrier, Amount.FromInt64(100));
        var first = new TokenDefinitionEnvelope(
            definition,
            new TokenDisplayMetadata("Project tickets", "First display text", "ipfs://display-one"));
        var second = new TokenDefinitionEnvelope(
            definition,
            new TokenDisplayMetadata("Renamed tickets", "Different display text", "ipfs://display-two"));

        TokenId firstId = hasher.CalculateTokenId(first.Definition);
        TokenId secondId = hasher.CalculateTokenId(second.Definition);

        Assert.Equal(firstId, secondId);
        Assert.Equal(
            "token:v2:7823e24911fd358347232b27e1f384066f3debcb035f60c9031fd9de6cef93e4",
            firstId.Value);
        Assert.StartsWith(TokenDefinitionIdentity.V2TokenIdPrefix, firstId.Value, StringComparison.Ordinal);
        Assert.Equal(64, hasher.CalculateDefinitionHash(definition).Value.Length);
    }

    [Fact]
    public async Task TokenDefinitionPolicy_TamperedIdAndInitialSupplyMismatchAreRejected()
    {
        ITokenDefinitionHasher hasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenDefinition definition = CreateDefinition(TokenClassification.Fungible, Amount.FromInt64(100));
        LedgerTransaction transaction = CreateMint(
            definition,
            new TokenId(TokenDefinitionIdentity.V2TokenIdPrefix + new string('f', 64)),
            Amount.FromInt64(99));
        var policy = new TokenDefinitionPolicy(hasher);

        ValidationResult result = await policy.ValidateAsync(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput>(),
            new InMemoryLedgerStore(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.InvalidTokenDefinitionId);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.InvalidTokenInitialSupply);
    }

    [Theory]
    [InlineData(TokenClassification.Fungible, 1, true, ValidationErrorCodes.InvalidTokenClassification)]
    [InlineData(TokenClassification.BusinessObjectCarrier, 1, false, ValidationErrorCodes.InvalidTokenClassification)]
    [InlineData(TokenClassification.BusinessObjectCarrier, 2, true, ValidationErrorCodes.InvalidTokenClassification)]
    public async Task TokenDefinitionPolicy_ClassificationControlsBusinessObjectCarrierShape(
        TokenClassification classification,
        long amount,
        bool includeBusinessObject,
        string expectedCode)
    {
        ITokenDefinitionHasher hasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenDefinition definition = CreateDefinition(classification, Amount.FromInt64(amount));
        TokenId tokenId = hasher.CalculateTokenId(definition);
        BusinessObjectRef? businessObject = includeBusinessObject
            ? new BusinessObjectRef(
                new BusinessObjectId("bo-token-v2"),
                1,
                BusinessObjectOperation.Create,
                new HashHex(new string('a', 64)))
            : null;
        LedgerTransaction transaction = CreateMint(definition, tokenId, Amount.FromInt64(amount), businessObject);
        var policy = new TokenDefinitionPolicy(hasher);

        ValidationResult result = await policy.ValidateAsync(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput>(),
            new InMemoryLedgerStore(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    [Fact]
    public async Task InMemoryStore_V2DefinitionMetadataRoundTripsWhileLegacyTokenRemainsAddressable()
    {
        ITokenDefinitionHasher definitionHasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenDefinition definition = CreateDefinition(TokenClassification.BusinessObjectCarrier, Amount.FromInt64(10));
        TokenId tokenId = definitionHasher.CalculateTokenId(definition);
        ITransactionHasher transactionHasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(transactionHasher);
        LedgerTransaction v2 = CreateMint(definition, tokenId, Amount.FromInt64(10));
        await store.AppendConfirmedTransactionAsync(
            CreateConfirmed(v2, transactionHasher, 1, new HashHex(new string('0', 64))),
            CancellationToken.None);

        RegisteredTokenDefinition? registered = await store.GetTokenDefinitionAsync(
            v2.LedgerId,
            v2.ScenarioId,
            tokenId,
            CancellationToken.None);

        Assert.NotNull(registered);
        Assert.Equal(2, registered.ProtocolVersion);
        Assert.Equal(TokenClassification.BusinessObjectCarrier, registered.Classification);
        Assert.Equal("tickets", registered.Symbol);
        Assert.Equal(definitionHasher.CalculateDefinitionHash(definition), registered.DefinitionHash);

        var legacyTokenId = new TokenId("legacy.project-ticket");
        var legacy = new LedgerTransaction(
            v2.LedgerId,
            null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            v2.Timestamp.AddMinutes(1),
            [],
            [new TransactionOutput(0, Issuer, Amount.Zero, new TokenTransfer(legacyTokenId, Amount.FromInt64(5)))],
            null);
        await store.AppendConfirmedTransactionAsync(
            CreateConfirmed(legacy, transactionHasher, 2, new HashHex(new string('b', 64))),
            CancellationToken.None);

        RegisteredTokenDefinition? legacyDefinition = await store.GetTokenDefinitionAsync(
            legacy.LedgerId,
            legacy.ScenarioId,
            legacyTokenId,
            CancellationToken.None);

        Assert.NotNull(legacyDefinition);
        Assert.Equal(1, legacyDefinition!.ProtocolVersion);
        Assert.Equal(TokenClassification.LegacyUnspecified, legacyDefinition.Classification);
        Assert.Null(legacyDefinition.DefinitionHash);
    }

    [Fact]
    public async Task EfStore_V2DefinitionAndDisplayMetadataRoundTrip()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestDbContextFactory(options);
        await using (EconomyDbContext setup = factory.CreateDbContext())
        {
            await setup.Database.EnsureCreatedAsync();
        }

        ITokenDefinitionHasher definitionHasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenDefinition definition = CreateDefinition(TokenClassification.BusinessObjectCarrier, Amount.FromInt64(10));
        TokenId tokenId = definitionHasher.CalculateTokenId(definition);
        ITransactionHasher transactionHasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new EfLedgerStore(factory, transactionHasher);
        LedgerTransaction transaction = CreateMint(definition, tokenId, Amount.FromInt64(10));
        await store.AppendConfirmedTransactionAsync(
            CreateConfirmed(transaction, transactionHasher, 1, new HashHex(new string('0', 64))),
            CancellationToken.None);

        RegisteredTokenDefinition? registered = await store.GetTokenDefinitionAsync(
            transaction.LedgerId,
            null,
            tokenId,
            CancellationToken.None);

        Assert.NotNull(registered);
        Assert.Equal("Project tickets", registered!.DisplayMetadata!.DisplayName);
        Assert.Equal(definitionHasher.CalculateDefinitionHash(definition), registered.DefinitionHash);
        Assert.Equal(TokenClassification.BusinessObjectCarrier, registered.Classification);
    }

    [Fact]
    public async Task LedgerExecutor_AuthorizedV2DefinitionMintConfirmsAndRegistersImmutableMetadata()
    {
        byte[] treasuryPublicKey = Hex.Utf8("treasury-admin-public");
        Address treasury = Address.FromPublicKey(treasuryPublicKey);
        ITransactionHasher transactionHasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var payloadBuilder = new SignaturePayloadBuilder(transactionHasher);
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-10T03:00:00Z"));
        var signatureRegistry = new SignatureProviderRegistry([new AcceptingSignatureProvider()]);
        var store = new InMemoryLedgerStore(transactionHasher);
        var executor = new LedgerExecutor(
            store,
            transactionHasher,
            payloadBuilder,
            new TestAdminSigner(),
            time,
            signatureRegistry,
            new TokenCreationPolicy(new TokenCreationPolicyOptions { TreasuryAdminAddress = treasury }),
            new MainCurrencyIssuancePolicy(new MainCurrencyIssuancePolicyOptions { TreasuryAdminAddress = treasury }));

        LedgerSubmitResult genesis = await executor.SubmitAsync(
            new LedgerTransaction(
                new LedgerId("token-v2-authorized"),
                null,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                time.UtcNow,
                [],
                [new TransactionOutput(
                    0,
                    treasury,
                    Amount.Zero,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(100)))],
                null),
            CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        time.Advance(TimeSpan.FromMinutes(1));
        var grant = new PermissionGrant(
            PermissionTokenAuthority.MintNewTokenTypeAction,
            "project.events",
            Repeatable: true);
        TransactionData grantData = PermissionTokenAuthority.CreateData(grant);
        var permissionRef = new BusinessObjectRef(
            PermissionTokenAuthority.CreatePermissionObjectId(grant.Action, grant.Namespace),
            1,
            BusinessObjectOperation.Create,
            grantData.DataHash!.Value);
        LedgerTransaction grantTransaction = Sign(
            new LedgerTransaction(
                new LedgerId("token-v2-authorized"),
                null,
                Version: 1,
                TransactionKind.BusinessObject,
                SigningMode.Independent,
                time.UtcNow,
                [new TransactionInput(0, new OutPoint(genesis.TxId!.Value, 0), treasury, SignatureBundle.Empty)],
                [
                    new TransactionOutput(0, treasury, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(1)), permissionRef),
                    new TransactionOutput(1, treasury, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(99)))
                ],
                grantData),
            treasuryPublicKey,
            transactionHasher,
            payloadBuilder);
        LedgerSubmitResult granted = await executor.SubmitAsync(grantTransaction, CancellationToken.None);
        Assert.True(granted.Accepted, string.Join("; ", granted.Errors));

        time.Advance(TimeSpan.FromMinutes(1));
        var definition = new TokenDefinition(
            2,
            "project.events",
            "tickets",
            treasury,
            Amount.FromInt64(10),
            0,
            TokenClassification.BusinessObjectCarrier);
        var envelope = new TokenDefinitionEnvelope(
            definition,
            new TokenDisplayMetadata("Project tickets", "Immutable supply with mutable display metadata", null));
        ITokenDefinitionHasher definitionHasher = new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
        TokenId tokenId = definitionHasher.CalculateTokenId(definition);
        LedgerTransaction definitionMint = Sign(
            new LedgerTransaction(
                new LedgerId("token-v2-authorized"),
                null,
                Version: 2,
                TransactionKind.Mint,
                SigningMode.Independent,
                time.UtcNow,
                [new TransactionInput(0, new OutPoint(granted.TxId!.Value, 0), treasury, SignatureBundle.Empty)],
                [
                    new TransactionOutput(0, treasury, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(10))),
                    new TransactionOutput(
                        1,
                        treasury,
                        Amount.Zero,
                        new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(1)),
                        permissionRef with { Version = 2, Operation = BusinessObjectOperation.Update })
                ],
                null,
                TokenDefinition: envelope),
            treasuryPublicKey,
            transactionHasher,
            payloadBuilder);

        LedgerSubmitResult minted = await executor.SubmitAsync(definitionMint, CancellationToken.None);

        Assert.True(minted.Accepted, string.Join("; ", minted.Errors));
        RegisteredTokenDefinition? registered = await store.GetTokenDefinitionAsync(
            new LedgerId("token-v2-authorized"),
            null,
            tokenId,
            CancellationToken.None);
        Assert.NotNull(registered);
        Assert.Equal(definition, new TokenDefinition(
            registered!.ProtocolVersion,
            registered.Namespace!,
            registered.Symbol!,
            registered.Issuer!.Value,
            registered.InitialSupply!.Value,
            registered.DecimalPlaces!.Value,
            registered.Classification));
    }

    private static TokenDefinition CreateDefinition(TokenClassification classification, Amount initialSupply)
        => new(
            ProtocolVersion: 2,
            Namespace: "project.events",
            Symbol: "tickets",
            Issuer,
            initialSupply,
            DecimalPlaces: 0,
            classification);

    private static LedgerTransaction CreateMint(
        TokenDefinition definition,
        TokenId tokenId,
        Amount amount,
        BusinessObjectRef? businessObject = null)
        => new(
            new LedgerId("token-v2"),
            null,
            Version: 2,
            TransactionKind.Mint,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-10T01:00:00Z"),
            [],
            [new TransactionOutput(0, Issuer, Amount.Zero, new TokenTransfer(tokenId, amount), businessObject)],
            null,
            TokenDefinition: new TokenDefinitionEnvelope(
                definition,
                new TokenDisplayMetadata("Project tickets", null, null)));

    private static ConfirmedTransaction CreateConfirmed(
        LedgerTransaction transaction,
        ITransactionHasher hasher,
        long sequence,
        HashHex previousHash)
    {
        LedgerTransaction normalized = transaction with
        {
            TxId = hasher.CalculateTxId(transaction),
            TxHash = hasher.CalculateTxHash(transaction),
            Status = TransactionStatus.Confirmed
        };
        return new ConfirmedTransaction(
            normalized,
            sequence,
            previousHash,
            new HashHex(new string('b', 64)),
            SignatureBundle.Empty,
            transaction.Timestamp.AddSeconds(1));
    }

    private static LedgerTransaction Sign(
        LedgerTransaction transaction,
        byte[] publicKey,
        ITransactionHasher hasher,
        ISignaturePayloadBuilder payloadBuilder)
    {
        LedgerTransaction withId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withId.Inputs.Select(input =>
        {
            HashHex payload = payloadBuilder.BuildIndependentInputPayloadHash(withId, input);
            return input with
            {
                SignaturePayloadHash = payload,
                SignatureBundle = new SignatureBundle([
                    new SignatureEnvelope(
                        SignatureAlgorithmId.EcdsaP256Sha256,
                        "v2-issuer",
                        Address.CalculatePublicKeyHash(publicKey),
                        payload,
                        Hex.Utf8("valid-signature"),
                        publicKey)
                ])
            };
        }).ToArray();
        return withId with { Inputs = inputs };
    }

    private sealed class AcceptingSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;
        public bool IsSupported => true;
        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial) => Hex.Utf8("valid-signature");
        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial) => true;
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new([
                new SignatureEnvelope(
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    "v2-admin",
                    new HashHex(new string('c', 64)),
                    payloadHash,
                    Hex.Utf8("admin-signature"))
            ]);
    }

    private sealed class TestDbContextFactory(DbContextOptions<EconomyDbContext> options)
        : IDbContextFactory<EconomyDbContext>
    {
        public EconomyDbContext CreateDbContext() => new(options);

        public ValueTask<EconomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
