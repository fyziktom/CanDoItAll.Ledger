using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed class BusinessObjectTransactionBuilder(
    ILedgerReadClient ledgerReadClient,
    ITransactionHasher transactionHasher,
    ISignaturePayloadBuilder signaturePayloadBuilder,
    IAccountInputSigner inputSigner,
    ITimeProvider timeProvider,
    ITokenDefinitionHasher? providedTokenDefinitionHasher = null,
    IBusinessObjectLineageKeyFactory? providedLineageKeyFactory = null)
{
    private readonly ITokenDefinitionHasher tokenDefinitionHasher = providedTokenDefinitionHasher
        ?? new CanDoItAll.Economy.Cryptography.TokenDefinitionHasher(
            new CanDoItAll.Economy.Cryptography.DefaultTokenDefinitionCanonicalizer());

    private readonly IBusinessObjectLineageKeyFactory lineageKeyFactory = providedLineageKeyFactory
        ?? RandomBusinessObjectLineageKeyFactory.Instance;

    private readonly IAccountTransactionSigner transactionSigner = new AccountTransactionSigner(
        transactionHasher,
        signaturePayloadBuilder,
        inputSigner);

    public async Task<LedgerTransaction> BuildCreateAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CreateBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedCreateAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedCreateAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CreateBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        request.MainAmount?.EnsureNonNegative(nameof(request.MainAmount));
        request.Token?.Amount.EnsureNonNegative(nameof(request.Token));

        TransactionData data = BusinessObjectPayload.CreateData(request);
        Amount mainAmount = request.MainAmount ?? Amount.Zero;
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        TokenTransfer carrierToken = ResolveBusinessObjectCarrierToken(
            spendableOutputs,
            request.SourceOutPoint,
            request.Token);
        IReadOnlyList<LedgerSpendableOutput> selected = SelectCreateInputs(
            spendableOutputs,
            request.SourceOutPoint,
            mainAmount,
            carrierToken);

        decimal selectedMain = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        if (selectedMain < mainAmount.Value)
        {
            throw new InvalidOperationException("BusinessObject create main amount cannot exceed the selected input amount.");
        }

        Address ownerAddress = request.OwnerAddress ?? keySource.Address;
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                ownerAddress,
                Amount.Zero,
                carrierToken,
                new BusinessObjectRef(
                    request.CompatibilityLineageKey ?? lineageKeyFactory.Create(),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
        };

        AddMainOutputIfPositive(outputs, ownerAddress, mainAmount);

        AddSelectedChangeOutput(outputs, selected, keySource.Address, mainAmount, carrierToken);

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.BusinessObject, data);
        return unsigned;
    }

    public async Task<LedgerTransaction> BuildTransferAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TransferBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedTransferAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedTransferAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TransferBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        request.MainAmount?.EnsureNonNegative(nameof(request.MainAmount));

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        LedgerSpendableOutput selected = FindOwnedBusinessObject(
            spendableOutputs,
            request.ObjectId,
            keySource.Address);
        EnsureBusinessObjectCarrier(selected);
        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(selected.OutPoint);

        Amount amountToReceiver = request.MainAmount ?? selected.MainAmount;
        var selectedInputs = new List<LedgerSpendableOutput> { selected };
        decimal selectedMain = selected.MainAmount.Value;
        if (amountToReceiver.Value > selectedMain)
        {
            Amount remainingMain = new(amountToReceiver.Value - selectedMain);
            selectedInputs.AddRange(SelectMainOutputs(
                spendableOutputs
                    .Where(output => output.OutPoint != selected.OutPoint)
                    .ToArray(),
                remainingMain));
            selectedMain = selectedInputs.Sum(output => output.MainAmount.Value);
        }

        BusinessObjectPayloadDescriptor descriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            descriptor.ObjectType,
            descriptor.SchemaVersion,
            descriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.PreviousVersion, sourceObjectId)],
            itemType: descriptor.ItemType,
            externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress,
                Amount.Zero,
                selected.Token,
                selected.BusinessObject! with
                {
                    Version = selected.BusinessObject.Version + 1,
                    Operation = BusinessObjectOperation.Transfer,
                    DataHash = data.DataHash ?? BusinessObjectPayload.HashJson(descriptor.StateJson)
                })
        };

        AddMainOutputIfPositive(outputs, request.ReceiverAddress, amountToReceiver);

        Amount change = new(selectedMain - amountToReceiver.Value);
        AddMainOutputIfPositive(outputs, keySource.Address, change);

        var unsigned = CreateMultiInputTransaction(
            context,
            keySource,
            selectedInputs,
            outputs,
            TransactionKind.BusinessObject,
            data);
        return unsigned;
    }

    public async Task<LedgerTransaction> BuildPaymentAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedPaymentAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedPaymentAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LedgerSpendableOutput> selected = SelectMainOutputs(spendableOutputs, request.Amount);
        decimal selectedTotal = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        decimal change = selectedTotal - request.Amount.Value;

        var outputs = new List<TransactionOutput>
        {
            new(0, request.ReceiverAddress, request.Amount)
        };

        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(1, keySource.Address, new Amount(change)));
        }

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.Transfer, data: null);
        return unsigned;
    }

    public async Task<LedgerTransaction> BuildTokenTransferAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TokenTransferRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedTokenTransferAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedTokenTransferAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TokenTransferRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.TokenAmount, nameof(request.TokenAmount));
        Amount mainToReceiver = request.MainAmount ?? Amount.Zero;
        mainToReceiver.EnsureNonNegative(nameof(request.MainAmount));

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        var selected = SelectTokenTransferInputs(
            spendableOutputs,
            request.TokenId,
            request.TokenAmount,
            mainToReceiver);
        decimal mainInput = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        if (mainToReceiver.Value > mainInput)
        {
            throw new InvalidOperationException("Token transfer main amount cannot exceed the selected input amount.");
        }

        decimal tokenInput = selected.Aggregate(
            0m,
            (sum, output) => output.Token?.TokenId == request.TokenId
                ? sum + output.Token.Amount.Value
                : sum);
        decimal tokenChange = tokenInput - request.TokenAmount.Value;
        decimal mainChange = mainInput - mainToReceiver.Value;

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress,
                Amount.Zero,
                new TokenTransfer(request.TokenId, request.TokenAmount))
        };

        AddMainOutputIfPositive(outputs, request.ReceiverAddress, mainToReceiver);
        AddTokenOutputIfPositive(outputs, keySource.Address, request.TokenId, new Amount(tokenChange));
        AddMainOutputIfPositive(outputs, keySource.Address, new Amount(mainChange));

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.Transfer, data: null);
        return unsigned;
    }

    public async Task<LedgerTransaction> BuildUtxoMergeAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UtxoMergeRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedUtxoMergeAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedUtxoMergeAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UtxoMergeRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> selected = await SelectExplicitPlainMainUtxosAsync(
            context,
            keySource,
            request.SourceOutPoints,
            minimumCount: 2,
            cancellationToken).ConfigureAwait(false);
        decimal selectedTotal = selected.Sum(output => output.MainAmount.Value);
        if (selectedTotal <= 0m)
        {
            throw new InvalidOperationException("Selected UTXO total must be positive.");
        }

        var outputs = new[]
        {
            new TransactionOutput(0, request.ReceiverAddress ?? keySource.Address, new Amount(selectedTotal))
        };

        return CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.Transfer, data: null);
    }

    public async Task<LedgerTransaction> BuildUtxoSplitAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UtxoSplitRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedUtxoSplitAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedUtxoSplitAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UtxoSplitRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Outputs.Count == 0)
        {
            throw new InvalidOperationException("At least one split output is required.");
        }

        foreach (UtxoSplitOutput output in request.Outputs)
        {
            EnsurePositive(output.Amount, nameof(request.Outputs));
        }

        IReadOnlyList<LedgerSpendableOutput> selected = await SelectExplicitPlainMainUtxosAsync(
            context,
            keySource,
            request.SourceOutPoints,
            minimumCount: 1,
            cancellationToken).ConfigureAwait(false);
        decimal selectedTotal = selected.Sum(output => output.MainAmount.Value);
        decimal outputTotal = request.Outputs.Sum(output => output.Amount.Value);
        if (outputTotal > selectedTotal)
        {
            throw new InvalidOperationException("Split output total cannot exceed the selected UTXO amount.");
        }

        var outputs = request.Outputs
            .Select((output, index) => new TransactionOutput(index, output.ReceiverAddress, output.Amount))
            .ToList();

        decimal change = selectedTotal - outputTotal;
        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(outputs.Count, keySource.Address, new Amount(change)));
        }

        return CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.Transfer, data: null);
    }

    public async Task<LedgerTransaction> BuildTokenMintAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TokenMintRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedTokenMintAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedTokenMintAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TokenMintRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.TokenAmount, nameof(request.TokenAmount));
        Amount mainAmount = request.MainAmount ?? Amount.Zero;
        mainAmount.EnsureNonNegative(nameof(request.MainAmount));
        if (mainAmount.Value > 0m)
        {
            throw new InvalidOperationException("Permission-holder token minting cannot mint main ledger amount.");
        }

        TokenId tokenId = request.TokenDefinition is null
            ? request.TokenId
            : tokenDefinitionHasher.CalculateTokenId(request.TokenDefinition.Definition);
        if (request.TokenDefinition is not null)
        {
            if (request.TokenId != tokenId)
            {
                throw new InvalidOperationException($"Protocol-v2 token id must be the hash-derived id {tokenId.Value}.");
            }

            if (request.TokenDefinition.Definition.InitialSupply != request.TokenAmount)
            {
                throw new InvalidOperationException("Protocol-v2 token amount must equal the immutable initial supply.");
            }

            if (request.TokenDefinition.Definition.Issuer != keySource.Address)
            {
                throw new InvalidOperationException("Protocol-v2 token issuer must be the account consuming the mint permission.");
            }
        }

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .GetTransactionsAsync(new LedgerAddressQuery(context, keySource.Address), includeData: true, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LedgerPermissionSource> permissionSources = PermissionSourceResolver.Resolve(spendableOutputs, transactions);
        LedgerPermissionSource permission = request.TokenDefinition is null
            ? PermissionSourceResolver.SelectMintPermissionSource(
                permissionSources,
                tokenId,
                request.PermissionSourceOutPoint)
            : PermissionSourceResolver.SelectMintPermissionSource(
                permissionSources,
                request.TokenDefinition.Definition.Namespace,
                tokenId.Value,
                request.PermissionSourceOutPoint);
        BusinessObjectRef permissionRef = permission.Output.BusinessObject
            ?? throw new InvalidOperationException("Permission source output is missing its BusinessObject reference.");

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress ?? keySource.Address,
                mainAmount,
                new TokenTransfer(tokenId, request.TokenAmount))
        };

        if (permission.Grant.Repeatable)
        {
            outputs.Add(new TransactionOutput(
                outputs.Count,
                keySource.Address,
                Amount.Zero,
                permission.Output.Token,
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
            AddMainOutputIfPositive(outputs, keySource.Address, permission.Output.MainAmount);
        }

        var unsigned = new LedgerTransaction(
            context.LedgerId,
            context.ScenarioId,
            Version: request.TokenDefinition is null ? 1 : 2,
            TransactionKind.Mint,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, permission.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            outputs,
            Data: null,
            TokenDefinition: request.TokenDefinition);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildPermissionIssueAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        PermissionIssueRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedPermissionIssueAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedPermissionIssueAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        PermissionIssueRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.Amount, nameof(request.Amount));
        PermissionGrant grant = new(
            PermissionTokenAuthority.NormalizeAction(request.Action),
            PermissionTokenAuthority.NormalizeNamespace(request.TokenNamespace),
            request.Repeatable);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .GetTransactionsAsync(new LedgerAddressQuery(context, keySource.Address), includeData: true, cancellationToken)
            .ConfigureAwait(false);

        LedgerSpendableOutput source = SelectPermissionIssueSource(
            spendableOutputs,
            PermissionSourceResolver.Resolve(spendableOutputs, transactions),
            grant,
            request.Amount,
            request.SourceOutPoint);
        decimal sourceAmount = source.Token!.Amount.Value;
        decimal change = sourceAmount - request.Amount.Value;
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, request.Amount),
                new BusinessObjectRef(
                    lineageKeyFactory.Create(),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash!.Value))
        };

        if (change > 0m)
        {
            BusinessObjectRef? changeBusinessObject = source.BusinessObject is null
                ? null
                : source.BusinessObject with
                {
                    Version = source.BusinessObject.Version + 1,
                    Operation = BusinessObjectOperation.Update
                };
            outputs.Add(new TransactionOutput(
                outputs.Count,
                keySource.Address,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change)),
                changeBusinessObject));
            AddMainOutputIfPositive(outputs, keySource.Address, source.MainAmount);
        }

        var unsigned = new LedgerTransaction(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, source.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            outputs,
            data);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildCreateRuleTokenAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        RuleTokenCreateRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedCreateRuleTokenAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedCreateRuleTokenAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        RuleTokenCreateRequest request,
        CancellationToken cancellationToken)
    {
        AccountRuleDefinition rule = RuleTokenAuthority.Normalize(request.Rule);
        TransactionData data = RuleTokenAuthority.CreateData(rule);

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .GetTransactionsAsync(new LedgerAddressQuery(context, keySource.Address), includeData: true, cancellationToken)
            .ConfigureAwait(false);

        LedgerPermissionSource permission = PermissionSourceResolver.SelectRuleCreationPermissionSource(
            PermissionSourceResolver.Resolve(spendableOutputs, transactions),
            rule.RuleNamespace,
            request.PermissionSourceOutPoint);
        BusinessObjectRef permissionRef = permission.Output.BusinessObject
            ?? throw new InvalidOperationException("Rule permission source output is missing its BusinessObject reference.");

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.OwnerAddress ?? keySource.Address,
                Amount.Zero,
                new TokenTransfer(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1)),
                new BusinessObjectRef(
                    lineageKeyFactory.Create(),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash!.Value))
        };

        decimal remainingPermission = permission.Amount.Value - 1m;
        if (remainingPermission > 0m)
        {
            outputs.Add(new TransactionOutput(
                1,
                keySource.Address,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(remainingPermission)),
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
            AddMainOutputIfPositive(outputs, keySource.Address, permission.Output.MainAmount);
        }

        var unsigned = new LedgerTransaction(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, permission.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            outputs,
            data);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildUpdateAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UpdateBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedUpdateAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedUpdateAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UpdateBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        EnsureBusinessObjectCarrier(selected);
        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(selected.OutPoint);
        BusinessObjectPayloadDescriptor descriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            descriptor.ObjectType,
            descriptor.SchemaVersion,
            descriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.PreviousVersion, sourceObjectId)],
            itemType: descriptor.ItemType,
            externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                keySource.Address,
                Amount.Zero,
                selected.Token,
                selected.BusinessObject! with
                {
                    Version = selected.BusinessObject.Version + 1,
                    Operation = BusinessObjectOperation.Update,
                    DataHash = data.DataHash ?? BusinessObjectPayload.HashJson(descriptor.StateJson)
                })
        };
        AddMainOutputIfPositive(outputs, keySource.Address, selected.MainAmount);

        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            selected,
            outputs,
            data);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildCloseAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloseBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedCloseAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedCloseAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloseBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        LedgerSpendableOutput selected = FindOwnedBusinessObject(
            spendableOutputs,
            request.ObjectId,
            keySource.Address);
        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(selected.OutPoint);
        BusinessObjectPayloadDescriptor descriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            descriptor.ObjectType,
            descriptor.SchemaVersion,
            descriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.PreviousVersion, sourceObjectId)],
            itemType: descriptor.ItemType,
            externalRef: request.ExternalRef);

        LedgerSpendableOutput? mergeSource = selected.Token is null
            ? null
            : SelectTokenMergeOutput(spendableOutputs, selected.Token.TokenId);
        if (mergeSource is null)
        {
            if (selected.Token is not null)
            {
                throw new InvalidOperationException($"Closing token-backed BusinessObject {request.ObjectId.Value} requires a non-BusinessObject token source to merge into.");
            }

            var legacyCloseOutputs = new List<TransactionOutput>();
            AddMainOutputIfPositive(legacyCloseOutputs, keySource.Address, selected.MainAmount);
            return CreateSingleInputTransaction(context, keySource, selected, legacyCloseOutputs, data);
        }

        var outputToken = new TokenTransfer(
            selected.Token!.TokenId,
            new Amount(selected.Token.Amount.Value + mergeSource.Token!.Amount.Value));
        var outputMain = new Amount(selected.MainAmount.Value + mergeSource.MainAmount.Value);
        var closeOutputs = new List<TransactionOutput>
        {
            new(0, keySource.Address, Amount.Zero, outputToken)
        };
        AddMainOutputIfPositive(closeOutputs, keySource.Address, outputMain);
        return CreateMultiInputTransaction(
            context,
            keySource,
            [selected, mergeSource],
            closeOutputs,
            TransactionKind.BusinessObject,
            data);
    }

    public async Task<LedgerTransaction> BuildCloneAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloneBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedCloneAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedCloneAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloneBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        request.Token?.Amount.EnsureNonNegative(nameof(request.Token));
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        BusinessObjectId sourceObjectId = ResolveCanonicalSourceId(
            spendableOutputs,
            request.SourceObjectId);
        await EnsureReferencedBusinessObjectExistsAsync(
            context,
            sourceObjectId,
            cancellationToken).ConfigureAwait(false);
        BusinessObjectPayloadDescriptor sourceDescriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            requestedObjectType: null,
            requestedSchemaVersion: null,
            requestedStateJson: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.Equals(sourceDescriptor.ObjectType, request.ObjectType, StringComparison.Ordinal)
            || !string.Equals(sourceDescriptor.SchemaVersion, request.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A cloned BusinessObject must preserve the source object type and schema version.");
        }

        sourceDescriptor = sourceDescriptor with { StateJson = request.StateJson };
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            sourceDescriptor.ObjectType,
            sourceDescriptor.SchemaVersion,
            sourceDescriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.CloneSource, sourceObjectId)],
            itemType: sourceDescriptor.ItemType);
        TokenTransfer carrierToken = ResolveBusinessObjectCarrierToken(
            spendableOutputs,
            request.SourceOutPoint,
            request.Token);
        LedgerSpendableOutput tokenSource = request.SourceOutPoint is null
            ? SelectTokenSourceOutput(spendableOutputs, carrierToken.TokenId, carrierToken.Amount, Amount.Zero)
            : SelectExplicitCreateSource(spendableOutputs, request.SourceOutPoint, Amount.Zero, carrierToken);

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.OwnerAddress ?? keySource.Address,
                Amount.Zero,
                carrierToken,
                new BusinessObjectRef(
                    request.CompatibilityLineageKey ?? lineageKeyFactory.Create(),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
        };

        AddSelectedChangeOutput(outputs, [tokenSource], keySource.Address, Amount.Zero, carrierToken);

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, [tokenSource], outputs, TransactionKind.BusinessObject, data);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildMergeAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        MergeBusinessObjectsRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedMergeAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedMergeAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        MergeBusinessObjectsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FirstObjectId == request.SecondObjectId)
        {
            throw new InvalidOperationException("Merge requires two distinct BusinessObjects.");
        }

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        LedgerSpendableOutput first = FindOwnedBusinessObject(
            spendableOutputs,
            request.FirstObjectId,
            keySource.Address);
        LedgerSpendableOutput second = FindOwnedBusinessObject(
            spendableOutputs,
            request.SecondObjectId,
            keySource.Address);
        BusinessObjectId firstObjectId = BusinessObjectId.FromOutPoint(first.OutPoint);
        BusinessObjectId secondObjectId = BusinessObjectId.FromOutPoint(second.OutPoint);
        BusinessObjectPayloadDescriptor firstDescriptor = await ResolvePayloadDescriptorAsync(
            context,
            firstObjectId,
            requestedObjectType: null,
            requestedSchemaVersion: null,
            requestedStateJson: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        BusinessObjectPayloadDescriptor secondDescriptor = await ResolvePayloadDescriptorAsync(
            context,
            secondObjectId,
            requestedObjectType: null,
            requestedSchemaVersion: null,
            requestedStateJson: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!string.Equals(firstDescriptor.ObjectType, secondDescriptor.ObjectType, StringComparison.Ordinal)
            || !string.Equals(firstDescriptor.SchemaVersion, secondDescriptor.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Merge requires source BusinessObjects with the same object type and schema version.");
        }

        string targetObjectType = request.CompatibilityLineageKey is null
            ? request.ObjectType
            : firstDescriptor.ObjectType;
        string targetSchemaVersion = request.CompatibilityLineageKey is null
            ? request.SchemaVersion
            : firstDescriptor.SchemaVersion;
        if (!string.Equals(targetObjectType, firstDescriptor.ObjectType, StringComparison.Ordinal)
            || !string.Equals(targetSchemaVersion, firstDescriptor.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Merged BusinessObject type and schema must match both source BusinessObjects.");
        }

        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            targetObjectType,
            targetSchemaVersion,
            request.StateJson,
            [
                SourceReference(BusinessObjectReferenceRelationships.MergeSource, firstObjectId),
                SourceReference(BusinessObjectReferenceRelationships.MergeSource, secondObjectId)
            ],
            itemType: firstDescriptor.ItemType,
            externalRef: request.ExternalRef);
        var selected = new List<LedgerSpendableOutput> { first, second };
        TokenTransfer carrierToken;
        if (first.Token is null && second.Token is null)
        {
            carrierToken = ResolveBusinessObjectCarrierToken(spendableOutputs, sourceOutPoint: null, requestedToken: null);
            LedgerSpendableOutput tokenSource = SelectTokenSourceOutput(spendableOutputs, carrierToken.TokenId, carrierToken.Amount, Amount.Zero);
            selected.Add(tokenSource);
        }
        else
        {
            EnsureBusinessObjectCarrier(first);
            EnsureBusinessObjectCarrier(second);
            if (first.Token!.TokenId != second.Token!.TokenId)
            {
                throw new NotSupportedException("Merging token-bearing BusinessObject outputs requires both carriers to use the same token id.");
            }

            carrierToken = new TokenTransfer(first.Token.TokenId, Amount.FromInt64(1));
            LedgerSpendableOutput? mergeSource = SelectTokenMergeOutput(spendableOutputs, carrierToken.TokenId);
            if (mergeSource is not null)
            {
                selected.Add(mergeSource);
            }
        }

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                keySource.Address,
                Amount.Zero,
                carrierToken,
                new BusinessObjectRef(
                    request.CompatibilityLineageKey ?? lineageKeyFactory.Create(),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
        };
        AddMainOutputIfPositive(outputs, keySource.Address, new Amount(first.MainAmount.Value + second.MainAmount.Value));
        decimal tokenInput = selected
            .Where(output => output.Token?.TokenId == carrierToken.TokenId)
            .Sum(output => output.Token!.Amount.Value);
        AddTokenOutputIfPositive(outputs, keySource.Address, carrierToken.TokenId, new Amount(tokenInput - carrierToken.Amount.Value));

        var unsigned = CreateMultiInputTransaction(
            context,
            keySource,
            selected,
            outputs,
            TransactionKind.BusinessObject,
            data);

        return unsigned;
    }

    public async Task<LedgerTransaction> BuildSplitPaymentAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        SplitBusinessObjectPaymentRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedSplitPaymentAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedSplitPaymentAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        SplitBusinessObjectPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Payments.Count == 0)
        {
            throw new InvalidOperationException("At least one payment output is required.");
        }

        foreach (BusinessObjectPaymentOutput payment in request.Payments)
        {
            if (payment.Amount.Value <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Payments), "Split payment amounts must be positive.");
            }
        }

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        LedgerSpendableOutput selected = FindOwnedBusinessObject(
            spendableOutputs,
            request.ObjectId,
            keySource.Address);
        EnsureBusinessObjectCarrier(selected);
        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(selected.OutPoint);
        decimal paymentTotal = request.Payments.Aggregate(0m, (sum, payment) => sum + payment.Amount.Value);
        var selectedInputs = new List<LedgerSpendableOutput> { selected };
        decimal selectedMain = selected.MainAmount.Value;
        if (paymentTotal > selectedMain)
        {
            Amount remainingMain = new(paymentTotal - selectedMain);
            selectedInputs.AddRange(SelectMainOutputs(
                spendableOutputs
                    .Where(output => output.OutPoint != selected.OutPoint)
                    .ToArray(),
                remainingMain));
            selectedMain = selectedInputs.Sum(output => output.MainAmount.Value);
        }

        BusinessObjectPayloadDescriptor descriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            descriptor.ObjectType,
            descriptor.SchemaVersion,
            descriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.PreviousVersion, sourceObjectId)],
            itemType: descriptor.ItemType,
            externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>();

        BusinessObjectPaymentOutput carrierPayment = request.Payments[0];
        outputs.Add(new TransactionOutput(
            0,
            carrierPayment.ReceiverAddress,
            Amount.Zero,
            selected.Token,
            selected.BusinessObject! with
            {
                Version = selected.BusinessObject.Version + 1,
                Operation = BusinessObjectOperation.Transfer,
                DataHash = data.DataHash ?? BusinessObjectPayload.HashJson(descriptor.StateJson)
            }));

        for (var i = 0; i < request.Payments.Count; i++)
        {
            BusinessObjectPaymentOutput payment = request.Payments[i];
            outputs.Add(new TransactionOutput(
                outputs.Count,
                payment.ReceiverAddress,
                payment.Amount));
        }

        decimal change = selectedMain - paymentTotal;
        AddMainOutputIfPositive(outputs, keySource.Address, new Amount(change));

        var unsigned = CreateMultiInputTransaction(
            context,
            keySource,
            selectedInputs,
            outputs,
            TransactionKind.BusinessObject,
            data);
        return unsigned;
    }

    public async Task<LedgerTransaction> BuildSetPriceAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        SetBusinessObjectPriceRequest request,
        CancellationToken cancellationToken)
    {
        LedgerTransaction unsigned = await BuildUnsignedSetPriceAsync(context, keySource, request, cancellationToken).ConfigureAwait(false);
        return await transactionSigner.SignAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUnsignedSetPriceAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        SetBusinessObjectPriceRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.PriceAmount, nameof(request.PriceAmount));

        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        EnsureBusinessObjectCarrier(selected);
        BusinessObjectId sourceObjectId = BusinessObjectId.FromOutPoint(selected.OutPoint);
        BusinessObjectPayloadDescriptor descriptor = await ResolvePayloadDescriptorAsync(
            context,
            sourceObjectId,
            request.ObjectType,
            request.SchemaVersion,
            request.StateJson,
            cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateStructuredData(
            descriptor.ObjectType,
            descriptor.SchemaVersion,
            descriptor.StateJson,
            [SourceReference(BusinessObjectReferenceRelationships.PreviousVersion, sourceObjectId)],
            itemType: descriptor.ItemType,
            externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                keySource.Address,
                Amount.Zero,
                selected.Token,
                selected.BusinessObject! with
                {
                    Version = selected.BusinessObject.Version + 1,
                    Operation = BusinessObjectOperation.Update,
                    DataHash = data.DataHash ?? BusinessObjectPayload.HashJson(descriptor.StateJson)
                })
        };
        AddMainOutputIfPositive(outputs, keySource.Address, selected.MainAmount);

        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            selected,
            outputs,
            data);

        return unsigned;
    }

    private async Task<LedgerSpendableOutput> GetOwnedBusinessObjectAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        BusinessObjectId objectId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        return FindOwnedBusinessObject(spendableOutputs, objectId, keySource.Address);
    }

    private static LedgerSpendableOutput FindOwnedBusinessObject(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        BusinessObjectId objectId,
        Address ownerAddress)
    {
        if (objectId.TryGetOutPoint(out OutPoint outPoint))
        {
            return spendableOutputs.FirstOrDefault(output =>
                    output.BusinessObject is not null && output.OutPoint == outPoint)
                ?? throw new InvalidOperationException($"BusinessObject is not owned by {ownerAddress.Value}: {objectId.Value}.");
        }

        // V1/V2 compatibility for callers that still hold the former stable ID. Public UI/API
        // boundaries reject this shape and all responses expose the canonical current outpoint.
        return spendableOutputs.FirstOrDefault(output =>
                string.Equals(output.BusinessObject?.LineageKey.Value, objectId.Value, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"BusinessObject is not owned by {ownerAddress.Value}: {objectId.Value}.");
    }

    private static BusinessObjectId ResolveCanonicalSourceId(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        BusinessObjectId requestedObjectId)
    {
        if (requestedObjectId.TryGetOutPoint(out _))
        {
            return requestedObjectId;
        }

        LedgerSpendableOutput legacySource = spendableOutputs.FirstOrDefault(output =>
                string.Equals(
                    output.BusinessObject?.LineageKey.Value,
                    requestedObjectId.Value,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Legacy BusinessObject lineage alias could not be resolved: {requestedObjectId.Value}.");
        return BusinessObjectId.FromOutPoint(legacySource.OutPoint);
    }

    private async Task<BusinessObjectPayloadDescriptor> ResolvePayloadDescriptorAsync(
        LedgerAccountContext context,
        BusinessObjectId objectId,
        string? requestedObjectType,
        string? requestedSchemaVersion,
        string? requestedStateJson,
        CancellationToken cancellationToken)
    {
        objectId.RequireOutPoint();
        LedgerBusinessObjectHolding? holding = null;
        try
        {
            holding = await ledgerReadClient.GetBusinessObjectAsync(
                new LedgerBusinessObjectQuery(context, ObjectId: objectId, Take: 1),
                cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            // Older read clients can still supply the source transaction below.
        }

        BusinessObjectPayloadEnvelope? envelope = null;
        TransactionDataItemType? sourceItemType = null;
        try
        {
            OutPoint outPoint = objectId.RequireOutPoint();
            LedgerTransactionSummary? sourceTransaction = await ledgerReadClient.GetTransactionAsync(
                context,
                outPoint.TxId,
                includeData: true,
                cancellationToken).ConfigureAwait(false);
            if (sourceTransaction?.Outputs.Any(output =>
                    output.OutputIndex == outPoint.OutputIndex && output.HasBusinessObject) == true
                && BusinessObjectPayloadEnvelopeReader.TryRead(sourceTransaction.Data?.InlineJson, out BusinessObjectPayloadEnvelope parsed))
            {
                envelope = parsed;
            }

            sourceItemType = sourceTransaction?.Data?.ItemType;
        }
        catch (NotSupportedException)
        {
            // Request metadata and the holding projection remain valid compatibility fallbacks.
        }

        return new BusinessObjectPayloadDescriptor(
            FirstNonEmpty(requestedObjectType, holding?.ObjectType, envelope?.ObjectType, "business-object"),
            FirstNonEmpty(requestedSchemaVersion, holding?.SchemaVersion, envelope?.SchemaVersion, "1.0"),
            FirstNonEmpty(requestedStateJson, holding?.StateJson, envelope?.StateJson, "{}"),
            holding?.ItemType ?? sourceItemType ?? TransactionDataItemType.BusinessObject);
    }

    private async Task EnsureReferencedBusinessObjectExistsAsync(
        LedgerAccountContext context,
        BusinessObjectId objectId,
        CancellationToken cancellationToken)
    {
        OutPoint outPoint = objectId.RequireOutPoint();
        try
        {
            LedgerTransactionSummary? sourceTransaction = await ledgerReadClient.GetTransactionAsync(
                context,
                outPoint.TxId,
                includeData: false,
                cancellationToken).ConfigureAwait(false);
            if (sourceTransaction is null
                || !sourceTransaction.Outputs.Any(output =>
                    output.OutputIndex == outPoint.OutputIndex && output.HasBusinessObject))
            {
                throw new InvalidOperationException($"Clone source BusinessObject does not exist: {objectId.Value}.");
            }
        }
        catch (NotSupportedException)
        {
            // Compatibility read clients may validate the reference when the transaction is submitted.
        }
    }

    private static BusinessObjectDataReference SourceReference(
        string relationship,
        BusinessObjectId objectId)
    {
        objectId.RequireOutPoint();
        return new BusinessObjectDataReference(relationship, objectId);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private sealed record BusinessObjectPayloadDescriptor(
        string ObjectType,
        string SchemaVersion,
        string StateJson,
        TransactionDataItemType ItemType);

    private static IReadOnlyList<LedgerSpendableOutput> SelectMainOutputs(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        Amount amount)
    {
        var selected = new List<LedgerSpendableOutput>();
        decimal total = 0m;

        foreach (LedgerSpendableOutput output in spendableOutputs.Where(x => x.BusinessObject is null && x.Token is null))
        {
            selected.Add(output);
            total += output.MainAmount.Value;
            if (total >= amount.Value)
            {
                return selected;
            }
        }

        throw new InvalidOperationException("Account does not have enough spendable non-BusinessObject main amount outputs.");
    }

    private static IReadOnlyList<LedgerSpendableOutput> SelectCreateInputs(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        OutPoint? sourceOutPoint,
        Amount mainAmount,
        TokenTransfer? token)
    {
        if (token is null)
        {
            if (sourceOutPoint is not null)
            {
                return new[]
                {
                    SelectExplicitCreateSource(spendableOutputs, sourceOutPoint, mainAmount, token)
                };
            }

            return SelectMainOutputs(spendableOutputs, mainAmount);
        }

        LedgerSpendableOutput tokenSource = sourceOutPoint is null
            ? SelectTokenSourceOutput(spendableOutputs, token.TokenId, token.Amount, Amount.Zero)
            : SelectExplicitCreateSource(spendableOutputs, sourceOutPoint, mainAmount, token);

        return SelectCreateInputsFromTokenSource(spendableOutputs, tokenSource, mainAmount);
    }

    private static TokenTransfer ResolveBusinessObjectCarrierToken(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        OutPoint? sourceOutPoint,
        TokenTransfer? requestedToken)
    {
        if (requestedToken is not null)
        {
            EnsureBusinessObjectCarrierToken(requestedToken);
            return requestedToken;
        }

        LedgerSpendableOutput source = sourceOutPoint is null
            ? SelectDefaultBusinessObjectCarrierSource(spendableOutputs)
            : SelectExplicitBusinessObjectCarrierSource(spendableOutputs, sourceOutPoint);
        return new TokenTransfer(source.Token!.TokenId, Amount.FromInt64(1));
    }

    private static LedgerSpendableOutput SelectDefaultBusinessObjectCarrierSource(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs)
        => spendableOutputs
            .Where(output => output.BusinessObject is null
                && output.Token is not null
                && output.Token.Amount.Value > 1m)
            .OrderBy(output => output.CreatedAt)
            .ThenBy(output => output.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(output => output.OutPoint.OutputIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("BusinessObject creation requires a reusable non-BusinessObject token source UTXO with amount greater than 1.");

    private static LedgerSpendableOutput SelectExplicitBusinessObjectCarrierSource(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        OutPoint sourceOutPoint)
    {
        LedgerSpendableOutput source = spendableOutputs.FirstOrDefault(output => output.OutPoint == sourceOutPoint)
            ?? throw new InvalidOperationException($"Source UTXO {sourceOutPoint} is not spendable by this account.");

        if (source.BusinessObject is not null || source.Token is null || source.Token.Amount.Value <= 1m)
        {
            throw new InvalidOperationException("BusinessObject creation requires a reusable non-BusinessObject token source UTXO with amount greater than 1.");
        }

        return source;
    }

    private static void EnsureBusinessObjectCarrierToken(TokenTransfer token)
    {
        if (token.Amount.Value != 1m)
        {
            throw new InvalidOperationException("Normal BusinessObject outputs must carry exactly 1 token.");
        }
    }

    private static void EnsureBusinessObjectCarrier(LedgerSpendableOutput output)
    {
        if (output.Token is null)
        {
            throw new InvalidOperationException($"BusinessObject output {output.OutPoint} is missing its token carrier.");
        }

        EnsureBusinessObjectCarrierToken(output.Token);
    }

    private static LedgerSpendableOutput SelectExplicitCreateSource(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        OutPoint sourceOutPoint,
        Amount mainAmount,
        TokenTransfer? token)
    {
        LedgerSpendableOutput source = spendableOutputs.FirstOrDefault(output => output.OutPoint == sourceOutPoint)
            ?? throw new InvalidOperationException($"Source UTXO {sourceOutPoint} is not spendable by this account.");

        if (source.BusinessObject is not null)
        {
            throw new InvalidOperationException("BusinessObject creation cannot use an existing BusinessObject output as its source.");
        }

        if (token is null)
        {
            if (source.MainAmount.Value < mainAmount.Value)
            {
                throw new InvalidOperationException("BusinessObject create main amount cannot exceed the selected source UTXO amount.");
            }

            if (source.Token is not null)
            {
                throw new InvalidOperationException("A token source UTXO can only be used when the created BusinessObject requests the same token.");
            }

            return source;
        }

        if (source.Token?.TokenId != token.TokenId)
        {
            throw new InvalidOperationException($"Source UTXO {sourceOutPoint} does not contain token {token.TokenId.Value}.");
        }

        if (source.Token.Amount.Value <= token.Amount.Value)
        {
            throw new InvalidOperationException(
                $"BusinessObject creation needs a non-unique token source UTXO for {token.TokenId.Value} with amount larger than {token.Amount}.");
        }

        return source;
    }

    private static IReadOnlyList<LedgerSpendableOutput> SelectCreateInputsFromTokenSource(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        LedgerSpendableOutput tokenSource,
        Amount mainAmount)
    {
        var selected = new List<LedgerSpendableOutput> { tokenSource };
        decimal coveredMain = tokenSource.MainAmount.Value;
        if (coveredMain >= mainAmount.Value)
        {
            return selected;
        }

        Amount remainingMain = new(mainAmount.Value - coveredMain);
        selected.AddRange(SelectMainOutputs(
            spendableOutputs
                .Where(output => output.OutPoint != tokenSource.OutPoint)
                .ToArray(),
            remainingMain));

        return selected;
    }

    private static LedgerSpendableOutput SelectTokenSourceOutput(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        TokenId tokenId,
        Amount amount,
        Amount mainAmount)
    {
        EnsurePositive(amount, nameof(amount));

        return spendableOutputs
            .Where(output => output.BusinessObject is null
                && output.Token?.TokenId == tokenId
                && output.Token.Amount.Value > amount.Value
                && output.MainAmount.Value >= mainAmount.Value)
            .OrderBy(output => output.CreatedAt)
            .ThenBy(output => output.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(output => output.OutPoint.OutputIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Account does not have a non-unique token source UTXO for {tokenId.Value} with amount larger than {amount}.");
    }

    private static LedgerSpendableOutput? SelectTokenMergeOutput(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        TokenId tokenId)
        => spendableOutputs
            .Where(output => output.BusinessObject is null && output.Token?.TokenId == tokenId)
            .OrderByDescending(output => output.Token!.Amount.Value > 1m)
            .ThenBy(output => output.CreatedAt)
            .ThenBy(output => output.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(output => output.OutPoint.OutputIndex)
            .FirstOrDefault();

    private static IReadOnlyList<LedgerSpendableOutput> SelectTokenOutputs(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        TokenId tokenId,
        Amount amount)
    {
        var selected = new List<LedgerSpendableOutput>();
        decimal total = 0m;

        foreach (LedgerSpendableOutput output in spendableOutputs.Where(x => x.BusinessObject is null && x.Token?.TokenId == tokenId))
        {
            selected.Add(output);
            total += output.Token!.Amount.Value;
            if (total >= amount.Value)
            {
                return selected;
            }
        }

        throw new InvalidOperationException($"Account does not have enough spendable token outputs for {tokenId.Value}.");
    }

    private static IReadOnlyList<LedgerSpendableOutput> SelectTokenTransferInputs(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        TokenId tokenId,
        Amount tokenAmount,
        Amount mainAmount)
    {
        var selected = SelectTokenOutputs(spendableOutputs, tokenId, tokenAmount).ToList();
        decimal selectedMain = selected.Sum(output => output.MainAmount.Value);
        if (selectedMain >= mainAmount.Value)
        {
            return selected;
        }

        Amount remainingMain = new(mainAmount.Value - selectedMain);
        selected.AddRange(SelectMainOutputs(
            spendableOutputs
                .Where(output => selected.All(existing => existing.OutPoint != output.OutPoint))
                .ToArray(),
            remainingMain));
        return selected;
    }

    private async Task<IReadOnlyList<LedgerSpendableOutput>> SelectExplicitPlainMainUtxosAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        IReadOnlyList<OutPoint> sourceOutPoints,
        int minimumCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceOutPoints);
        if (sourceOutPoints.Count < minimumCount)
        {
            throw new InvalidOperationException($"At least {minimumCount} selected UTXO(s) are required.");
        }

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        var byOutPoint = spendableOutputs.ToDictionary(output => output.OutPoint);
        var seen = new HashSet<OutPoint>();
        var selected = new List<LedgerSpendableOutput>(sourceOutPoints.Count);

        foreach (OutPoint sourceOutPoint in sourceOutPoints)
        {
            if (!seen.Add(sourceOutPoint))
            {
                throw new InvalidOperationException($"Duplicate selected UTXO: {sourceOutPoint}.");
            }

            if (!byOutPoint.TryGetValue(sourceOutPoint, out LedgerSpendableOutput? output))
            {
                throw new InvalidOperationException($"Selected UTXO {sourceOutPoint} is not spendable by this account.");
            }

            if (output.BusinessObject is not null || output.Token is not null)
            {
                throw new InvalidOperationException($"Selected UTXO {sourceOutPoint} is not a plain main-currency output.");
            }

            if (output.MainAmount.Value <= 0m)
            {
                throw new InvalidOperationException($"Selected UTXO {sourceOutPoint} must have a positive main amount.");
            }

            selected.Add(output);
        }

        return selected;
    }

    private static LedgerSpendableOutput SelectPermissionIssueSource(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        IReadOnlyList<LedgerPermissionSource> permissionSources,
        PermissionGrant grant,
        Amount amount,
        OutPoint? sourceOutPoint)
    {
        bool MatchesRequestedSource(LedgerSpendableOutput output)
            => sourceOutPoint is null || output.OutPoint == sourceOutPoint;

        LedgerSpendableOutput? treasurySource = spendableOutputs
            .Where(output => MatchesRequestedSource(output)
                && output.BusinessObject is null
                && PermissionTokenAuthority.IsPermissionToken(output.Token)
                && output.Token!.Amount.Value >= amount.Value)
            .OrderBy(output => output.CreatedAt)
            .ThenBy(output => output.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(output => output.OutPoint.OutputIndex)
            .FirstOrDefault();
        if (treasurySource is not null)
        {
            return treasurySource;
        }

        LedgerPermissionSource? delegatedSource = permissionSources
            .Where(source => MatchesRequestedSource(source.Output)
                && source.Amount.Value > amount.Value
                && CanDelegate(source.Grant, grant))
            .OrderBy(source => source.Grant.Namespace.Length)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .FirstOrDefault();

        return delegatedSource?.Output
            ?? throw new InvalidOperationException(
                $"Account does not have a permission source that can issue {amount} permission token(s) for namespace {grant.Namespace}.");
    }

    private static bool CanDelegate(PermissionGrant source, PermissionGrant delegated)
        => string.Equals(source.Action, delegated.Action, StringComparison.Ordinal)
            && PermissionTokenAuthority.IsTokenInNamespace(new TokenId(delegated.Namespace), source.Namespace)
            && (!delegated.Repeatable || source.Repeatable);

    private static void AddSelectedChangeOutput(
        List<TransactionOutput> outputs,
        IReadOnlyList<LedgerSpendableOutput> selected,
        Address changeAddress,
        Amount usedMain,
        TokenTransfer? usedToken)
    {
        decimal mainInput = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        decimal mainChange = mainInput - usedMain.Value;
        if (mainChange < 0m)
        {
            throw new InvalidOperationException("Selected inputs do not cover the requested main amount.");
        }

        TokenTransfer? tokenChange = null;
        if (usedToken is not null)
        {
            decimal tokenInput = selected
                .Where(output => output.Token?.TokenId == usedToken.TokenId)
                .Aggregate(0m, (sum, output) => sum + output.Token!.Amount.Value);
            decimal remainingToken = tokenInput - usedToken.Amount.Value;
            if (remainingToken < 0m)
            {
                throw new InvalidOperationException($"Selected inputs do not cover token amount for {usedToken.TokenId.Value}.");
            }

            if (remainingToken > 0m)
            {
                tokenChange = new TokenTransfer(usedToken.TokenId, new Amount(remainingToken));
            }
        }

        if (tokenChange is not null)
        {
            outputs.Add(new TransactionOutput(outputs.Count, changeAddress, Amount.Zero, tokenChange));
        }

        AddMainOutputIfPositive(outputs, changeAddress, new Amount(mainChange));
    }

    private static void AddTokenOutputIfPositive(
        List<TransactionOutput> outputs,
        Address address,
        TokenId tokenId,
        Amount amount)
    {
        if (amount.Value <= 0m)
        {
            return;
        }

        outputs.Add(new TransactionOutput(outputs.Count, address, Amount.Zero, new TokenTransfer(tokenId, amount)));
    }

    private static void AddMainOutputIfPositive(
        List<TransactionOutput> outputs,
        Address address,
        Amount amount)
    {
        if (amount.Value <= 0m)
        {
            return;
        }

        outputs.Add(new TransactionOutput(outputs.Count, address, amount));
    }

    private static void EnsurePositive(Amount amount, string fieldName)
    {
        if (amount.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(fieldName, "Amount must be positive.");
        }
    }

    private LedgerTransaction CreateSingleInputTransaction(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        LedgerSpendableOutput selected,
        IReadOnlyList<TransactionOutput> outputs,
        TransactionData? data)
        => new(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, selected.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            outputs,
            data);

    private LedgerTransaction CreateMultiInputTransaction(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        IReadOnlyList<LedgerSpendableOutput> selected,
        IReadOnlyList<TransactionOutput> outputs,
        TransactionKind kind,
        TransactionData? data)
        => new(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            kind,
            SigningMode.Independent,
            timeProvider.UtcNow,
            selected.Select((output, index) => new TransactionInput(index, output.OutPoint, keySource.Address, SignatureBundle.Empty)).ToArray(),
            outputs,
            data);

}
