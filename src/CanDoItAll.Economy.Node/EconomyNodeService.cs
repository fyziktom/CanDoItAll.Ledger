using System.Globalization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using CanDoItAll.Economy.Sdk;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Node;

public sealed class EconomyNodeService(
    IDbContextFactory<EconomyDbContext> dbContextFactory,
    LedgerExecutor ledgerExecutor,
    ITimeProvider timeProvider,
    ITransactionHasher? transactionHasher = null,
    ISignaturePayloadBuilder? signaturePayloadBuilder = null,
    IAccountInputSigner? accountInputSigner = null,
    EconomyNodeHostOptions? hostOptions = null,
    EconomyLedgerReadModelService? readModelService = null,
    IEconomySchemaMigrator? schemaMigrator = null,
    ITokenDefinitionHasher? providedTokenDefinitionHasher = null,
    IBusinessObjectLineageKeyFactory? providedLineageKeyFactory = null)
{
    private static readonly LedgerId DefaultLedgerId = new("default");
    private readonly EconomyNodeHostOptions options = hostOptions ?? new EconomyNodeHostOptions();
    private readonly ITransactionHasher txHasher = transactionHasher ?? new TransactionHasher(new DefaultTransactionCanonicalizer());
    private readonly ISignaturePayloadBuilder inputPayloadBuilder = signaturePayloadBuilder ?? new SignaturePayloadBuilder(transactionHasher ?? new TransactionHasher(new DefaultTransactionCanonicalizer()));
    private readonly IAccountInputSigner inputSigner = accountInputSigner ?? new AccountInputSigner(SignatureProviderRegistry.CreateDefault());
    private readonly EconomyLedgerReadModelService readModels = readModelService ?? new EconomyLedgerReadModelService(dbContextFactory);
    private readonly IEconomySchemaMigrator? configuredSchemaMigrator = schemaMigrator;
    private readonly ITokenDefinitionHasher tokenDefinitionHasher = providedTokenDefinitionHasher
        ?? new TokenDefinitionHasher(new DefaultTokenDefinitionCanonicalizer());
    private readonly IBusinessObjectLineageKeyFactory lineageKeyFactory = providedLineageKeyFactory
        ?? RandomBusinessObjectLineageKeyFactory.Instance;

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        if (!options.AllowLedgerReset)
        {
            throw new InvalidOperationException("Ledger reset is disabled by node configuration.");
        }

        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
        await ApplySchemaManagementAsync(db, cancellationToken).ConfigureAwait(false);

        if (options.BootstrapTreasuryAdmin)
        {
            await EconomyNodeBootstrapper
                .BootstrapTreasuryAdminAsync(db, ledgerExecutor, timeProvider, options, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<NodeBootstrapInfo> GetBootstrapInfoAsync(CancellationToken cancellationToken)
    {
        EconomyNodeAdminAccount adminAccount = options.ValidateAndGetTreasuryAdminAccount();
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        bool genesisCreated = await db.LedgerStates
            .AsNoTracking()
            .AnyAsync(x => x.LedgerId == DefaultLedgerId.Value && x.ScenarioId == null, cancellationToken)
            .ConfigureAwait(false);

        return new NodeBootstrapInfo(
            DefaultLedgerId.Value,
            options.BootstrapTreasuryAdmin,
            genesisCreated,
            adminAccount.Address.Value,
            adminAccount.KeyId,
            adminAccount.Algorithm.ToString(),
            adminAccount.PublicKeyHash.Value,
            Convert.ToBase64String(adminAccount.PublicKeyMaterial),
            Convert.ToBase64String(adminAccount.PublicKeyMaterial),
            options.TreasuryAdminMainAmount,
            options.TreasuryAdminMainAmount.ToString(CultureInfo.InvariantCulture),
            options.EnforceTokenCreationPolicy,
            options.NormalizedApiVersion,
            options.VersionedApiBasePath,
            options.LegacyApiBasePath,
            options.SecurityProfile.ToString(),
            options.ApiSecurityMode.ToString(),
            options.ReadSecurityMode.ToString(),
            options.UiAuthMode.ToString(),
            options.RequireTwoFactorAuthentication,
            options.AuthPersistenceProvider.ToString(),
            options.JwtRequireActiveTokenRecord,
            options.UsesDevelopmentLedgerAdminSigner ? "Development" : "Configured",
            options.AllowLedgerReset,
            options.SchemaManagementMode.ToString(),
            options.RequiresTrustedAdminVerification);
    }

    public async Task<PermissionGrantReceipt> GrantPermissionAsync(
        PermissionGrantRequest request,
        CancellationToken cancellationToken)
    {
        string tokenNamespace = PermissionTokenAuthority.NormalizeNamespace(request.TokenNamespace);
        string action = PermissionTokenAuthority.NormalizeAction(
            string.IsNullOrWhiteSpace(request.Action)
                ? PermissionTokenAuthority.MintNewTokenTypeAction
                : request.Action);
        Amount amount = ParseAmount(request.Amount ?? 1m, nameof(request.Amount));
        if (amount.Value <= 0m)
        {
            throw new InvalidOperationException("Permission amount must be positive.");
        }

        Address treasuryAddress = new(options.TreasuryAdminAddress.Trim());
        Address receiverAddress = new(request.Address.Trim());
        ScenarioId? scenarioId = ToScenarioId(request.ScenarioId);
        PermissionGrant grant = new(action, tokenNamespace, request.Repeatable ?? true);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);
        UnspentOutputRecord treasuryPermission = await GetTreasuryPermissionOutputAsync(scenarioId, cancellationToken).ConfigureAwait(false);
        decimal availablePermission = treasuryPermission.TokenAmount ?? 0m;
        if (availablePermission < amount.Value)
        {
            throw new InvalidOperationException("Treasury admin does not have enough permission token supply.");
        }

        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                receiverAddress,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, amount),
                new BusinessObjectRef(lineageKeyFactory.Create(), 1, BusinessObjectOperation.Create, data.DataHash!.Value))
        };
        decimal change = availablePermission - amount.Value;
        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(
                1,
                treasuryAddress,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change))));
        }

        var transaction = new LedgerTransaction(
            DefaultLedgerId,
            scenarioId,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, new OutPoint(new TxId(treasuryPermission.TxId), treasuryPermission.OutputIndex), treasuryAddress, SignatureBundle.Empty)
            },
            outputs,
            data);

        LedgerSubmitResult result = await ledgerExecutor.SubmitAsync(
            await SignTreasuryInputsAsync(transaction, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        TransactionReceipt receipt = ToReceipt(result);
        BusinessObjectId permissionObjectId = BusinessObjectId.FromOutPoint(
            new OutPoint(new TxId(receipt.TxId), OutputIndex: 0));

        return new PermissionGrantReceipt(
            permissionObjectId.Value,
            grant.Action,
            grant.Namespace,
            grant.Repeatable,
            amount.Value,
            FormatDecimal(amount.Value),
            receiverAddress.Value,
            receipt.TxId,
            receipt.LedgerSequence);
    }

    public async Task<TokenAuthorityGrantReceipt> GrantTokenCreationAuthorityAsync(
        TokenAuthorityGrantRequest request,
        CancellationToken cancellationToken)
    {
        PermissionGrantReceipt receipt = await GrantPermissionAsync(
            new PermissionGrantRequest(
                request.TokenNamespace,
                request.Address,
                Amount: 1m,
                Repeatable: true,
                Action: PermissionTokenAuthority.MintNewTokenTypeAction,
                request.ScenarioId),
            cancellationToken).ConfigureAwait(false);
        return new TokenAuthorityGrantReceipt(
            receipt.PermissionObjectId,
            receipt.TokenNamespace,
            receipt.Address,
            receipt.TxId,
            receipt.TxId,
            receipt.LedgerSequence);
    }

    public async Task<TransactionReceipt> MintAsync(MintTransactionRequest request, CancellationToken cancellationToken)
    {
        Amount mainAmount = ParseAmount(request.MainAmount, nameof(request.MainAmount));
        TokenTransfer? token = request.TokenDefinition is null
            ? CreateToken(request.TokenId, request.TokenAmount)
            : CreateVersionTwoToken(request.TokenDefinition, request.TokenId, request.TokenAmount);

        if (mainAmount.Value == 0m && token is null)
        {
            throw new InvalidOperationException("Mint requires a main amount or token amount.");
        }

        Address receiverAddress = new(request.Address);
        Address treasuryAddress = new(options.TreasuryAdminAddress.Trim());
        if (request.TokenDefinition is not null && request.TokenDefinition.Definition.Issuer != treasuryAddress)
        {
            throw new InvalidOperationException("Treasury helper token definition issuer must be the configured treasury address.");
        }
        if (token is not null && receiverAddress != treasuryAddress)
        {
            throw new InvalidOperationException("Grant-holder token minting must be submitted as a signed ledger transaction from the account that owns the token authority BO.");
        }

        ScenarioId? scenarioId = ToScenarioId(request.ScenarioId);
        UnspentOutputRecord? treasuryAuthority = mainAmount.Value > 0m || token is null
            ? await GetTreasuryAuthorityOutputAsync(scenarioId, cancellationToken).ConfigureAwait(false)
            : null;
        UnspentOutputRecord? tokenMintPermission = token is null
            ? null
            : await GetOrCreateTreasuryTokenMintPermissionOutputAsync(
                scenarioId,
                token.TokenId,
                request.TokenDefinition?.Definition.Namespace,
                cancellationToken).ConfigureAwait(false);
        TransactionData? data = CreateData(request.DataItemType, request.DataJson, request.ExternalRef);
        var outputs = new List<TransactionOutput>();
        AddRequestedAssetOutputs(
            outputs,
            receiverAddress,
            mainAmount,
            token,
            CreateBusinessObjectRef(request.LegacyBusinessObjectCreationMarker, data));
        if (treasuryAuthority is not null)
        {
            AddPreservedAuthorityOutput(outputs, treasuryAuthority, treasuryAddress);
        }

        if (tokenMintPermission is not null)
        {
            AddReturnedPermissionOutput(outputs, tokenMintPermission, treasuryAddress);
        }

        var inputs = new List<TransactionInput>();
        if (treasuryAuthority is not null)
        {
            inputs.Add(new TransactionInput(
                inputs.Count,
                new OutPoint(new TxId(treasuryAuthority.TxId), treasuryAuthority.OutputIndex),
                treasuryAddress,
                SignatureBundle.Empty));
        }

        if (tokenMintPermission is not null)
        {
            inputs.Add(new TransactionInput(
                inputs.Count,
                new OutPoint(new TxId(tokenMintPermission.TxId), tokenMintPermission.OutputIndex),
                treasuryAddress,
                SignatureBundle.Empty));
        }

        var transaction = new LedgerTransaction(
            DefaultLedgerId,
            scenarioId,
            Version: request.TokenDefinition is null ? 1 : 2,
            TransactionKind.Mint,
            SigningMode.Independent,
            timeProvider.UtcNow,
            inputs,
            outputs,
            data,
            TokenDefinition: request.TokenDefinition);

        LedgerSubmitResult result = await ledgerExecutor.SubmitAsync(
            await SignTreasuryInputsAsync(transaction, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return ToReceipt(result);
    }

    public async Task<TransactionReceipt> TransferAsync(TransferTransactionRequest request, CancellationToken cancellationToken)
    {
        Amount mainAmount = ParseAmount(request.MainAmount, nameof(request.MainAmount));
        TokenTransfer? token = CreateToken(request.TokenId, request.TokenAmount);

        if (mainAmount.Value == 0m && token is null)
        {
            throw new InvalidOperationException("Transfer requires a main amount or token amount.");
        }

        Address fromAddress = new(request.FromAddress);
        Address toAddress = new(request.ToAddress);
        ScenarioId? scenarioId = ToScenarioId(request.ScenarioId);
        string? scenarioValue = scenarioId?.Value;
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IQueryable<UnspentOutputRecord> candidatesQuery = db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == fromAddress.Value
                && x.MainAmount >= mainAmount.Value);

        candidatesQuery = token is null
            ? candidatesQuery.Where(x => x.TokenId == null || x.TokenId == string.Empty)
            : candidatesQuery.Where(x => x.TokenId == token.TokenId.Value && (x.TokenAmount ?? 0m) >= token.Amount.Value);

        UnspentOutputRecord? selected = await candidatesQuery
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (selected is null)
        {
            throw new InvalidOperationException($"No UTXO for {fromAddress.Value} can cover the requested transfer.");
        }

        TransactionData? data = CreateData(request.DataItemType, request.DataJson, request.ExternalRef);
        var outputs = new List<TransactionOutput>();
        AddRequestedAssetOutputs(
            outputs,
            toAddress,
            mainAmount,
            token,
            CreateBusinessObjectRef(request.LegacyBusinessObjectCreationMarker, data));

        AddChangeOutput(outputs, selected, fromAddress.Value, mainAmount, token);

        var transaction = new LedgerTransaction(
            DefaultLedgerId,
            scenarioId,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[]
            {
                new TransactionInput(0, new OutPoint(new TxId(selected.TxId), selected.OutputIndex), fromAddress, SignatureBundle.Empty)
            },
            outputs,
            data);

        if (fromAddress != new Address(options.TreasuryAdminAddress.Trim()))
        {
            throw new InvalidOperationException("The node transfer helper can only sign treasury admin transfers. Account transfers must be submitted as signed ledger transactions.");
        }

        LedgerSubmitResult result = await ledgerExecutor.SubmitAsync(
            await SignTreasuryInputsAsync(transaction, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return ToReceipt(result);
    }

    public Task<LedgerSubmitResult> SubmitLedgerTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken)
        => ledgerExecutor.SubmitAsync(transaction, cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
        => readModels.GetSpendableOutputsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectHoldingsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
        => readModels.GetBusinessObjectHoldingsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetLedgerTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
        => readModels.GetLedgerTransactionsAsync(query, includeData, cancellationToken);

    public Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
        => readModels.GetAccountsAsync(cancellationToken);

    public Task<IReadOnlyList<TransactionSummary>> GetAddressTransactionsAsync(string address, CancellationToken cancellationToken)
        => readModels.GetAddressTransactionsAsync(address, cancellationToken);

    public Task<TransactionDetails?> GetTransactionAsync(
        string txId,
        bool includeData,
        CancellationToken cancellationToken)
        => readModels.GetTransactionAsync(txId, includeData, cancellationToken);

    private static TransactionReceipt ToReceipt(LedgerSubmitResult result)
    {
        if (!result.Accepted || result.TxId is null || result.LedgerSequence is null)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.ToString())));
        }

        return new TransactionReceipt(result.TxId.Value.Value, result.LedgerSequence.Value);
    }

    private async Task<LedgerTransaction> SignTreasuryInputsAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (transaction.Inputs.Count == 0)
        {
            return transaction;
        }

        EconomyNodeAdminAccount adminAccount = options.ValidateAndGetTreasuryAdminAccount();
        var keySource = new InMemoryAccountKeySource(
            adminAccount.Address,
            adminAccount.Algorithm,
            adminAccount.KeyId,
            adminAccount.PublicKeyHash,
            adminAccount.PublicKeyMaterial,
            adminAccount.PrivateKeyMaterial);

        LedgerTransaction withTxId = transaction with { TxId = txHasher.CalculateTxId(transaction) };
        var signedInputs = new List<TransactionInput>(withTxId.Inputs.Count);

        foreach (TransactionInput input in withTxId.Inputs.OrderBy(x => x.InputIndex))
        {
            if (input.Address != adminAccount.Address)
            {
                throw new InvalidOperationException("The node can only sign treasury admin inputs.");
            }

            HashHex payloadHash = withTxId.SigningMode == SigningMode.Ordered
                ? inputPayloadBuilder.BuildOrderedInputPayloadHash(withTxId, input)
                : inputPayloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
            SignatureEnvelope signature = await inputSigner
                .SignAsync(keySource, payloadHash, cancellationToken)
                .ConfigureAwait(false);

            signedInputs.Add(input with
            {
                SignaturePayloadHash = payloadHash,
                SignatureBundle = new SignatureBundle(new[] { signature })
            });
        }

        return withTxId with { Inputs = signedInputs };
    }

    private async Task<UnspentOutputRecord> GetTreasuryAuthorityOutputAsync(
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        string treasuryAddress = options.TreasuryAdminAddress.Trim();
        string? scenarioValue = scenarioId?.Value;
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        UnspentOutputRecord? output = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == treasuryAddress
                && (x.TokenId == null || x.TokenId == string.Empty)
                && (x.BusinessObjectId == null || x.BusinessObjectId == string.Empty))
            .OrderByDescending(x => x.MainAmount)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return output
            ?? throw new InvalidOperationException("Treasury admin does not have a spendable authority input.");
    }

    private async Task<UnspentOutputRecord> GetTreasuryPermissionOutputAsync(
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        string treasuryAddress = options.TreasuryAdminAddress.Trim();
        string? scenarioValue = scenarioId?.Value;
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        UnspentOutputRecord? output = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == treasuryAddress
                && x.TokenId == PermissionTokenAuthority.PermissionTokenId.Value
                && (x.BusinessObjectId == null || x.BusinessObjectId == string.Empty))
            .OrderByDescending(x => x.TokenAmount)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return output
            ?? throw new InvalidOperationException("Treasury admin does not have a spendable root permission token input.");
    }

    private async Task<UnspentOutputRecord> GetOrCreateTreasuryTokenMintPermissionOutputAsync(
        ScenarioId? scenarioId,
        TokenId tokenId,
        string? authorizationNamespace,
        CancellationToken cancellationToken)
    {
        UnspentOutputRecord? existing = await GetTreasuryTokenMintPermissionOutputAsync(
            scenarioId,
            tokenId,
            authorizationNamespace,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        string treasuryAddress = options.TreasuryAdminAddress.Trim();
        await GrantPermissionAsync(
            new PermissionGrantRequest(
                authorizationNamespace ?? tokenId.Value,
                treasuryAddress,
                Amount: 1m,
                Repeatable: true,
                Action: PermissionTokenAuthority.MintNewTokenTypeAction,
                ScenarioId: scenarioId?.Value),
            cancellationToken).ConfigureAwait(false);

        return await GetTreasuryTokenMintPermissionOutputAsync(
                scenarioId,
                tokenId,
                authorizationNamespace,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Treasury admin could not create a mint permission for token {tokenId.Value}.");
    }

    private async Task<UnspentOutputRecord?> GetTreasuryTokenMintPermissionOutputAsync(
        ScenarioId? scenarioId,
        TokenId tokenId,
        string? authorizationNamespace,
        CancellationToken cancellationToken)
    {
        string treasuryAddress = options.TreasuryAdminAddress.Trim();
        string? scenarioValue = scenarioId?.Value;
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<UnspentOutputRecord> candidates = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == treasuryAddress
                && x.TokenId == PermissionTokenAuthority.PermissionTokenId.Value
                && x.BusinessObjectId != null
                && x.BusinessObjectId != string.Empty)
            .OrderByDescending(x => x.TokenAmount)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] dataHashes = candidates
            .Select(candidate => candidate.BusinessObjectDataHash)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<LedgerTransactionRecord> permissionDataRows = dataHashes.Length == 0
            ? []
            : await db.Transactions
                .AsNoTracking()
                .Where(transaction => transaction.LedgerId == DefaultLedgerId.Value
                    && transaction.ScenarioId == scenarioValue
                    && transaction.DataHash != null
                    && dataHashes.Contains(transaction.DataHash))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        Dictionary<string, TransactionData> permissionDataByHash = permissionDataRows
            .Select(row => new { row.DataHash, Data = EconomyNodeRecordMapper.ToTransactionData(row) })
            .Where(item => !string.IsNullOrWhiteSpace(item.DataHash) && item.Data is not null)
            .GroupBy(item => item.DataHash!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Data!, StringComparer.Ordinal);

        TokenId requestedNamespace = authorizationNamespace is null
            ? tokenId
            : new TokenId(authorizationNamespace);
        return candidates.FirstOrDefault(candidate =>
            PermissionObjectAllowsToken(candidate, permissionDataByHash, requestedNamespace));
    }

    private static void AddPreservedAuthorityOutput(
        List<TransactionOutput> outputs,
        UnspentOutputRecord authority,
        Address treasuryAddress)
    {
        outputs.Add(new TransactionOutput(
            outputs.Count,
            treasuryAddress,
            EconomyNodeRecordMapper.ToTokenTransfer(authority) is null ? new Amount(authority.MainAmount) : Amount.Zero,
            EconomyNodeRecordMapper.ToTokenTransfer(authority),
            EconomyNodeRecordMapper.ToBusinessObjectRef(authority),
            string.IsNullOrWhiteSpace(authority.MetadataHash) ? null : new HashHex(authority.MetadataHash)));
        if (EconomyNodeRecordMapper.ToTokenTransfer(authority) is not null)
        {
            AddMainOutputIfPositive(outputs, treasuryAddress, new Amount(authority.MainAmount));
        }
    }

    private static void AddReturnedPermissionOutput(
        List<TransactionOutput> outputs,
        UnspentOutputRecord permission,
        Address treasuryAddress)
    {
        BusinessObjectRef permissionRef = EconomyNodeRecordMapper.ToBusinessObjectRef(permission)
            ?? throw new InvalidOperationException("Treasury mint permission output is missing its permission BusinessObject reference.");

        outputs.Add(new TransactionOutput(
            outputs.Count,
            treasuryAddress,
            Amount.Zero,
            EconomyNodeRecordMapper.ToTokenTransfer(permission),
            permissionRef with
            {
                Version = permissionRef.Version + 1,
                Operation = BusinessObjectOperation.Update
            },
            string.IsNullOrWhiteSpace(permission.MetadataHash) ? null : new HashHex(permission.MetadataHash)));
        AddMainOutputIfPositive(outputs, treasuryAddress, new Amount(permission.MainAmount));
    }

    private static TransactionData? CreateData(string? itemType, string? dataJson, string? externalRef)
    {
        if (string.IsNullOrWhiteSpace(itemType) && string.IsNullOrWhiteSpace(dataJson) && string.IsNullOrWhiteSpace(externalRef))
        {
            return null;
        }

        TransactionDataItemType parsedType = ParseItemType(itemType);
        string? normalizedJson = string.IsNullOrWhiteSpace(dataJson) ? null : dataJson.Trim();
        if (normalizedJson is not null && System.Text.Encoding.UTF8.GetByteCount(normalizedJson) > LedgerValidationLimits.MaxInlineJsonBytes)
        {
            throw new InvalidOperationException($"Inline JSON cannot exceed {LedgerValidationLimits.MaxInlineJsonBytes} bytes.");
        }

        string? normalizedExternalRef = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim();
        if (!LedgerValueValidation.IsSafeText(normalizedExternalRef, LedgerValidationLimits.MaxExternalRefLength, allowEmpty: true))
        {
            throw new InvalidOperationException("External reference is invalid or too long.");
        }

        HashHex? dataHash = normalizedJson is null ? null : Hex.Sha256Hex(Hex.Utf8(normalizedJson));

        return new TransactionData(
            normalizedJson is null ? null : "application/json",
            dataHash,
            normalizedJson,
            normalizedExternalRef,
            parsedType);
    }

    private BusinessObjectRef? CreateBusinessObjectRef(
        string? legacyBusinessObjectCreationMarker,
        TransactionData? data)
    {
        if (string.IsNullOrWhiteSpace(legacyBusinessObjectCreationMarker))
        {
            return null;
        }

        if (data?.DataHash is not { } dataHash)
        {
            throw new InvalidOperationException(
                "The legacy businessObjectId creation marker requires transaction data with a content hash.");
        }

        return new BusinessObjectRef(
            lineageKeyFactory.Create(),
            1,
            BusinessObjectOperation.Create,
            dataHash);
    }

    private static TokenTransfer? CreateToken(string? tokenId, decimal? amount)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        string normalizedTokenId = tokenId.Trim();
        if (!LedgerValueValidation.IsValidIdentifier(normalizedTokenId, LedgerValidationLimits.MaxTokenIdLength))
        {
            throw new InvalidOperationException("Token id contains unsupported characters, is empty, or is too long.");
        }

        Amount parsedAmount = ParseAmount(amount ?? 0m, nameof(amount));
        if (parsedAmount.Value <= 0m)
        {
            return null;
        }

        return new TokenTransfer(new TokenId(normalizedTokenId), parsedAmount);
    }

    private TokenTransfer CreateVersionTwoToken(
        TokenDefinitionEnvelope envelope,
        string? requestedTokenId,
        decimal? requestedAmount)
    {
        TokenId tokenId = tokenDefinitionHasher.CalculateTokenId(envelope.Definition);
        if (!string.IsNullOrWhiteSpace(requestedTokenId)
            && !string.Equals(requestedTokenId.Trim(), tokenId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Protocol-v2 token id must be the hash-derived id {tokenId.Value}.");
        }

        Amount amount = ParseAmount(requestedAmount ?? envelope.Definition.InitialSupply.Value, nameof(requestedAmount));
        if (amount != envelope.Definition.InitialSupply)
        {
            throw new InvalidOperationException("Protocol-v2 token amount must equal the immutable initial supply.");
        }

        return new TokenTransfer(tokenId, amount);
    }

    private static bool PermissionObjectAllowsToken(
        UnspentOutputRecord row,
        IReadOnlyDictionary<string, TransactionData> permissionDataByHash,
        TokenId tokenId)
    {
        BusinessObjectRef? businessObject = EconomyNodeRecordMapper.ToBusinessObjectRef(row);
        if (businessObject is null
            || string.IsNullOrWhiteSpace(row.BusinessObjectDataHash)
            || !permissionDataByHash.TryGetValue(row.BusinessObjectDataHash, out TransactionData? data)
            || !PermissionTokenAuthority.TryParseData(data, businessObject, out PermissionGrant grant)
            || !string.Equals(
                grant.Action,
                PermissionTokenAuthority.MintNewTokenTypeAction,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return PermissionTokenAuthority.IsTokenInNamespace(tokenId, grant.Namespace);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void AddChangeOutput(
        List<TransactionOutput> outputs,
        UnspentOutputRecord selected,
        string fromAddress,
        Amount requestedMain,
        TokenTransfer? requestedToken)
    {
        decimal availableMain = selected.MainAmount;
        decimal remainingMain = availableMain - requestedMain.Value;
        decimal remainingToken = 0m;

        if (!string.IsNullOrWhiteSpace(selected.TokenId))
        {
            decimal selectedToken = selected.TokenAmount ?? 0m;
            remainingToken = selectedToken - (requestedToken?.Amount.Value ?? 0m);
        }

        if (!string.IsNullOrWhiteSpace(selected.TokenId) && remainingToken > 0m)
        {
            outputs.Add(new TransactionOutput(
                outputs.Count,
                new Address(fromAddress),
                Amount.Zero,
                new TokenTransfer(new TokenId(selected.TokenId), new Amount(remainingToken))));
        }

        AddMainOutputIfPositive(outputs, new Address(fromAddress), new Amount(remainingMain));
    }

    private static void AddRequestedAssetOutputs(
        List<TransactionOutput> outputs,
        Address receiverAddress,
        Amount mainAmount,
        TokenTransfer? token,
        BusinessObjectRef? businessObject)
    {
        if (businessObject is not null)
        {
            if (token is null || token.Amount.Value != 1m)
            {
                throw new InvalidOperationException("BusinessObject outputs must carry exactly 1 token and 0 main amount.");
            }

            outputs.Add(new TransactionOutput(outputs.Count, receiverAddress, Amount.Zero, token, businessObject));
            AddMainOutputIfPositive(outputs, receiverAddress, mainAmount);
            return;
        }

        if (token is not null)
        {
            outputs.Add(new TransactionOutput(outputs.Count, receiverAddress, Amount.Zero, token));
        }

        AddMainOutputIfPositive(outputs, receiverAddress, mainAmount);
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

    private static Amount ParseAmount(decimal value, string name)
    {
        if (value < 0m)
        {
            throw new InvalidOperationException($"{name} must be a non-negative decimal value.");
        }

        return new Amount(value);
    }

    private static string FormatDecimal(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static ScenarioId? ToScenarioId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (!LedgerValueValidation.IsValidIdentifier(normalized, LedgerValidationLimits.MaxScenarioIdLength))
        {
            throw new InvalidOperationException("Scenario id contains unsupported characters, is empty, or is too long.");
        }

        return new ScenarioId(normalized);
    }

    private static TransactionDataItemType ParseItemType(string? value)
        => Enum.TryParse(value, ignoreCase: true, out TransactionDataItemType itemType)
            ? itemType
            : TransactionDataItemType.Generic;

    private async Task ApplySchemaManagementAsync(EconomyDbContext db, CancellationToken cancellationToken)
    {
        switch (options.SchemaManagementMode)
        {
            case EconomyNodeSchemaManagementMode.EnsureCreated:
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeSchemaManagementMode.Migrate:
                if (configuredSchemaMigrator is null)
                {
                    throw new InvalidOperationException("Schema migration mode requires an IEconomySchemaMigrator.");
                }

                await configuredSchemaMigrator.MigrateAsync(db, cancellationToken).ConfigureAwait(false);
                return;

            case EconomyNodeSchemaManagementMode.None:
                return;

            default:
                throw new InvalidOperationException($"Unsupported economy schema management mode: {options.SchemaManagementMode}.");
        }
    }
}
