using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed class BusinessObjectTransactionBuilder(
    ILedgerReadClient ledgerReadClient,
    ITransactionHasher transactionHasher,
    ISignaturePayloadBuilder signaturePayloadBuilder,
    IAccountInputSigner inputSigner,
    ITimeProvider timeProvider)
{
    public async Task<LedgerTransaction> BuildCreateAsync(
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

        IReadOnlyList<LedgerSpendableOutput> selected = SelectCreateInputs(
            spendableOutputs,
            request.SourceOutPoint,
            mainAmount,
            request.Token);

        decimal selectedMain = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        if (selectedMain < mainAmount.Value)
        {
            throw new InvalidOperationException("BusinessObject create main amount cannot exceed the selected input amount.");
        }

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.OwnerAddress ?? keySource.Address,
                mainAmount,
                request.Token,
                new BusinessObjectRef(request.ObjectId, 1, BusinessObjectOperation.Create, data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
        };

        AddSelectedChangeOutput(outputs, selected, keySource.Address, mainAmount, request.Token);

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.BusinessObject, data);
        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildTransferAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        TransferBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        request.MainAmount?.EnsureNonNegative(nameof(request.MainAmount));

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);

        LedgerSpendableOutput selected = spendableOutputs.FirstOrDefault(x => x.BusinessObject?.ObjectId == request.ObjectId)
            ?? throw new InvalidOperationException($"BusinessObject is not owned by {keySource.Address.Value}: {request.ObjectId.Value}.");

        Amount amountToReceiver = request.MainAmount ?? selected.MainAmount;
        if (amountToReceiver.Value > selected.MainAmount.Value)
        {
            throw new InvalidOperationException("Transfer amount cannot exceed the selected BusinessObject output amount.");
        }

        string stateJson = request.StateJson ?? "{}";
        HashHex dataHash = request.StateJson is null && selected.BusinessObject is not null
            ? selected.BusinessObject.DataHash
            : BusinessObjectPayload.HashJson(stateJson);
        TransactionData? data = request.StateJson is null && request.ExternalRef is null
            ? null
            : BusinessObjectPayload.CreateData(stateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress,
                amountToReceiver,
                selected.Token,
                new BusinessObjectRef(
                    request.ObjectId,
                    selected.BusinessObject!.Version + 1,
                    BusinessObjectOperation.Transfer,
                    dataHash))
        };

        Amount change = new(selected.MainAmount.Value - amountToReceiver.Value);
        if (change.Value > 0m)
        {
            outputs.Add(new TransactionOutput(1, keySource.Address, change));
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
                new TransactionInput(0, selected.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            outputs,
            data);

        TxId txId = transactionHasher.CalculateTxId(unsigned);
        LedgerTransaction withTxId = unsigned with { TxId = txId };
        TransactionInput signedInput = await SignInputAsync(withTxId, withTxId.Inputs[0], keySource, cancellationToken).ConfigureAwait(false);
        return withTxId with { Inputs = new[] { signedInput } };
    }

    public async Task<LedgerTransaction> BuildPaymentAsync(
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
        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildTokenTransferAsync(
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

        IReadOnlyList<LedgerSpendableOutput> selected = SelectTokenOutputs(spendableOutputs, request.TokenId, request.TokenAmount);
        decimal mainInput = selected.Aggregate(0m, (sum, output) => sum + output.MainAmount.Value);
        if (mainToReceiver.Value > mainInput)
        {
            throw new InvalidOperationException("Token transfer main amount cannot exceed the selected input amount.");
        }

        decimal tokenInput = selected.Aggregate(
            0m,
            (sum, output) => sum + output.Token!.Amount.Value);
        decimal tokenChange = tokenInput - request.TokenAmount.Value;
        decimal mainChange = mainInput - mainToReceiver.Value;

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress,
                mainToReceiver,
                new TokenTransfer(request.TokenId, request.TokenAmount))
        };

        if (mainChange > 0m || tokenChange > 0m)
        {
            outputs.Add(new TransactionOutput(
                1,
                keySource.Address,
                new Amount(mainChange),
                tokenChange > 0m
                    ? new TokenTransfer(request.TokenId, new Amount(tokenChange))
                    : null));
        }

        LedgerTransaction unsigned = CreateMultiInputTransaction(context, keySource, selected, outputs, TransactionKind.Transfer, data: null);
        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildTokenMintAsync(
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

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(new LedgerAddressQuery(context, keySource.Address), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .GetTransactionsAsync(new LedgerAddressQuery(context, keySource.Address), includeData: true, cancellationToken)
            .ConfigureAwait(false);

        LedgerPermissionSource permission = PermissionSourceResolver.SelectMintPermissionSource(
            PermissionSourceResolver.Resolve(spendableOutputs, transactions),
            request.TokenId,
            request.PermissionSourceOutPoint);
        BusinessObjectRef permissionRef = permission.Output.BusinessObject
            ?? throw new InvalidOperationException("Permission source output is missing its BusinessObject reference.");

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                request.ReceiverAddress ?? keySource.Address,
                mainAmount,
                new TokenTransfer(request.TokenId, request.TokenAmount))
        };

        if (permission.Grant.Repeatable)
        {
            outputs.Add(new TransactionOutput(
                1,
                keySource.Address,
                permission.Output.MainAmount,
                permission.Output.Token,
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
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
            Data: null);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildPermissionIssueAsync(
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
                    PermissionTokenAuthority.CreatePermissionObjectId(grant.Action, grant.Namespace),
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
                1,
                keySource.Address,
                source.MainAmount,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change)),
                changeBusinessObject));
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

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildCreateRuleTokenAsync(
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
                    RuleTokenAuthority.CreateRuleObjectId(rule.RuleId),
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
                permission.Output.MainAmount,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(remainingPermission)),
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
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

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildUpdateAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        UpdateBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateData(request.StateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            selected,
            new[]
            {
                new TransactionOutput(
                    0,
                    keySource.Address,
                    selected.MainAmount,
                    selected.Token,
                    new BusinessObjectRef(request.ObjectId, selected.BusinessObject!.Version + 1, BusinessObjectOperation.Update, data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
            },
            data);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildCloseAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloseBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateData(request.StateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            selected,
            new[]
            {
                new TransactionOutput(0, keySource.Address, selected.MainAmount, selected.Token)
            },
            data);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildCloneAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CloneBusinessObjectRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceObjectId == request.NewObjectId)
        {
            throw new InvalidOperationException("Clone requires a new BusinessObject id.");
        }

        LedgerSpendableOutput source = await GetOwnedBusinessObjectAsync(context, keySource, request.SourceObjectId, cancellationToken).ConfigureAwait(false);
        TransactionData data = BusinessObjectPayload.CreateData(new CreateBusinessObjectRequest(
            request.NewObjectId,
            request.ObjectType,
            request.StateJson,
            request.SchemaVersion,
            request.OwnerAddress,
            Amount.Zero,
            null,
            TransactionDataItemType.BusinessObject));
        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            source,
            new[]
            {
                new TransactionOutput(
                    0,
                    keySource.Address,
                    source.MainAmount,
                    source.Token,
                    source.BusinessObject! with
                    {
                        Version = source.BusinessObject.Version + 1,
                        Operation = BusinessObjectOperation.Update
                    }),
                new TransactionOutput(
                    1,
                    request.OwnerAddress ?? keySource.Address,
                    Amount.Zero,
                    null,
                    new BusinessObjectRef(request.NewObjectId, 1, BusinessObjectOperation.Create, data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
            },
            data);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildMergeAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        MergeBusinessObjectsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FirstObjectId == request.SecondObjectId)
        {
            throw new InvalidOperationException("Merge requires two distinct BusinessObjects.");
        }

        LedgerSpendableOutput first = await GetOwnedBusinessObjectAsync(context, keySource, request.FirstObjectId, cancellationToken).ConfigureAwait(false);
        LedgerSpendableOutput second = await GetOwnedBusinessObjectAsync(context, keySource, request.SecondObjectId, cancellationToken).ConfigureAwait(false);

        if (first.Token is not null || second.Token is not null)
        {
            throw new NotSupportedException("Merging token-bearing BusinessObject outputs needs explicit token merge rules.");
        }

        TransactionData data = BusinessObjectPayload.CreateData(request.StateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var unsigned = new LedgerTransaction(
            context.LedgerId,
            context.ScenarioId,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, first.OutPoint, keySource.Address, SignatureBundle.Empty),
                new TransactionInput(1, second.OutPoint, keySource.Address, SignatureBundle.Empty)
            },
            new[]
            {
                new TransactionOutput(
                    0,
                    keySource.Address,
                    new Amount(first.MainAmount.Value + second.MainAmount.Value),
                    null,
                    new BusinessObjectRef(request.TargetObjectId, 1, BusinessObjectOperation.Create, data.DataHash ?? BusinessObjectPayload.HashJson(request.StateJson)))
            },
            data);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildSplitPaymentAsync(
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

        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        decimal paymentTotal = request.Payments.Aggregate(0m, (sum, payment) => sum + payment.Amount.Value);
        if (paymentTotal > selected.MainAmount.Value)
        {
            throw new InvalidOperationException("Split payment total cannot exceed the selected BusinessObject output amount.");
        }

        string stateJson = request.StateJson ?? "{}";
        HashHex dataHash = request.StateJson is null && selected.BusinessObject is not null
            ? selected.BusinessObject.DataHash
            : BusinessObjectPayload.HashJson(stateJson);
        TransactionData? data = request.StateJson is null && request.ExternalRef is null
            ? null
            : BusinessObjectPayload.CreateData(stateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var outputs = new List<TransactionOutput>();

        for (var i = 0; i < request.Payments.Count; i++)
        {
            BusinessObjectPaymentOutput payment = request.Payments[i];
            outputs.Add(new TransactionOutput(
                i,
                payment.ReceiverAddress,
                payment.Amount,
                i == 0 ? selected.Token : null,
                i == 0
                    ? new BusinessObjectRef(request.ObjectId, selected.BusinessObject!.Version + 1, BusinessObjectOperation.Transfer, dataHash)
                    : null));
        }

        decimal change = selected.MainAmount.Value - paymentTotal;
        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(outputs.Count, keySource.Address, new Amount(change)));
        }

        var unsigned = CreateSingleInputTransaction(context, keySource, selected, outputs, data);
        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransaction> BuildSetPriceAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        SetBusinessObjectPriceRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePositive(request.PriceAmount, nameof(request.PriceAmount));

        LedgerSpendableOutput selected = await GetOwnedBusinessObjectAsync(context, keySource, request.ObjectId, cancellationToken).ConfigureAwait(false);
        string stateJson = string.IsNullOrWhiteSpace(request.StateJson) ? "{}" : request.StateJson;
        TransactionData data = BusinessObjectPayload.CreateData(stateJson, TransactionDataItemType.BusinessObject, externalRef: request.ExternalRef);
        var unsigned = CreateSingleInputTransaction(
            context,
            keySource,
            selected,
            new[]
            {
                new TransactionOutput(
                    0,
                    keySource.Address,
                    selected.MainAmount,
                    selected.Token,
                    new BusinessObjectRef(
                        request.ObjectId,
                        selected.BusinessObject!.Version + 1,
                        BusinessObjectOperation.Update,
                        data.DataHash ?? BusinessObjectPayload.HashJson(stateJson)))
            },
            data);

        return await SignInputsAsync(unsigned, keySource, cancellationToken).ConfigureAwait(false);
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

        return spendableOutputs.FirstOrDefault(x => x.BusinessObject?.ObjectId == objectId)
            ?? throw new InvalidOperationException($"BusinessObject is not owned by {keySource.Address.Value}: {objectId.Value}.");
    }

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
        if (sourceOutPoint is not null)
        {
            return new[]
            {
                SelectExplicitCreateSource(spendableOutputs, sourceOutPoint, mainAmount, token)
            };
        }

        if (token is null)
        {
            return SelectMainOutputs(spendableOutputs, mainAmount);
        }

        return new[]
        {
            SelectTokenSourceOutput(spendableOutputs, token.TokenId, token.Amount, mainAmount)
        };
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

        if (source.MainAmount.Value < mainAmount.Value)
        {
            throw new InvalidOperationException("BusinessObject create main amount cannot exceed the selected source UTXO amount.");
        }

        if (token is null)
        {
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

        if (mainChange > 0m || tokenChange is not null)
        {
            outputs.Add(new TransactionOutput(outputs.Count, changeAddress, new Amount(mainChange), tokenChange));
        }
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

    private async Task<LedgerTransaction> SignInputsAsync(
        LedgerTransaction unsigned,
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        TxId txId = transactionHasher.CalculateTxId(unsigned);
        LedgerTransaction withTxId = unsigned with { TxId = txId };
        var signedInputs = new List<TransactionInput>(withTxId.Inputs.Count);

        foreach (TransactionInput input in withTxId.Inputs.OrderBy(x => x.InputIndex))
        {
            signedInputs.Add(await SignInputAsync(withTxId, input, keySource, cancellationToken).ConfigureAwait(false));
        }

        return withTxId with { Inputs = signedInputs };
    }

    private async Task<TransactionInput> SignInputAsync(
        LedgerTransaction transaction,
        TransactionInput input,
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        HashHex payloadHash = transaction.SigningMode == SigningMode.Ordered
            ? signaturePayloadBuilder.BuildOrderedInputPayloadHash(transaction, input)
            : signaturePayloadBuilder.BuildIndependentInputPayloadHash(transaction, input);
        SignatureEnvelope signature = await inputSigner.SignAsync(keySource, payloadHash, cancellationToken).ConfigureAwait(false);

        return input with
        {
            SignaturePayloadHash = payloadHash,
            SignatureBundle = new SignatureBundle(new[] { signature })
        };
    }
}
