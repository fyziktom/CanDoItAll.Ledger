using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Sdk;

JsonSerializerOptions keyJsonOptions = new(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    Converters = { new JsonStringEnumConverter() }
};
JsonSerializerOptions accountAppBackupJsonOptions = new(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};
JsonSerializerOptions consoleJsonOptions = new(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    Converters = { new JsonStringEnumConverter() }
};

DemoSeedOptions options = DemoSeedOptions.Parse(args);
using HttpClient httpClient = CreateHttpClient(options);
using var nodeClient = new LedgerNodeLedgerClient(
    httpClient,
    new LedgerNodeLedgerClientOptions
    {
        ApiBasePath = options.ApiBasePath,
        ApiKeyHeaderName = options.ApiKeyHeaderName,
        ApiKey = options.ApiKey
    });

CancellationToken cancellationToken = CancellationToken.None;
await WaitForNodeAsync(nodeClient, options, cancellationToken).ConfigureAwait(false);
NodeBootstrapInfo bootstrap = await nodeClient.GetBootstrapInfoAsync(cancellationToken).ConfigureAwait(false);

string ledgerId = string.IsNullOrWhiteSpace(options.LedgerId)
    ? bootstrap.LedgerId
    : options.LedgerId;
ScenarioId? scenarioId = string.IsNullOrWhiteSpace(options.ScenarioId)
    ? null
    : new ScenarioId(options.ScenarioId);
var context = new LedgerAccountContext(new LedgerId(ledgerId), scenarioId);

DemoAccountKey alice = await LoadOrCreateKeyAsync(options, "alice", keyJsonOptions).ConfigureAwait(false);
DemoAccountKey bob = await LoadOrCreateKeyAsync(options, "bob", keyJsonOptions).ConfigureAwait(false);
GeneratedBackup aliceBackup = await WriteAccountAppBackupAsync(options, alice, accountAppBackupJsonOptions, cancellationToken).ConfigureAwait(false);
GeneratedBackup bobBackup = await WriteAccountAppBackupAsync(options, bob, accountAppBackupJsonOptions, cancellationToken).ConfigureAwait(false);
await WriteImportReadmeAsync(options, alice, bob, aliceBackup, bobBackup, cancellationToken).ConfigureAwait(false);

decimal aliceMainBefore = await GetMainBalanceAsync(nodeClient, context, alice.Address, cancellationToken).ConfigureAwait(false);
decimal bobMainBefore = await GetMainBalanceAsync(nodeClient, context, bob.Address, cancellationToken).ConfigureAwait(false);
decimal aliceTokenBefore = await GetTokenBalanceAsync(nodeClient, context, alice.Address, options.TokenId, cancellationToken).ConfigureAwait(false);
decimal bobTokenBefore = await GetTokenBalanceAsync(nodeClient, context, bob.Address, options.TokenId, cancellationToken).ConfigureAwait(false);

TransactionReceipt? aliceMainTransfer = await EnsureMinimumMainBalanceAsync(
    nodeClient,
    context,
    bootstrap.AdminAddress,
    alice.Address,
    options.AliceMainAmount,
    "alice",
    cancellationToken).ConfigureAwait(false);
TransactionReceipt? bobMainTransfer = await EnsureMinimumMainBalanceAsync(
    nodeClient,
    context,
    bootstrap.AdminAddress,
    bob.Address,
    options.BobMainAmount,
    "bob",
    cancellationToken).ConfigureAwait(false);

decimal bobTokenDeficit = Math.Max(0m, options.BobTokenAmount - bobTokenBefore);
decimal aliceTokenMinimumAfterBobFunding = Math.Max(0m, options.TokenSupplyAmount - options.BobTokenAmount);
decimal aliceTokenRequiredBeforeSignedTransfer = aliceTokenMinimumAfterBobFunding + bobTokenDeficit;
decimal aliceTokenBeforeAdminTopUp = await GetTokenBalanceAsync(
    nodeClient,
    context,
    alice.Address,
    options.TokenId,
    cancellationToken).ConfigureAwait(false);

TransactionReceipt? tokenMint = null;
TransactionReceipt? aliceTokenTreasuryTransfer = null;
if (aliceTokenBeforeAdminTopUp < aliceTokenRequiredBeforeSignedTransfer)
{
    decimal treasuryToAliceTokenAmount = aliceTokenRequiredBeforeSignedTransfer - aliceTokenBeforeAdminTopUp;
    tokenMint = await EnsureTreasuryTokenBalanceAsync(
        nodeClient,
        context,
        bootstrap.AdminAddress,
        options.TokenId,
        treasuryToAliceTokenAmount,
        cancellationToken).ConfigureAwait(false);
    aliceTokenTreasuryTransfer = await TransferTokenFromTreasuryAsync(
        nodeClient,
        context,
        bootstrap.AdminAddress,
        alice.Address,
        options.TokenId,
        treasuryToAliceTokenAmount,
        "alice",
        cancellationToken).ConfigureAwait(false);
}

LedgerSubmitResult? aliceToBobTokenTransfer = null;
if (bobTokenDeficit > 0m)
{
    aliceToBobTokenTransfer = await SubmitSignedTokenTransferAsync(
        nodeClient,
        context,
        alice,
        bob.Address,
        options.TokenId,
        bobTokenDeficit,
        cancellationToken).ConfigureAwait(false);
}

decimal aliceMainAfter = await GetMainBalanceAsync(nodeClient, context, alice.Address, cancellationToken).ConfigureAwait(false);
decimal bobMainAfter = await GetMainBalanceAsync(nodeClient, context, bob.Address, cancellationToken).ConfigureAwait(false);
decimal aliceTokenAfter = await GetTokenBalanceAsync(nodeClient, context, alice.Address, options.TokenId, cancellationToken).ConfigureAwait(false);
decimal bobTokenAfter = await GetTokenBalanceAsync(nodeClient, context, bob.Address, options.TokenId, cancellationToken).ConfigureAwait(false);

EnsureMinimum("Alice main", aliceMainAfter, options.AliceMainAmount);
EnsureMinimum("Bob main", bobMainAfter, options.BobMainAmount);
EnsureMinimum($"Alice {options.TokenId}", aliceTokenAfter, aliceTokenMinimumAfterBobFunding);
EnsureMinimum($"Bob {options.TokenId}", bobTokenAfter, options.BobTokenAmount);

if (options.VerifyPermissionTokenBusinessObjectFlow)
{
    PermissionTokenBusinessObjectVerification verification = await VerifyPermissionTokenBusinessObjectFlowAsync(
        nodeClient,
        context,
        alice,
        cancellationToken).ConfigureAwait(false);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = "verified",
        seedTransactions = new
        {
            aliceMainTransferTx = aliceMainTransfer?.TxId,
            bobMainTransferTx = bobMainTransfer?.TxId,
            tokenMintTx = tokenMint?.TxId,
            aliceTokenTransferTx = aliceTokenTreasuryTransfer?.TxId,
            aliceToBobSignedTokenTransferTx = aliceToBobTokenTransfer?.TxId?.Value
        },
        verification
    }, consoleJsonOptions));
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = "seeded",
        node = options.NodeBaseUri.ToString(),
        ledgerId = context.LedgerId.Value,
        scenarioId = context.ScenarioId?.Value,
        token = options.TokenId,
        requested = new
        {
            aliceMain = FormatAmount(options.AliceMainAmount),
            bobMain = FormatAmount(options.BobMainAmount),
            aliceTokenSupply = FormatAmount(options.TokenSupplyAmount),
            bobTokenFromAlice = FormatAmount(options.BobTokenAmount)
        },
        treasury = new
        {
            address = bootstrap.AdminAddress,
            mainAmount = bootstrap.TreasuryMainAmountText,
            tokenMintTx = tokenMint?.TxId
        },
        alice = new
        {
            address = alice.Address,
            rawKeyPath = alice.Path,
            accountAppImportPath = aliceBackup.Path,
            mainBefore = FormatAmount(aliceMainBefore),
            mainAfter = FormatAmount(aliceMainAfter),
            tokenBefore = FormatAmount(aliceTokenBefore),
            tokenAfter = FormatAmount(aliceTokenAfter),
            treasuryMainTransferTx = aliceMainTransfer?.TxId,
            treasuryTokenTransferTx = aliceTokenTreasuryTransfer?.TxId
        },
        bob = new
        {
            address = bob.Address,
            rawKeyPath = bob.Path,
            accountAppImportPath = bobBackup.Path,
            mainBefore = FormatAmount(bobMainBefore),
            mainAfter = FormatAmount(bobMainAfter),
            tokenBefore = FormatAmount(bobTokenBefore),
            tokenAfter = FormatAmount(bobTokenAfter),
            treasuryMainTransferTx = bobMainTransfer?.TxId,
            aliceSignedTokenTransferTx = aliceToBobTokenTransfer?.TxId?.Value
        },
        accountAppImport = new
        {
            password = options.AccountBackupPassword,
            readmePath = Path.Combine(options.KeyRoot, options.KeySetName, "imports", "README.md")
        }
    }, consoleJsonOptions));
}

static HttpClient CreateHttpClient(DemoSeedOptions options)
{
    if (!options.AllowInvalidHttpsCertificate)
    {
        return new HttpClient { BaseAddress = options.NodeBaseUri, Timeout = TimeSpan.FromSeconds(60) };
    }

    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return new HttpClient(handler) { BaseAddress = options.NodeBaseUri, Timeout = TimeSpan.FromSeconds(60) };
}

static async Task WaitForNodeAsync(
    LedgerNodeLedgerClient nodeClient,
    DemoSeedOptions options,
    CancellationToken cancellationToken)
{
    DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(options.WaitTimeout);
    Exception? lastException = null;
    while (DateTimeOffset.UtcNow < deadline)
    {
        try
        {
            NodeHealthResponse health = await nodeClient.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            lastException = exception;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
    }

    throw new TimeoutException($"Ledger node was not ready within {options.WaitTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds.", lastException);
}

static async Task<TransactionReceipt?> EnsureMinimumMainBalanceAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    string treasuryAddress,
    string receiverAddress,
    decimal minimumAmount,
    string receiverName,
    CancellationToken cancellationToken)
{
    decimal current = await GetMainBalanceAsync(nodeClient, context, receiverAddress, cancellationToken).ConfigureAwait(false);
    if (current >= minimumAmount)
    {
        return null;
    }

    decimal amount = minimumAmount - current;
    return await nodeClient.TransferAsync(
        new TransferTransactionRequest(
            treasuryAddress,
            receiverAddress,
            amount,
            null,
            null,
            "DemoFunding",
            JsonSerializer.Serialize(new
            {
                asset = "main",
                receiver = receiverName,
                amount = FormatAmount(amount),
                purpose = "docker-compose-accountapp-demo"
            }),
            $"demo://main/treasury-to-{receiverName}",
            null,
            context.ScenarioId?.Value),
        cancellationToken).ConfigureAwait(false);
}

static async Task<TransactionReceipt?> EnsureTreasuryTokenBalanceAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    string treasuryAddress,
    string tokenId,
    decimal requiredAmount,
    CancellationToken cancellationToken)
{
    if (requiredAmount <= 0m)
    {
        return null;
    }

    decimal treasuryBalance = await GetTokenBalanceAsync(
        nodeClient,
        context,
        treasuryAddress,
        tokenId,
        cancellationToken).ConfigureAwait(false);
    if (treasuryBalance >= requiredAmount)
    {
        return null;
    }

    decimal mintAmount = requiredAmount - treasuryBalance;
    return await nodeClient.MintAsync(
        new MintTransactionRequest(
            treasuryAddress,
            0m,
            tokenId,
            mintAmount,
            "TokenMetadata",
            JsonSerializer.Serialize(new
            {
                token = tokenId,
                amount = FormatAmount(mintAmount),
                purpose = "docker-compose-accountapp-demo-source"
            }),
            $"demo://{tokenId}/treasury-source",
            null,
            context.ScenarioId?.Value),
        cancellationToken).ConfigureAwait(false);
}

static async Task<TransactionReceipt> TransferTokenFromTreasuryAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    string treasuryAddress,
    string receiverAddress,
    string tokenId,
    decimal amount,
    string receiverName,
    CancellationToken cancellationToken)
    => await nodeClient.TransferAsync(
        new TransferTransactionRequest(
            treasuryAddress,
            receiverAddress,
            0m,
            tokenId,
            amount,
            "TokenMetadata",
            JsonSerializer.Serialize(new
            {
                token = tokenId,
                receiver = receiverName,
                amount = FormatAmount(amount),
                purpose = "docker-compose-accountapp-demo-treasury-transfer"
            }),
            $"demo://{tokenId}/treasury-to-{receiverName}",
            null,
            context.ScenarioId?.Value),
        cancellationToken).ConfigureAwait(false);

static async Task<LedgerSubmitResult> SubmitSignedTokenTransferAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    DemoAccountKey sender,
    string receiverAddress,
    string tokenId,
    decimal amount,
    CancellationToken cancellationToken)
{
    ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
    var builder = new BusinessObjectTransactionBuilder(
        nodeClient,
        hasher,
        new SignaturePayloadBuilder(hasher),
        new CryptoDriverAccountInputSigner(new DotNetLedgerCryptoDriver()),
        new SeederSystemClock());
    LedgerTransaction transaction = await builder.BuildTokenTransferAsync(
        context,
        sender.ToKeySource(),
        new TokenTransferRequest(new TokenId(tokenId), Amount.FromDecimal(amount), new Address(receiverAddress)),
        cancellationToken).ConfigureAwait(false);
    LedgerSubmitResult result = await nodeClient.SubmitTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    if (!result.Accepted)
    {
        string errors = result.Errors.Count == 0 ? "unknown rejection" : string.Join("; ", result.Errors.Select(x => x.ToString()));
        throw new InvalidOperationException($"Signed token transfer from {sender.Name} was rejected: {errors}");
    }

    return result;
}

static async Task<PermissionTokenBusinessObjectVerification> VerifyPermissionTokenBusinessObjectFlowAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    DemoAccountKey alice,
    CancellationToken cancellationToken)
{
    const string tokenNamespace = "xray";
    const decimal tokenSupply = 1_000_000_000_000m;
    const decimal carrierAmount = 1m;
    const decimal expectedPlainChange = tokenSupply - carrierAmount;
    var tokenId = new TokenId("xray.image");

    PermissionGrantReceipt grant = await nodeClient.GrantPermissionAsync(
        new PermissionGrantRequest(
            tokenNamespace,
            alice.Address,
            Amount: 1m,
            Repeatable: true,
            Action: PermissionTokenAuthority.MintNewTokenTypeAction,
            context.ScenarioId?.Value),
        cancellationToken).ConfigureAwait(false);
    var grantedPermissionOutPoint = new OutPoint(new TxId(grant.TxId), OutputIndex: 0);

    ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
    var builder = new BusinessObjectTransactionBuilder(
        nodeClient,
        hasher,
        new SignaturePayloadBuilder(hasher),
        new CryptoDriverAccountInputSigner(new DotNetLedgerCryptoDriver()),
        new SeederSystemClock());
    var ledgerClient = new LedgerClient(nodeClient, nodeClient, builder);
    BusinessObjectAccount account = ledgerClient.LoadAccount(context, alice.ToKeySource());

    IReadOnlyList<LedgerPermissionSource> permissionSources = await account
        .GetMintPermissionSourcesAsync(tokenId, cancellationToken)
        .ConfigureAwait(false);
    LedgerPermissionSource permissionSource = permissionSources.SingleOrDefault(
            source => source.OutPoint == grantedPermissionOutPoint)
        ?? throw new InvalidOperationException(
            $"The newly granted xray permission output {FormatOutPoint(grantedPermissionOutPoint)} was not resolved for Alice.");

    LedgerSubmitResult mintResult = await account.MintTokenAsync(
        new TokenMintRequest(
            tokenId,
            Amount.FromDecimal(tokenSupply),
            PermissionSourceOutPoint: permissionSource.OutPoint),
        cancellationToken).ConfigureAwait(false);
    TxId mintTxId = RequireAcceptedTxId(mintResult, "Alice xray.image token mint");
    var mintedTokenOutPoint = new OutPoint(mintTxId, OutputIndex: 0);

    string stateJson = JsonSerializer.Serialize(new
    {
        name = "Xray verification image",
        description = "Token-backed image created by the opt-in live verification flow.",
        token = tokenId.Value,
        tags = new[] { "xray", "image", "verification" }
    });
    LedgerSubmitResult businessObjectResult = await account.CreateBusinessObjectAsync(
        new CreateBusinessObjectRequest(
            "image",
            stateJson,
            Token: new TokenTransfer(tokenId, Amount.FromDecimal(carrierAmount)),
            SourceOutPoint: mintedTokenOutPoint),
        cancellationToken).ConfigureAwait(false);
    TxId businessObjectTxId = RequireAcceptedTxId(businessObjectResult, "Alice xray.image-backed image BO create");
    var businessObjectOutPoint = new OutPoint(businessObjectTxId, OutputIndex: 0);
    var plainTokenChangeOutPoint = new OutPoint(businessObjectTxId, OutputIndex: 1);

    IReadOnlyList<LedgerSpendableOutput> outputs = await account
        .GetSpendableOutputsAsync(cancellationToken)
        .ConfigureAwait(false);
    LedgerSpendableOutput businessObjectOutput = outputs.SingleOrDefault(
            output => output.OutPoint == businessObjectOutPoint)
        ?? throw new InvalidOperationException(
            $"The confirmed image BO output {FormatOutPoint(businessObjectOutPoint)} is not spendable by Alice.");
    if (businessObjectOutput.BusinessObject is null
        || businessObjectOutput.Token?.TokenId != tokenId
        || businessObjectOutput.Token.Amount.Value != carrierAmount)
    {
        throw new InvalidOperationException("The confirmed image BO output does not carry exactly one xray.image token.");
    }

    LedgerSpendableOutput plainTokenChange = outputs.SingleOrDefault(
            output => output.OutPoint == plainTokenChangeOutPoint)
        ?? throw new InvalidOperationException(
            $"The expected plain token change output {FormatOutPoint(plainTokenChangeOutPoint)} is missing.");
    if (plainTokenChange.BusinessObject is not null
        || plainTokenChange.MainAmount != Amount.Zero
        || plainTokenChange.Token?.TokenId != tokenId
        || plainTokenChange.Token.Amount.Value != expectedPlainChange)
    {
        throw new InvalidOperationException(
            $"Expected plain xray.image change {FormatAmount(expectedPlainChange)}, but the confirmed output shape or amount differs.");
    }

    IReadOnlyList<LedgerBusinessObjectHolding> holdings = await account
        .GetBusinessObjectsAsync(cancellationToken)
        .ConfigureAwait(false);
    LedgerBusinessObjectHolding holding = holdings.SingleOrDefault(
            candidate => candidate.CurrentOutPoint == businessObjectOutPoint)
        ?? throw new InvalidOperationException("The confirmed xray.image-backed image BO is absent from Alice's holdings.");
    if (!string.Equals(holding.ObjectType, "image", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected image BO type, but the confirmed type is {holding.ObjectType}.");
    }

    NodeHealthResponse readiness = await nodeClient.GetReadinessAsync(cancellationToken).ConfigureAwait(false);
    bool ledgerVerificationValid = string.Equals(readiness.VerificationStatus, "valid_semantic_payload", StringComparison.OrdinalIgnoreCase)
        || string.Equals(readiness.VerificationStatus, "valid_semantic_trusted", StringComparison.OrdinalIgnoreCase);
    if (!string.Equals(readiness.Status, "ok", StringComparison.OrdinalIgnoreCase)
        || !ledgerVerificationValid
        || readiness.VerificationIssueCount != 0
        || !string.Equals(readiness.ProjectionVerificationStatus, "valid", StringComparison.OrdinalIgnoreCase)
        || readiness.ProjectionIssueCount != 0)
    {
        throw new InvalidOperationException(
            $"Node readiness is not valid after verification: status={readiness.Status}, ledger={readiness.VerificationStatus}, " +
            $"ledgerIssues={readiness.VerificationIssueCount}, projection={readiness.ProjectionVerificationStatus}, " +
            $"projectionIssues={readiness.ProjectionIssueCount}.");
    }

    return new PermissionTokenBusinessObjectVerification(
        TokenId: tokenId.Value,
        Supply: FormatAmount(tokenSupply),
        PermissionGrantTxId: grant.TxId,
        PermissionOutPoint: FormatOutPoint(permissionSource.OutPoint),
        MintTxId: mintTxId.Value,
        MintedTokenOutPoint: FormatOutPoint(mintedTokenOutPoint),
        BusinessObjectTxId: businessObjectTxId.Value,
        BusinessObjectOutPoint: FormatOutPoint(businessObjectOutPoint),
        BusinessObjectCarrierAmount: FormatAmount(carrierAmount),
        PlainTokenChangeOutPoint: FormatOutPoint(plainTokenChangeOutPoint),
        PlainTokenChangeAmount: FormatAmount(plainTokenChange.Token.Amount.Value),
        ReadinessStatus: readiness.Status,
        LedgerVerificationStatus: readiness.VerificationStatus,
        ProjectionVerificationStatus: readiness.ProjectionVerificationStatus,
        LatestLedgerSequence: readiness.LatestLedgerSequence);
}

static TxId RequireAcceptedTxId(LedgerSubmitResult result, string operation)
{
    if (result.Accepted && result.TxId is { } txId)
    {
        return txId;
    }

    string errors = result.Errors.Count == 0
        ? "unknown rejection"
        : string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}"));
    throw new InvalidOperationException($"{operation} was rejected: {errors}");
}

static string FormatOutPoint(OutPoint outPoint)
    => $"{outPoint.TxId.Value}:{outPoint.OutputIndex.ToString(CultureInfo.InvariantCulture)}";

static async Task<decimal> GetMainBalanceAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    string address,
    CancellationToken cancellationToken)
{
    LedgerAccountBalance balance = await nodeClient
        .GetBalanceAsync(new LedgerAddressQuery(context, new Address(address)), cancellationToken)
        .ConfigureAwait(false);
    return balance.MainBalance.Value;
}

static async Task<decimal> GetTokenBalanceAsync(
    LedgerNodeLedgerClient nodeClient,
    LedgerAccountContext context,
    string address,
    string tokenId,
    CancellationToken cancellationToken)
{
    LedgerAccountBalance balance = await nodeClient
        .GetBalanceAsync(new LedgerAddressQuery(context, new Address(address)), cancellationToken)
        .ConfigureAwait(false);
    return balance.TokenBalances.TryGetValue(new TokenId(tokenId), out Amount amount) ? amount.Value : 0m;
}

static void EnsureMinimum(string label, decimal actual, decimal minimum)
{
    if (actual < minimum)
    {
        throw new InvalidOperationException($"{label} balance expected at least {FormatAmount(minimum)}, got {FormatAmount(actual)}.");
    }
}

static async Task<DemoAccountKey> LoadOrCreateKeyAsync(
    DemoSeedOptions options,
    string accountName,
    JsonSerializerOptions jsonOptions)
{
    string directory = Path.Combine(options.KeyRoot, options.KeySetName, "accounts", accountName);
    string path = Path.Combine(directory, "account-key.json");
    if (File.Exists(path))
    {
        AccountKeyFile existing = JsonSerializer.Deserialize<AccountKeyFile>(
            await File.ReadAllTextAsync(path).ConfigureAwait(false),
            jsonOptions)
            ?? throw new InvalidOperationException($"Could not read key file {path}.");
        return DemoAccountKey.FromFile(accountName, path, existing);
    }

    Directory.CreateDirectory(directory);
    (byte[] publicKeyMaterial, byte[] privateKeyMaterial) = EcdsaP256KeyMaterial.CreateKeyPair();
    string address = Address.FromPublicKey(publicKeyMaterial).Value;
    var created = new AccountKeyFile(
        address,
        SignatureAlgorithmId.EcdsaP256Sha256,
        $"compose-demo-{accountName}-{Guid.NewGuid():N}",
        Convert.ToBase64String(publicKeyMaterial),
        Convert.ToBase64String(privateKeyMaterial),
        Address.CalculatePublicKeyHash(publicKeyMaterial).Value);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(created, jsonOptions)).ConfigureAwait(false);
    return DemoAccountKey.FromFile(accountName, path, created);
}

static async Task<GeneratedBackup> WriteAccountAppBackupAsync(
    DemoSeedOptions options,
    DemoAccountKey account,
    JsonSerializerOptions backupJsonOptions,
    CancellationToken cancellationToken)
{
    string importDirectory = Path.Combine(options.KeyRoot, options.KeySetName, "imports");
    Directory.CreateDirectory(importDirectory);
    string path = Path.Combine(importDirectory, $"{account.Name}-accountapp-backup.json");
    string sourceVaultId = $"{options.KeySetName}-{account.Name}-source";

    var vaultStore = new InMemoryAccountVaultStore();
    var vaultService = new AccountVaultService(new DotNetLedgerCryptoDriver(), vaultStore);
    AccountVaultCreationResult imported = await vaultService.ImportAsync(
        new AccountVaultImportRequest(
            sourceVaultId,
            options.AccountBackupPassword,
            new Address(account.Address),
            account.Algorithm,
            account.KeyId,
            new HashHex(account.PublicKeyHash),
            account.PublicKeyMaterial,
            account.PrivateKeyMaterial,
            $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(account.Name)} demo account"),
        cancellationToken).ConfigureAwait(false);
    var backup = new AccountAppVaultBackup(
        "candoitall-economy-accountapp-vault-backup-v1",
        DateTimeOffset.UtcNow,
        imported.Record);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(backup, backupJsonOptions), cancellationToken)
        .ConfigureAwait(false);

    return new GeneratedBackup(path, sourceVaultId);
}

static async Task WriteImportReadmeAsync(
    DemoSeedOptions options,
    DemoAccountKey alice,
    DemoAccountKey bob,
    GeneratedBackup aliceBackup,
    GeneratedBackup bobBackup,
    CancellationToken cancellationToken)
{
    string importDirectory = Path.Combine(options.KeyRoot, options.KeySetName, "imports");
    Directory.CreateDirectory(importDirectory);
    string path = Path.Combine(importDirectory, "README.md");
    string content = $"""
        # CanDoItAll Ledger AccountApp demo imports

        These files are generated by `CanDoItAll.Ledger.DemoSeeder`.

        Backup password: `{options.AccountBackupPassword}`

        Alice address: `{alice.Address}`
        Alice import file: `{aliceBackup.Path}`
        Alice source vault id: `{aliceBackup.SourceVaultId}`

        Bob address: `{bob.Address}`
        Bob import file: `{bobBackup.Path}`
        Bob source vault id: `{bobBackup.SourceVaultId}`

        Import flow:
        1. Open one of the nginx-hosted AccountApp instances.
        2. Paste the JSON from the account backup file.
        3. Use the backup password above as the source backup password.
        4. Choose any new local browser-vault password for that app origin.
        """;
    await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
}

static string FormatAmount(decimal amount)
    => amount.ToString("0.#############################", CultureInfo.InvariantCulture);

internal sealed record PermissionTokenBusinessObjectVerification(
    string TokenId,
    string Supply,
    string PermissionGrantTxId,
    string PermissionOutPoint,
    string MintTxId,
    string MintedTokenOutPoint,
    string BusinessObjectTxId,
    string BusinessObjectOutPoint,
    string BusinessObjectCarrierAmount,
    string PlainTokenChangeOutPoint,
    string PlainTokenChangeAmount,
    string ReadinessStatus,
    string LedgerVerificationStatus,
    string ProjectionVerificationStatus,
    long LatestLedgerSequence);

internal sealed record DemoSeedOptions(
    Uri NodeBaseUri,
    string ApiBasePath,
    string ApiKeyHeaderName,
    string? ApiKey,
    string KeyRoot,
    string KeySetName,
    string LedgerId,
    string? ScenarioId,
    string TokenId,
    decimal AliceMainAmount,
    decimal BobMainAmount,
    decimal TokenSupplyAmount,
    decimal BobTokenAmount,
    string AccountBackupPassword,
    TimeSpan WaitTimeout,
    bool VerifyPermissionTokenBusinessObjectFlow,
    bool AllowInvalidHttpsCertificate)
{
    public static DemoSeedOptions Parse(string[] args)
    {
        string node = Read(args, "--node")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_NODE_URL")
            ?? "http://localhost:8080/";
        string keyRoot = Read(args, "--keys")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_TEST_KEY_ROOT")
            ?? Path.Combine(Environment.CurrentDirectory, "docker", "demo-keys");
        string tokenId = Read(args, "--token")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_TOKEN_ID")
            ?? "bo.posts";
        decimal tokenSupply = ReadDecimal(
            Read(args, "--alice-token-supply")
                ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_ALICE_TOKEN_SUPPLY")
                ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_TOKEN_SOURCE_AMOUNT"),
            100_000_000m);
        decimal bobToken = ReadDecimal(
            Read(args, "--bob-token-amount")
                ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_BOB_TOKEN_AMOUNT")
                ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_TRANSFER_AMOUNT"),
            1_000m);
        int waitSeconds = ReadInt(
            Read(args, "--wait-seconds") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_WAIT_SECONDS"),
            240);

        return new DemoSeedOptions(
            EnsureTrailingSlash(new Uri(node, UriKind.Absolute)),
            Read(args, "--api-base-path") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_API_BASE_PATH") ?? "api/v1",
            Read(args, "--api-key-header") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_API_KEY_HEADER") ?? "X-CDA-Ledger-Key",
            Read(args, "--api-key") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_API_KEY"),
            keyRoot,
            Read(args, "--key-set") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_KEY_SET") ?? "accountapp-demo",
            Read(args, "--ledger") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_LEDGER_ID") ?? string.Empty,
            Read(args, "--scenario") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_SCENARIO_ID"),
            tokenId,
            ReadDecimal(
                Read(args, "--alice-main-amount") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_ALICE_MAIN_AMOUNT"),
                100_000m),
            ReadDecimal(
                Read(args, "--bob-main-amount") ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_DEMO_BOB_MAIN_AMOUNT"),
                1_000m),
            tokenSupply,
            bobToken,
            Read(args, "--account-backup-password")
                ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_ACCOUNT_BACKUP_PASSWORD")
                ?? "accountapp-demo-password",
            TimeSpan.FromSeconds(waitSeconds),
            HasSwitch(args, "--verify-permission-token-bo-flow")
                || ReadBool(Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_VERIFY_PERMISSION_TOKEN_BO_FLOW")),
            HasSwitch(args, "--allow-invalid-https-certificate")
                || ReadBool(Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_ALLOW_INVALID_HTTPS_CERTIFICATE")));
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        string value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(value + "/", UriKind.Absolute);
    }

    private static string? Read(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            return args[i + 1];
        }

        return null;
    }

    private static bool HasSwitch(string[] args, string name)
        => args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static decimal ReadDecimal(string? value, decimal fallback)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : fallback;

    private static int ReadInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    private static bool ReadBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;
}

internal sealed record DemoAccountKey(
    string Name,
    string Address,
    string Path,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    string PublicKeyHash,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial)
{
    public static DemoAccountKey FromFile(string name, string path, AccountKeyFile file)
        => new(
            name,
            file.Address,
            path,
            file.Algorithm,
            file.KeyId,
            file.PublicKeyHash,
            Convert.FromBase64String(file.PublicKeyMaterialBase64),
            Convert.FromBase64String(file.PrivateKeyMaterialBase64));

    public IAccountKeySource ToKeySource()
        => new InMemoryAccountKeySource(
            new Address(Address),
            Algorithm,
            KeyId,
            new HashHex(PublicKeyHash),
            PublicKeyMaterial,
            PrivateKeyMaterial);
}

internal sealed record AccountKeyFile(
    string Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    string PublicKeyMaterialBase64,
    string PrivateKeyMaterialBase64,
    string PublicKeyHash);

internal sealed record AccountAppVaultBackup(
    string Format,
    DateTimeOffset ExportedAtUtc,
    AccountVaultRecord Record);

internal sealed record GeneratedBackup(string Path, string SourceVaultId);

internal sealed class SeederSystemClock : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
