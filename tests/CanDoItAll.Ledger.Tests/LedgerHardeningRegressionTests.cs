using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Storage.InMemory;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerHardeningRegressionTests
{
    private static readonly LedgerId Ledger = new("hardening");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("hardening-alice"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("hardening-bob"));
    private static readonly TokenId CarrierA = new("bo.carrier-a");
    private static readonly TokenId CarrierB = new("bo.carrier-b");
    private static readonly BusinessObjectId ObjectId = new("bo-hardening");
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-07-09T00:00:00Z");

    [Fact]
    public async Task LedgerExecutor_RejectsOversizedEnvelopeBeforeHashing()
    {
        var hasher = new CountingHasher();
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        var executor = new LedgerExecutor(
            new InMemoryLedgerStore(hasher),
            hasher,
            payloadBuilder,
            new TestAdminSigner(),
            new ManualTimeProvider(Timestamp),
            SignatureProviderRegistry.CreateDefault());
        TransactionOutput[] outputs = Enumerable
            .Range(0, LedgerValidationLimits.MaxTransactionOutputs + 1)
            .Select(index => new TransactionOutput(index, Alice, Amount.FromInt64(1)))
            .ToArray();

        LedgerSubmitResult result = await executor.SubmitAsync(CreateGenesis(outputs), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.ResourceLimitExceeded);
        Assert.Equal(0, hasher.HashOperationCount);
    }

    [Fact]
    public async Task InMemoryConfirmationStore_ConcurrentDoubleSpendConfirmsExactlyOneTransaction()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        HashHex zeroHash = new(new string('0', 64));
        var genesisId = new TxId("genesis-hardening");
        var genesis = CreateGenesis(new TransactionOutput(0, Alice, Amount.FromInt64(10))) with
        {
            TxId = genesisId,
            TxHash = Hex.Sha256Hex(Hex.Utf8("genesis-hardening-hash"))
        };
        await store.AppendConfirmedTransactionAsync(
            new ConfirmedTransaction(
                genesis,
                1,
                zeroHash,
                Hex.Sha256Hex(Hex.Utf8("entry-1")),
                SignatureBundle.Empty,
                Timestamp),
            CancellationToken.None);

        var source = new OutPoint(genesisId, 0);
        OutPointKey sourceKey = hasher.CalculateOutPointKey(Ledger, null, source, Alice);
        LedgerTransaction first = CreateNormalizedSpend("spend-first", source, Bob);
        LedgerTransaction second = CreateNormalizedSpend("spend-second", source, Alice);

        async Task<LedgerSubmitResult> ConfirmAsync(LedgerTransaction candidate)
            => await store.ConfirmTransactionAsync(
                candidate,
                new[] { sourceKey },
                async (context, _) =>
                {
                    await Task.Yield();
                    if (!context.UnspentOutputs.ContainsKey(sourceKey))
                    {
                        return LedgerConfirmationDecision.Rejected(new[]
                        {
                            new ValidationError(ValidationErrorCodes.SpentInput, "Source output is already spent.")
                        });
                    }

                    LedgerState state = context.State!;
                    return LedgerConfirmationDecision.Confirmed(new ConfirmedTransaction(
                        candidate,
                        state.CurrentSequence + 1,
                        state.CurrentLedgerHash,
                        Hex.Sha256Hex(Hex.Utf8(candidate.TxId!.Value.Value)),
                        SignatureBundle.Empty,
                        Timestamp.AddMinutes(1)));
                },
                CancellationToken.None);

        LedgerSubmitResult[] results = await Task.WhenAll(ConfirmAsync(first), ConfirmAsync(second));

        Assert.Single(results, result => result.Accepted);
        LedgerSubmitResult rejected = Assert.Single(results, result => !result.Accepted);
        Assert.Contains(rejected.Errors, error => error.Code == ValidationErrorCodes.SpentInput);
    }

    [Fact]
    public async Task InMemoryConfirmationStore_ConcurrentDuplicateTokenRegistrationConfirmsExactlyOneTransaction()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var tokenId = new TokenId("hardening.concurrent-token");

        async Task<LedgerSubmitResult> ConfirmAsync(string txId)
        {
            var candidate = new LedgerTransaction(
                Ledger,
                null,
                1,
                TransactionKind.Mint,
                SigningMode.Independent,
                Timestamp,
                Array.Empty<TransactionInput>(),
                new[]
                {
                    new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(1)))
                },
                null)
            {
                TxId = new TxId(txId),
                TxHash = Hex.Sha256Hex(Hex.Utf8($"{txId}-hash"))
            };

            return await store.ConfirmTransactionAsync(
                candidate,
                Array.Empty<OutPointKey>(),
                async (context, cancellationToken) =>
                {
                    if (await store.TokenExistsAsync(Ledger, null, tokenId, cancellationToken))
                    {
                        return LedgerConfirmationDecision.Rejected(new[]
                        {
                            new ValidationError(
                                ValidationErrorCodes.DuplicateTokenType,
                                $"Token type already exists: {tokenId.Value}.")
                        });
                    }

                    LedgerState state = context.State ?? new LedgerState(
                        Ledger,
                        null,
                        0,
                        new HashHex(new string('0', 64)),
                        Timestamp);
                    return LedgerConfirmationDecision.Confirmed(new ConfirmedTransaction(
                        candidate,
                        state.CurrentSequence + 1,
                        state.CurrentLedgerHash,
                        Hex.Sha256Hex(Hex.Utf8($"{txId}-entry")),
                        SignatureBundle.Empty,
                        Timestamp));
                },
                CancellationToken.None);
        }

        LedgerSubmitResult[] results = await Task.WhenAll(
            ConfirmAsync("token-registration-first"),
            ConfirmAsync("token-registration-second"));

        Assert.Single(results, result => result.Accepted);
        LedgerSubmitResult rejected = Assert.Single(results, result => !result.Accepted);
        Assert.Contains(rejected.Errors, error => error.Code == ValidationErrorCodes.DuplicateTokenType);
    }

    [Fact]
    public void InputReferenceValidator_DistinguishesSpentUnknownAndWrongOwnerInputs()
    {
        var spentKey = new OutPointKey(Hex.Sha256Hex(Hex.Utf8("spent-key")));
        var unknownKey = new OutPointKey(Hex.Sha256Hex(Hex.Utf8("unknown-key")));
        var wrongOwnerKey = new OutPointKey(Hex.Sha256Hex(Hex.Utf8("wrong-owner-key")));
        var spentOutPoint = new OutPoint(new TxId("spent-source"), 0);
        var unknownOutPoint = new OutPoint(new TxId("unknown-source"), 0);
        var wrongOwnerOutPoint = new OutPoint(new TxId("wrong-owner-source"), 0);
        var transaction = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            Timestamp,
            new[]
            {
                new TransactionInput(0, spentOutPoint, Alice, SignatureBundle.Empty),
                new TransactionInput(1, unknownOutPoint, Alice, SignatureBundle.Empty),
                new TransactionInput(2, wrongOwnerOutPoint, Alice, SignatureBundle.Empty)
            },
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
            null);
        var spent = new SpentOutput(
            spentKey,
            Ledger,
            null,
            spentOutPoint,
            Alice,
            new TxId("spender"),
            Timestamp,
            SignatureBundle.Empty);
        var wrongOwner = new UnspentOutput(
            wrongOwnerKey,
            Ledger,
            null,
            wrongOwnerOutPoint,
            Bob,
            Amount.FromInt64(1),
            null,
            null,
            Timestamp);

        ValidationResult result = new InputReferenceValidator().Validate(
            transaction,
            new[] { spentKey, unknownKey, wrongOwnerKey },
            new Dictionary<OutPointKey, UnspentOutput> { [wrongOwnerKey] = wrongOwner },
            new Dictionary<OutPointKey, SpentOutput> { [spentKey] = spent });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.SpentInput);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.UnknownInput);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.AddressMismatch);
    }

    [Fact]
    public void InputSignatureValidator_RejectsMissingPayloadAndSignature()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var transaction = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            Timestamp,
            new[]
            {
                new TransactionInput(
                    0,
                    new OutPoint(new TxId("signature-source"), 0),
                    Alice,
                    SignatureBundle.Empty)
            },
            new[] { new TransactionOutput(0, Bob, Amount.FromInt64(1)) },
            null);
        var validator = new InputSignatureValidator(
            new SignaturePayloadBuilder(hasher),
            SignatureProviderRegistry.CreateDefault());

        ValidationResult result = validator.Validate(transaction);

        Assert.False(result.IsValid);
        Assert.All(result.Errors, error => Assert.Equal(ValidationErrorCodes.InvalidSignature, error.Code));
        Assert.Contains(result.Errors, error => error.Message.Contains("payload hash", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Message.Contains("at least one signature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LedgerExecutor_WithoutPolicyArgumentRejectsUnauthorizedMint()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var executor = new LedgerExecutor(
            store,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            new ManualTimeProvider(Timestamp),
            SignatureProviderRegistry.CreateDefault());
        LedgerSubmitResult genesis = await executor.SubmitAsync(
            CreateGenesis(new TransactionOutput(0, Alice, Amount.FromInt64(1))),
            CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors.Select(error => error.Message)));
        var unauthorizedMint = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Mint,
            SigningMode.Independent,
            Timestamp.AddMinutes(1),
            new[]
            {
                new TransactionInput(
                    0,
                    new OutPoint(genesis.TxId!.Value, 0),
                    Alice,
                    SignatureBundle.Empty)
            },
            new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(1)),
                new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierA, Amount.FromInt64(1)))
            },
            null);

        LedgerSubmitResult result = await executor.SubmitAsync(unauthorizedMint, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task LedgerExecutor_WithoutMainIssuancePolicyRejectsMainCurrencyInflation()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var executor = new LedgerExecutor(
            store,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            new ManualTimeProvider(Timestamp),
            SignatureProviderRegistry.CreateDefault());
        LedgerSubmitResult genesis = await executor.SubmitAsync(
            CreateGenesis(new TransactionOutput(0, Alice, Amount.FromInt64(1))),
            CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors.Select(error => error.Message)));
        var unauthorizedMint = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Mint,
            SigningMode.Independent,
            Timestamp.AddMinutes(1),
            new[]
            {
                new TransactionInput(
                    0,
                    new OutPoint(genesis.TxId!.Value, 0),
                    Alice,
                    SignatureBundle.Empty)
            },
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(2)) },
            null);

        LedgerSubmitResult result = await executor.SubmitAsync(unauthorizedMint, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.UnauthorizedMainCurrencyIssuance);
    }

    [Fact]
    public void TransactionShapeValidator_RejectsEmptySpendableOutput()
    {
        LedgerTransaction transaction = CreateGenesis(
            new TransactionOutput(0, Alice, Amount.Zero));

        ValidationResult result = new TransactionShapeValidator().ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.InvalidTransactionShape
            && error.Message.Contains("no spendable value", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsZeroTokenAmount()
    {
        LedgerTransaction transaction = CreateGenesis(
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(CarrierA, Amount.Zero)));

        ValidationResult result = new TransactionShapeValidator().ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.InvalidTransactionShape
            && error.Message.Contains("token amount must be positive", StringComparison.Ordinal));
    }

    [Fact]
    public void AssetConservationValidator_MintCannotInflateExistingNormalToken()
    {
        (OutPointKey key, UnspentOutput input) = CreateInput(
            mainAmount: 0m,
            token: new TokenTransfer(CarrierA, new Amount(5m)));
        LedgerTransaction transaction = CreateMint(
            input,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(CarrierA, new Amount(6m))),
            new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierB, new Amount(100m))));

        ValidationResult result = new AssetConservationValidator().ValidateTransfer(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput> { [key] = input });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.AssetImbalance
            && error.Message.Contains(CarrierA.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void AssetConservationValidator_MintCannotIncreasePermissionSupply()
    {
        (OutPointKey key, UnspentOutput input) = CreateInput(
            mainAmount: 0m,
            token: new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(1m)));
        LedgerTransaction transaction = CreateMint(
            input,
            new TransactionOutput(0, Alice, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(1m))),
            new TransactionOutput(1, Bob, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(1m))));

        ValidationResult result = new AssetConservationValidator().ValidateTransfer(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput> { [key] = input });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.AssetImbalance
            && error.Message.Contains(PermissionTokenAuthority.PermissionTokenId.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void AssetConservationValidator_MintCannotDestroyExistingMainAmount()
    {
        (OutPointKey key, UnspentOutput input) = CreateInput(mainAmount: 10m, token: null);
        LedgerTransaction transaction = CreateMint(
            input,
            new TransactionOutput(0, Alice, new Amount(9m)),
            new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierB, new Amount(100m))));

        ValidationResult result = new AssetConservationValidator().ValidateTransfer(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput> { [key] = input });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.AssetImbalance
            && error.Message.Contains("destroy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MainCurrencyIssuancePolicy_NonTreasuryCannotCreateMainAmount()
    {
        (OutPointKey key, UnspentOutput input) = CreateInput(mainAmount: 10m, token: null);
        LedgerTransaction transaction = CreateMint(
            input,
            new TransactionOutput(0, Alice, new Amount(11m)),
            new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierB, new Amount(100m))));

        ValidationResult result = new MainCurrencyIssuancePolicy(
            new MainCurrencyIssuancePolicyOptions { TreasuryAdminAddress = Bob }).Validate(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput> { [key] = input });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == ValidationErrorCodes.UnauthorizedMainCurrencyIssuance);
    }

    [Fact]
    public void MainCurrencyIssuancePolicy_TreasuryAuthorityCanCreateMainAmount()
    {
        (OutPointKey key, UnspentOutput input) = CreateInput(mainAmount: 10m, token: null);
        LedgerTransaction transaction = CreateMint(
            input,
            new TransactionOutput(0, Bob, new Amount(5m)),
            new TransactionOutput(1, Alice, new Amount(10m)));

        ValidationResult result = new MainCurrencyIssuancePolicy(
            new MainCurrencyIssuancePolicyOptions { TreasuryAdminAddress = Alice }).Validate(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput> { [key] = input });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AssetConservationValidator_AmountOverflowReturnsValidationError()
    {
        (OutPointKey firstKey, UnspentOutput first) = CreateInput(decimal.MaxValue, null, "first");
        (OutPointKey secondKey, UnspentOutput second) = CreateInput(decimal.MaxValue, null, "second");
        var transaction = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            Timestamp,
            new[]
            {
                new TransactionInput(0, first.OutPoint, Alice, SignatureBundle.Empty),
                new TransactionInput(1, second.OutPoint, Alice, SignatureBundle.Empty)
            },
            new[] { new TransactionOutput(0, Alice, new Amount(decimal.MaxValue)) },
            null);

        ValidationResult result = new AssetConservationValidator().ValidateTransfer(
            transaction,
            new Dictionary<OutPointKey, UnspentOutput>
            {
                [firstKey] = first,
                [secondKey] = second
            });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "amount_overflow");
    }

    [Fact]
    public void BusinessObjectTransitionValidator_UpdateCannotChangeOwner()
    {
        (LedgerTransaction transaction, IReadOnlyList<OutPointKey> keys, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs) =
            CreateBusinessObjectTransition(BusinessObjectOperation.Update, Bob, CarrierA);

        ValidationResult result = new BusinessObjectTransitionValidator().Validate(transaction, keys, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("update must keep owner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BusinessObjectTransitionValidator_TransferMustChangeOwner()
    {
        (LedgerTransaction transaction, IReadOnlyList<OutPointKey> keys, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs) =
            CreateBusinessObjectTransition(BusinessObjectOperation.Transfer, Alice, CarrierA);

        ValidationResult result = new BusinessObjectTransitionValidator().Validate(transaction, keys, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("transfer must change owner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BusinessObjectTransitionValidator_CannotChangeCarrierTokenId()
    {
        (LedgerTransaction transaction, IReadOnlyList<OutPointKey> keys, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs) =
            CreateBusinessObjectTransition(BusinessObjectOperation.Update, Alice, CarrierB);

        ValidationResult result = new BusinessObjectTransitionValidator().Validate(transaction, keys, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("carrier token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BusinessObjectTransitionValidator_ChangedExternalDataMustMatchDeclaredHash()
    {
        HashHex currentHash = Hex.Sha256Hex(Hex.Utf8("current"));
        HashHex nextHash = Hex.Sha256Hex(Hex.Utf8("next"));
        HashHex declaredHash = Hex.Sha256Hex(Hex.Utf8("different"));
        (LedgerTransaction transaction, IReadOnlyList<OutPointKey> keys, IReadOnlyDictionary<OutPointKey, UnspentOutput> inputs) =
            CreateBusinessObjectTransition(
                BusinessObjectOperation.Update,
                Alice,
                CarrierA,
                currentHash,
                nextHash,
                new TransactionData("application/json", declaredHash, null, "ipfs://next", TransactionDataItemType.BusinessObject));

        ValidationResult result = new BusinessObjectTransitionValidator().Validate(transaction, keys, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Message.Contains("data hash must match", StringComparison.OrdinalIgnoreCase));
    }

    private static LedgerTransaction CreateGenesis(params TransactionOutput[] outputs)
        => new(
            Ledger,
            null,
            1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            Timestamp,
            Array.Empty<TransactionInput>(),
            outputs,
            null);

    private static LedgerTransaction CreateNormalizedSpend(string txId, OutPoint source, Address destination)
        => new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            Timestamp.AddMinutes(1),
            new[] { new TransactionInput(0, source, Alice, SignatureBundle.Empty) },
            new[] { new TransactionOutput(0, destination, Amount.FromInt64(10)) },
            null)
        {
            TxId = new TxId(txId),
            TxHash = Hex.Sha256Hex(Hex.Utf8($"{txId}-hash"))
        };

    private static LedgerTransaction CreateMint(UnspentOutput input, params TransactionOutput[] outputs)
        => new(
            Ledger,
            null,
            1,
            TransactionKind.Mint,
            SigningMode.Independent,
            Timestamp,
            new[] { new TransactionInput(0, input.OutPoint, input.Address, SignatureBundle.Empty) },
            outputs,
            null);

    private static (OutPointKey Key, UnspentOutput Output) CreateInput(
        decimal mainAmount,
        TokenTransfer? token,
        string txId = "source")
    {
        var key = new OutPointKey(Hex.Sha256Hex(Hex.Utf8(txId)));
        var outPoint = new OutPoint(new TxId(txId), 0);
        return (
            key,
            new UnspentOutput(
                key,
                Ledger,
                null,
                outPoint,
                Alice,
                new Amount(mainAmount),
                token,
                null,
                Timestamp));
    }

    private static (
        LedgerTransaction Transaction,
        IReadOnlyList<OutPointKey> Keys,
        IReadOnlyDictionary<OutPointKey, UnspentOutput> Inputs) CreateBusinessObjectTransition(
        BusinessObjectOperation operation,
        Address nextOwner,
        TokenId nextCarrier,
        HashHex? currentHash = null,
        HashHex? nextHash = null,
        TransactionData? data = null)
    {
        currentHash ??= Hex.Sha256Hex(Hex.Utf8("current"));
        nextHash ??= currentHash;
        var key = new OutPointKey(Hex.Sha256Hex(Hex.Utf8("bo-source")));
        var source = new OutPoint(new TxId("bo-source"), 0);
        var current = new UnspentOutput(
            key,
            Ledger,
            null,
            source,
            Alice,
            Amount.Zero,
            new TokenTransfer(CarrierA, new Amount(1m)),
            new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, currentHash.Value),
            Timestamp);
        var transaction = new LedgerTransaction(
            Ledger,
            null,
            1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            Timestamp.AddMinutes(1),
            new[] { new TransactionInput(0, source, Alice, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(
                    0,
                    nextOwner,
                    Amount.Zero,
                    new TokenTransfer(nextCarrier, new Amount(1m)),
                    new BusinessObjectRef(ObjectId, 2, operation, nextHash.Value))
            },
            data);

        return (transaction, new[] { key }, new Dictionary<OutPointKey, UnspentOutput> { [key] = current });
    }

    private sealed class CountingHasher : ITransactionHasher
    {
        private readonly ITransactionHasher inner = new TransactionHasher(new DefaultTransactionCanonicalizer());

        public int HashOperationCount { get; private set; }

        public TxId CalculateTxId(LedgerTransaction transaction)
        {
            HashOperationCount++;
            return inner.CalculateTxId(transaction);
        }

        public HashHex CalculateTxHash(LedgerTransaction transaction)
        {
            HashOperationCount++;
            return inner.CalculateTxHash(transaction);
        }

        public OutPointKey CalculateOutPointKey(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            OutPoint outPoint,
            Address address)
        {
            HashOperationCount++;
            return inner.CalculateOutPointKey(ledgerId, scenarioId, outPoint, address);
        }
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash) => SignatureBundle.Empty;
    }
}
