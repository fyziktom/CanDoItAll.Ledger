using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Ledger;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectReferenceValidatorTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Alice = new("alice");
    private static readonly Address Bob = new("bob");
    private static readonly HashHex TargetDataHash = new("target-data-hash");

    public static TheoryData<string> MalformedReferencePayloads => new()
    {
        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":{}}",
        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":[null]}",
        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":[{\"relationship\":\"clone-source\",\"objectId\":\"bo:post:name\"}]}",
        $$"""{"objectType":"post","schemaVersion":"1.0","stateJson":{},"references":[{"relationship":"clone-source","objectId":"{{new string('a', 64)}}:256"}]}""",
        $$"""{"objectType":"post","schemaVersion":"1.0","stateJson":{},"references":[{"relationship":"clone-source","objectId":"{{new string('a', 64)}}:0","objectId":"{{new string('b', 64)}}:0"}]}""",
        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":[],\"references\":[]}",
        "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":["
    };

    [Theory]
    [MemberData(nameof(MalformedReferencePayloads))]
    public void TryRead_MalformedReferenceClaim_ReportsInvalidReferences(string inlineJson)
    {
        bool parsed = BusinessObjectPayloadEnvelopeReader.TryRead(
            inlineJson,
            out _,
            out bool hasInvalidReferences);

        Assert.False(parsed);
        Assert.True(hasInvalidReferences);
    }

    [Theory]
    [InlineData("{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{}}")]
    [InlineData("{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":[]}")]
    public void TryRead_HistoricalEnvelopeWithoutReferences_RemainsValid(string inlineJson)
    {
        bool parsed = BusinessObjectPayloadEnvelopeReader.TryRead(
            inlineJson,
            out BusinessObjectPayloadEnvelope envelope,
            out bool hasInvalidReferences);

        Assert.True(parsed);
        Assert.False(hasInvalidReferences);
        Assert.Empty(envelope.References);
    }

    [Fact]
    public async Task Validate_MalformedReferenceClaim_IsRejectedInsteadOfIgnored()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            inlineJson: "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{},\"references\":{}}");

        await AssertInvalidAsync(context, "source references are malformed");
    }

    [Fact]
    public async Task Validate_CloneSourceReferencedWithoutConsumption_Succeeds()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source]);

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task Validate_CloneSourceConsumed_RejectsMutationOfSource()
    {
        LedgerTransaction source = CreateSource('a');
        BusinessObjectId sourceId = IdOf(source);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            consumed: [sourceId]);

        await AssertInvalidAsync(context, "without consuming or mutating");
    }

    [Fact]
    public async Task Validate_PreviousVersionNotConsumed_RejectsReference()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [source]);

        await AssertInvalidAsync(context, "previous-version source must be consumed");
    }

    [Theory]
    [InlineData(BusinessObjectOperation.Update)]
    [InlineData(BusinessObjectOperation.Transfer)]
    public async Task Validate_PreviousVersionFromSameConsumedLineage_Succeeds(
        BusinessObjectOperation operation)
    {
        LedgerTransaction source = CreateSource('a');
        string sourceLineage = source.Outputs[0].BusinessObject!.LineageKey.Value;
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [source],
            consumed: [IdOf(source)],
            outputs:
            [
                CreateTargetOutput(
                    operation,
                    TargetDataHash,
                    lineageKey: sourceLineage)
            ]);

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData(BusinessObjectOperation.Update)]
    [InlineData(BusinessObjectOperation.Transfer)]
    public async Task Validate_PreviousVersionFromDifferentConsumedLineage_RejectsReference(
        BusinessObjectOperation operation)
    {
        LedgerTransaction updatedSource = CreateSource('a');
        LedgerTransaction referencedSource = CreateSource('b');
        string updatedLineage = updatedSource.Outputs[0].BusinessObject!.LineageKey.Value;
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [updatedSource, referencedSource],
            references: [IdOf(referencedSource)],
            consumed: [IdOf(updatedSource), IdOf(referencedSource)],
            outputs:
            [
                CreateTargetOutput(
                    operation,
                    TargetDataHash,
                    lineageKey: updatedLineage)
            ]);

        await AssertInvalidAsync(
            context,
            "previous-version source must belong to the same lineage");
    }

    [Fact]
    public async Task Validate_PreviousVersionCannotSilentlyCloseUnrelatedConsumedBusinessObject()
    {
        LedgerTransaction updatedSource = CreateSource('a');
        LedgerTransaction unrelatedSource = CreateSource('b');
        string updatedLineage = updatedSource.Outputs[0].BusinessObject!.LineageKey.Value;
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [updatedSource, unrelatedSource],
            references: [IdOf(updatedSource)],
            consumed: [IdOf(updatedSource), IdOf(unrelatedSource)],
            outputs:
            [
                CreateTargetOutput(
                    BusinessObjectOperation.Update,
                    TargetDataHash,
                    lineageKey: updatedLineage)
            ]);

        await AssertInvalidAsync(
            context,
            $"cannot consume unrelated source {IdOf(unrelatedSource).Value} without re-emitting the same lineage");
    }

    [Fact]
    public async Task Validate_PreviousVersionAllowsUnrelatedConsumedBusinessObjectWhenReEmitted()
    {
        LedgerTransaction updatedSource = CreateSource('a');
        LedgerTransaction passThroughSource = CreateSource('b');
        string updatedLineage = updatedSource.Outputs[0].BusinessObject!.LineageKey.Value;
        TransactionOutput passThroughOutput = passThroughSource.Outputs[0] with
        {
            OutputIndex = 1,
            BusinessObject = passThroughSource.Outputs[0].BusinessObject! with
            {
                Version = 2,
                Operation = BusinessObjectOperation.Update
            }
        };
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [updatedSource, passThroughSource],
            references: [IdOf(updatedSource)],
            consumed: [IdOf(updatedSource), IdOf(passThroughSource)],
            outputs:
            [
                CreateTargetOutput(
                    BusinessObjectOperation.Update,
                    TargetDataHash,
                    lineageKey: updatedLineage),
                passThroughOutput
            ]);

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task Validate_MergeWithOnlyOneConsumedSource_RejectsOtherReference()
    {
        LedgerTransaction first = CreateSource('a');
        LedgerTransaction second = CreateSource('b');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.MergeSource,
            [first, second],
            consumed: [IdOf(first)]);

        await AssertInvalidAsync(context, $"merge-source source must be consumed by the transaction: {IdOf(second).Value}");
    }

    [Fact]
    public async Task Validate_MergeWithTwoDistinctConsumedSources_Succeeds()
    {
        LedgerTransaction first = CreateSource('a');
        LedgerTransaction second = CreateSource('b');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.MergeSource,
            [first, second],
            consumed: [IdOf(first), IdOf(second)]);

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task Validate_MixedSourceRelationships_RejectsPayload()
    {
        LedgerTransaction first = CreateSource('a');
        LedgerTransaction second = CreateSource('b');
        string mixedPayload = CreateEnvelope(
            "post",
            "1.0",
            [
                (BusinessObjectReferenceRelationships.CloneSource, IdOf(first)),
                (BusinessObjectReferenceRelationships.MergeSource, IdOf(second))
            ]);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [first, second],
            inlineJson: mixedPayload);

        await AssertInvalidAsync(context, "cannot mix source reference relationships");
    }

    [Fact]
    public async Task Validate_DuplicateMergeSource_RejectsNonDistinctReferences()
    {
        LedgerTransaction source = CreateSource('a');
        BusinessObjectId sourceId = IdOf(source);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.MergeSource,
            [source],
            references: [sourceId, sourceId],
            consumed: [sourceId]);

        await AssertInvalidAsync(context, "requires 2 distinct source reference");
    }

    [Fact]
    public async Task Validate_UnknownSourceRelationship_RejectsPayload()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            "derived-from",
            [source]);

        await AssertInvalidAsync(context, "Unknown BusinessObject source relationship");
    }

    [Fact]
    public async Task Validate_ReferenceToMissingSourceOutput_RejectsReference()
    {
        LedgerTransaction source = CreateSource('a');
        BusinessObjectId missingOutput = BusinessObjectId.FromOutPoint(source.TxId!.Value, 9);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            references: [missingOutput]);

        await AssertInvalidAsync(context, $"source output does not exist: {missingOutput.Value}");
    }

    [Theory]
    [InlineData("invoice", "1.0")]
    [InlineData("post", "2.0")]
    public async Task Validate_SourceTypeOrSchemaMismatch_RejectsReference(
        string sourceType,
        string sourceSchema)
    {
        LedgerTransaction source = CreateSource('a', sourceType, sourceSchema);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source]);

        await AssertInvalidAsync(context, "source type and schema must match");
    }

    [Theory]
    [InlineData("previous-version")]
    [InlineData("merge-source")]
    public async Task Validate_ConsumedSourceTypeMismatch_RejectsReference(string relationship)
    {
        LedgerTransaction mismatched = CreateSource('a', objectType: "invoice");
        LedgerTransaction[] sources = relationship == BusinessObjectReferenceRelationships.MergeSource
            ? [mismatched, CreateSource('b')]
            : [mismatched];
        LedgerTransactionValidationContext context = CreateContext(
            relationship,
            sources,
            consumed: sources.Select(IdOf).ToArray());

        await AssertInvalidAsync(context, "source type and schema must match");
    }

    [Fact]
    public async Task Validate_SourceEnvelopeNotBoundToReferencedOutput_RejectsReference()
    {
        LedgerTransaction source = CreateSource(
            'a',
            businessObjectDataHash: new HashHex("different-source-output-hash"));
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source]);

        await AssertInvalidAsync(context, "source payload is not bound to the referenced output data hash");
    }

    [Fact]
    public async Task Validate_TargetEnvelopeNotBoundToBusinessObjectOutput_RejectsReference()
    {
        LedgerTransaction source = CreateSource('a');
        TransactionOutput unboundTarget = CreateTargetOutput(
            BusinessObjectOperation.Create,
            new HashHex("different-target-output-hash"));
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            outputs: [unboundTarget]);

        await AssertInvalidAsync(context, "bind to exactly one BusinessObject output data hash");
    }

    [Fact]
    public async Task Validate_EnvelopeBoundToMultipleBusinessObjectOutputs_RejectsAmbiguousTarget()
    {
        LedgerTransaction source = CreateSource('a');
        TransactionOutput first = CreateTargetOutput(BusinessObjectOperation.Create, TargetDataHash);
        TransactionOutput second = CreateTargetOutput(
            BusinessObjectOperation.Create,
            TargetDataHash,
            outputIndex: 1,
            lineageKey: "second-target");
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            outputs: [first, second]);

        await AssertInvalidAsync(context, "bind to exactly one BusinessObject output data hash");
    }

    [Fact]
    public async Task Validate_PreviousVersionCloseWithNoBusinessObjectOutput_Succeeds()
    {
        LedgerTransaction source = CreateSource('a');
        BusinessObjectId sourceId = IdOf(source);
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [source],
            consumed: [sourceId],
            outputs: []);

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task Validate_PreviousVersionCloseWithoutConsumption_RejectsReference()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.PreviousVersion,
            [source],
            outputs: []);

        await AssertInvalidAsync(context, "previous-version source must be consumed");
    }

    [Fact]
    public async Task Validate_CloneWithNoBusinessObjectOutput_IsNotTreatedAsClose()
    {
        LedgerTransaction source = CreateSource('a');
        LedgerTransactionValidationContext context = CreateContext(
            BusinessObjectReferenceRelationships.CloneSource,
            [source],
            outputs: []);

        await AssertInvalidAsync(context, "bind to exactly one BusinessObject output data hash");
    }

    [Fact]
    public async Task Validate_HistoricalEnvelopeWithoutReferences_RemainsUnrestricted()
    {
        const string historicalEnvelope =
            "{\"objectType\":\"post\",\"schemaVersion\":\"1.0\",\"stateJson\":{\"name\":\"historical\"}}";
        LedgerTransaction transaction = CreateTargetTransaction(
            historicalEnvelope,
            [CreateTargetOutput(BusinessObjectOperation.Create, TargetDataHash)]);
        LedgerTransactionValidationContext context = new(
            transaction,
            CreateState(),
            ExistingTransaction: null,
            InputKeys: [],
            UnspentOutputs: new Dictionary<OutPointKey, UnspentOutput>(),
            SpentOutputs: new Dictionary<OutPointKey, SpentOutput>(),
            new FakeLedgerStore([]));

        ValidationResult result = await ValidateAsync(context);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("clone-source", BusinessObjectOperation.Update)]
    [InlineData("merge-source", BusinessObjectOperation.Transfer)]
    [InlineData("previous-version", BusinessObjectOperation.Create)]
    public async Task Validate_RelationshipWithIncompatibleTargetOperation_RejectsReference(
        string relationship,
        BusinessObjectOperation operation)
    {
        LedgerTransaction first = CreateSource('a');
        LedgerTransaction[] sources = relationship == BusinessObjectReferenceRelationships.MergeSource
            ? [first, CreateSource('b')]
            : [first];
        BusinessObjectId[] consumed = relationship == BusinessObjectReferenceRelationships.CloneSource
            ? []
            : sources.Select(IdOf).ToArray();
        LedgerTransactionValidationContext context = CreateContext(
            relationship,
            sources,
            consumed: consumed,
            outputs: [CreateTargetOutput(operation, TargetDataHash)]);

        await AssertInvalidAsync(context, $"cannot produce a {operation} output");
    }

    private static async Task<ValidationResult> ValidateAsync(LedgerTransactionValidationContext context)
        => await new BusinessObjectReferenceValidator().ValidateAsync(context, CancellationToken.None);

    private static async Task AssertInvalidAsync(
        LedgerTransactionValidationContext context,
        string expectedMessage)
    {
        ValidationResult result = await ValidateAsync(context);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.InvalidBusinessObjectTransition
                && error.Message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    private static LedgerTransactionValidationContext CreateContext(
        string relationship,
        IReadOnlyList<LedgerTransaction> storedSources,
        IReadOnlyList<BusinessObjectId>? references = null,
        IReadOnlyCollection<BusinessObjectId>? consumed = null,
        IReadOnlyList<TransactionOutput>? outputs = null,
        string? inlineJson = null)
    {
        BusinessObjectId[] sourceReferences = references?.ToArray()
            ?? storedSources.Select(IdOf).ToArray();
        string payload = inlineJson ?? CreateEnvelope(
            "post",
            "1.0",
            sourceReferences.Select(reference => (relationship, reference)).ToArray());
        BusinessObjectOperation targetOperation = relationship == BusinessObjectReferenceRelationships.PreviousVersion
            ? BusinessObjectOperation.Update
            : BusinessObjectOperation.Create;
        LedgerTransaction transaction = CreateTargetTransaction(
            payload,
            outputs ?? [CreateTargetOutput(targetOperation, TargetDataHash)]);

        var inputKeys = new List<OutPointKey>();
        var unspent = new Dictionary<OutPointKey, UnspentOutput>();
        foreach (BusinessObjectId consumedId in consumed ?? [])
        {
            OutPoint outPoint = consumedId.RequireOutPoint();
            LedgerTransaction? source = storedSources.SingleOrDefault(item => item.TxId == outPoint.TxId);
            TransactionOutput? sourceOutput = source?.Outputs.SingleOrDefault(item => item.OutputIndex == outPoint.OutputIndex);
            if (sourceOutput?.BusinessObject is null)
            {
                continue;
            }

            var key = new OutPointKey(new HashHex($"input-key-{inputKeys.Count}"));
            inputKeys.Add(key);
            unspent[key] = new UnspentOutput(
                key,
                LedgerId,
                ScenarioId: null,
                outPoint,
                sourceOutput.Address,
                sourceOutput.MainAmount,
                sourceOutput.Token,
                sourceOutput.BusinessObject,
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"));
        }

        return new LedgerTransactionValidationContext(
            transaction,
            CreateState(),
            ExistingTransaction: null,
            inputKeys,
            unspent,
            new Dictionary<OutPointKey, SpentOutput>(),
            new FakeLedgerStore(storedSources));
    }

    private static LedgerTransaction CreateSource(
        char transactionIdCharacter,
        string objectType = "post",
        string schemaVersion = "1.0",
        HashHex? businessObjectDataHash = null)
    {
        var txId = new TxId(new string(transactionIdCharacter, 64));
        var dataHash = new HashHex($"source-data-{transactionIdCharacter}");
        string inlineJson = CreateEnvelope(objectType, schemaVersion, []);
        return new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
            Inputs: [],
            Outputs:
            [
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.Zero,
                    Token: null,
                    new BusinessObjectRef(
                        new BusinessObjectLineageKey($"source-{transactionIdCharacter}"),
                        1,
                        BusinessObjectOperation.Create,
                        businessObjectDataHash ?? dataHash))
            ],
            new TransactionData(
                "application/json",
                dataHash,
                inlineJson,
                ExternalRef: null,
                TransactionDataItemType.BusinessObject),
            txId);
    }

    private static LedgerTransaction CreateTargetTransaction(
        string inlineJson,
        IReadOnlyList<TransactionOutput> outputs)
        => new(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-07-11T00:00:00Z"),
            Inputs: [],
            outputs,
            new TransactionData(
                "application/json",
                TargetDataHash,
                inlineJson,
                ExternalRef: null,
                TransactionDataItemType.BusinessObject),
            new TxId(new string('f', 64)));

    private static TransactionOutput CreateTargetOutput(
        BusinessObjectOperation operation,
        HashHex dataHash,
        int outputIndex = 0,
        string lineageKey = "target")
        => new(
            outputIndex,
            Bob,
            Amount.Zero,
            Token: null,
            new BusinessObjectRef(
                new BusinessObjectLineageKey(lineageKey),
                operation == BusinessObjectOperation.Create ? 1 : 2,
                operation,
                dataHash));

    private static BusinessObjectId IdOf(LedgerTransaction transaction)
        => BusinessObjectId.FromOutPoint(transaction.TxId!.Value, 0);

    private static string CreateEnvelope(
        string objectType,
        string schemaVersion,
        IReadOnlyList<(string Relationship, BusinessObjectId ObjectId)> references)
        => JsonSerializer.Serialize(new
        {
            objectType,
            schemaVersion,
            stateJson = new { name = "payload" },
            references = references.Select(reference => new
            {
                relationship = reference.Relationship,
                objectId = reference.ObjectId.Value
            })
        });

    private static LedgerState CreateState()
        => new(
            LedgerId,
            ScenarioId: null,
            CurrentSequence: 1,
            new HashHex("ledger-hash"),
            DateTimeOffset.Parse("2026-07-11T00:00:00Z"));

    private sealed class FakeLedgerStore : ILedgerStore
    {
        private readonly IReadOnlyDictionary<TxId, LedgerTransaction> transactions;

        public FakeLedgerStore(IEnumerable<LedgerTransaction> transactions)
        {
            this.transactions = transactions.ToDictionary(transaction => transaction.TxId!.Value);
        }

        public Task<LedgerTransaction?> GetTransactionAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            TxId txId,
            CancellationToken cancellationToken)
            => Task.FromResult(transactions.GetValueOrDefault(txId));

        public Task<LedgerState?> GetLedgerStateAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RegisteredAddress?> GetRegisteredAddressAsync(
            LedgerId ledgerId,
            Address address,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> GetUnspentOutputsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            IReadOnlyList<OutPointKey> outPointKeys,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> GetSpentOutputsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            IReadOnlyList<OutPointKey> outPointKeys,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<UnspentOutput>> GetUnspentOutputsByAddressAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            Address address,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> TokenExistsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            TokenId tokenId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RegisteredTokenDefinition?> GetTokenDefinitionAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            TokenId tokenId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AppendConfirmedTransactionAsync(
            ConfirmedTransaction confirmedTransaction,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
