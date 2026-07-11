using System.Text;
using System.Text.Json;
using CanDoItAll.Components.QRCode;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Components.Services;

public interface IEconomyAccountWorkspaceAdapter
{
    string HostMode { get; }

    bool UsesServerSideAccountSigning { get; }

    Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<IBusinessObjectView>> LookupBusinessObjectsAsync(
        BusinessObjectLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectWorkspaceModel workspace = await GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<IBusinessObjectView> query = workspace.BusinessObjects;
        query = request.SourceMode switch
        {
            BusinessObjectSourceMode.Mine => query,
            BusinessObjectSourceMode.Address => query.Where(item => item.OwnerAddress.Value.Equals(request.Query, StringComparison.Ordinal)),
            BusinessObjectSourceMode.DirectReference => query.Where(item =>
                item.ObjectId.Value.Equals(request.Query, StringComparison.Ordinal)
                || item.DataHash.Value.Equals(request.Query, StringComparison.OrdinalIgnoreCase)),
            _ => []
        };
        return query.Take(Math.Clamp(request.Take, 1, 100)).ToArray();
    }

    Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
        int maxTransactions = 10,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
        ProfileEditRequest request,
        CancellationToken cancellationToken = default);

    Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> UseTicketAsync(
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default);

    Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> CreateTokenAsync(
        EconomyTokenCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
        EconomyRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageSecurityActionResult> SignMessageAsync(
        MessageSecuritySignRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageSecurityActionResult> VerifyMessageAsync(
        MessageSecurityVerifyRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageSecurityActionResult> EncryptMessageAsync(
        MessageSecurityEncryptRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageSecurityActionResult> DecryptMessageAsync(
        MessageSecurityDecryptRequest request,
        CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> SendSecureMessageAsync(
        SecureMessageComposeRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new EconomyTransactionExecutionResult(
            BusinessObjectTransactionMode.Create,
            Accepted: false,
            TxId: null,
            LedgerSequence: null,
            "This workspace adapter does not expose secure messaging."));

    Task<SecureMessageOpenResult> OpenSecureMessageAsync(
        SecureMessageOpenRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SecureMessageOpenResult.Fail("This workspace adapter does not expose secure message decryption."));

    Task<SecureMessageAttachmentOpenResult> OpenSecureMessageAttachmentAsync(
        SecureMessageAttachmentOpenRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(SecureMessageAttachmentOpenResult.Fail("This workspace adapter does not expose secure attachment decryption."));
}

public sealed record WasmAccountWorkspaceOptions
{
    public string LedgerId { get; init; } = "default";

    public string? ScenarioId { get; init; }

    public string AccountName { get; init; } = "Account";

    public string ConnectionLabel { get; init; } = "Browser API";

    public Uri? NodeBaseUri { get; init; }
}

public sealed record NodeAccountWorkspaceAdapterOptions
{
    public string AccountName { get; init; } = "alice";
}

public sealed class WasmAccountWorkspaceAdapter(
    ILedgerGateway ledgerGateway,
    ILedgerReadClient ledgerReadClient,
    BusinessObjectTransactionBuilder transactionBuilder,
    IAccountTransactionSigner transactionSigner,
    IEconomyCryptoDriver cryptoDriver,
    IAccountWorkspaceCache workspaceCache,
    IAddressDirectoryService addressDirectoryService,
    IBusinessObjectCastRegistry castRegistry,
    ITimeProvider timeProvider,
    WasmAccountWorkspaceOptions? options = null,
    IBusinessObjectDataItemUploadService? messageUploadService = null,
    IBusinessObjectDataStorageDriver? messageStorageDriver = null) : IEconomyAccountWorkspaceAdapter
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly WasmAccountWorkspaceOptions options = options ?? new();
    private IAccountKeySource? keySource;
    private string? accountNameOverride;

    public string HostMode => "WASM";

    public bool UsesServerSideAccountSigning => false;

    public ValueTask UseUnlockedAccountAsync(
        AccountVaultUnlockedKey unlockedKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unlockedKey);
        keySource = new AccountVaultKeySource(unlockedKey);
        accountNameOverride = unlockedKey.DisplayName;
        return ValueTask.CompletedTask;
    }

    public ValueTask UseKeySourceAsync(
        IAccountKeySource accountKeySource,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        keySource = accountKeySource ?? throw new ArgumentNullException(nameof(accountKeySource));
        accountNameOverride = accountName;
        return ValueTask.CompletedTask;
    }

    public async Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        LedgerAccountBalance balance = await account.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await account.GetSpendableOutputsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTokenType> tokenTypes = await account.GetTokenTypesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerPermissionSource> permissionSources = await account.GetPermissionSourcesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IBusinessObjectView> businessObjects = await account
            .GetBusinessObjectViewsAsync(castRegistry, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await account
            .GetTransactionsAsync(maxCount: 100, includeData: true, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<EconomyTokenCreationAuthority> authorities = CreateTokenCreationAuthorities(permissionSources);
        IReadOnlyList<EconomyTokenOption> tokenOptions = CreateTokenOptions(balance, tokenTypes, authorities);
        EconomyTokenOption? defaultCarrierToken = BusinessObjectCarrierTokenPolicy.SelectDefaultCarrierToken(tokenOptions);
        IReadOnlyList<EconomyAddressBookmark> bookmarks = CreateAddressBookmarks(account.Address, transactions);
        Address receiver = bookmarks.FirstOrDefault(bookmark => bookmark.Address != account.Address)?.Address ?? account.Address;
        DateTimeOffset now = timeProvider.UtcNow;
        BusinessObjectId draftObjectId = BusinessObjectCreateIdFactory.CreateNewObjectId("account-app", "account app message");

        var draft = new BusinessObjectTransactionDraft(
            draftObjectId,
            "account-app-message",
            "1.0",
            account.Address,
            receiver,
            receiver,
            Amount.Zero,
            Amount.FromInt64(1),
            "{\"source\":\"account-app\"}",
            $"account-app://draft/{draftObjectId.Value}",
            TokenId: defaultCarrierToken?.TokenId,
            TokenAmount: defaultCarrierToken is null ? null : Amount.FromInt64(1));

        IBusinessObjectView? selected = businessObjects.FirstOrDefault();
        var editor = new BusinessObjectEditorModel(
            selected?.ObjectId ?? draft.ObjectId,
            selected?.ObjectType ?? draft.ObjectType,
            selected?.SchemaVersion ?? draft.SchemaVersion,
            account.Address,
            selected?.StateJson ?? draft.StateJson,
            now.AddDays(30));

        return new BusinessObjectWorkspaceModel(
            accountNameOverride ?? options.AccountName,
            Context,
            account.Address,
            balance,
            tokenOptions,
            authorities,
            bookmarks,
            businessObjects,
            [],
            transactions,
            draft,
            editor,
            now,
            AccountAlias: keySource?.KeyId,
            NodeBaseUri: options.NodeBaseUri,
            ConnectionLabel: options.ConnectionLabel,
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: permissionSources);
    }

    public async Task<IReadOnlyList<IBusinessObjectView>> LookupBusinessObjectsAsync(
        BusinessObjectLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int take = Math.Clamp(request.Take, 1, 100);
        LedgerBusinessObjectQuery query = request.SourceMode switch
        {
            BusinessObjectSourceMode.Mine => new LedgerBusinessObjectQuery(Context, OwnerAddress: RequiredKeySource.Address, Take: take),
            BusinessObjectSourceMode.Address when Address.TryParse(request.Query, out Address owner) =>
                new LedgerBusinessObjectQuery(Context, OwnerAddress: owner, Take: take),
            BusinessObjectSourceMode.DirectReference =>
                new LedgerBusinessObjectQuery(Context, ObjectId: new BusinessObjectId(request.Query.Trim()), Take: take),
            _ => new LedgerBusinessObjectQuery(Context, Take: take)
        };

        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await ledgerReadClient
            .QueryBusinessObjectsAsync(query, cancellationToken)
            .ConfigureAwait(false);
        if (request.SourceMode == BusinessObjectSourceMode.DirectReference && holdings.Count == 0)
        {
            holdings = await ledgerReadClient
                .QueryBusinessObjectsAsync(new LedgerBusinessObjectQuery(Context, Take: 100), cancellationToken)
                .ConfigureAwait(false);
            holdings = holdings
                .Where(item => item.ObjectId.Value.Equals(request.Query, StringComparison.Ordinal)
                    || item.DataHash.Value.Equals(request.Query, StringComparison.OrdinalIgnoreCase))
                .Take(take)
                .ToArray();
        }

        var views = new List<IBusinessObjectView>(holdings.Count);
        foreach (LedgerBusinessObjectHolding holding in holdings)
        {
            views.Add(await castRegistry.CastAsync(
                new BusinessObjectCastSource(
                    Context,
                    holding.ObjectId,
                    holding.ObjectType,
                    holding.SchemaVersion,
                    holding.Version,
                    holding.OwnerAddress,
                    holding.Operation,
                    holding.DataHash,
                    holding.CurrentOutPoint,
                    holding.StateJson,
                    holding.ExternalRef,
                    holding.ItemType,
                    holding.CreatedAt),
                cancellationToken).ConfigureAwait(false));
        }

        return views;
    }

    public async Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        AccountWorkspaceCacheKey cacheKey = TransactionDetailsCacheKey(txId);
        EconomyTransactionDetailsModel? cached = await workspaceCache
            .GetJsonAsync<EconomyTransactionDetailsModel>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            EconomyTransactionDetailsModel resolvedCached = await ResolveInputDetailsAsync(cached, cancellationToken)
                .ConfigureAwait(false);
            if (!ReferenceEquals(cached.Inputs, resolvedCached.Inputs))
            {
                await workspaceCache.SetJsonAsync(cacheKey, resolvedCached, cancellationToken).ConfigureAwait(false);
            }

            return resolvedCached;
        }

        LedgerTransactionSummary? summary = await ledgerReadClient
            .GetTransactionAsync(Context, txId, includeData: true, cancellationToken)
            .ConfigureAwait(false);
        EconomyTransactionDetailsModel? details = summary is null
            ? null
            : await ResolveInputDetailsAsync(ToDetails(summary), cancellationToken).ConfigureAwait(false);
        if (details is not null)
        {
            await workspaceCache.SetJsonAsync(cacheKey, details, cancellationToken).ConfigureAwait(false);
        }

        return details;
    }

    public async Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
        int maxTransactions = 10,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        IReadOnlyList<LedgerTransactionSummary> transactions;
        try
        {
            transactions = await ledgerReadClient
                .QueryTransactionsAsync(
                    new LedgerTransactionQuery(
                        Context,
                        Address: account.Address,
                        IncludeData: false,
                        Skip: 0,
                        Take: Math.Clamp(maxTransactions, 1, 100)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            transactions = await account
                .GetTransactionsAsync(maxTransactions, includeData: false, cancellationToken)
                .ConfigureAwait(false);
        }

        return AccountWorkspaceRefreshSnapshot.Create(
            Context,
            account.Address,
            transactions,
            maxTransactions,
            timeProvider.UtcNow);
    }

    public async Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        LedgerTransaction unsigned = await BuildUnsignedTransactionAsync(account, draft, cancellationToken).ConfigureAwait(false);
        return await SignAndSubmitAsync(draft.SelectedMode, unsigned, LabelFor(draft.SelectedMode), cancellationToken).ConfigureAwait(false);
    }

    public async Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        LedgerTransaction unsigned = request.Mode == BusinessObjectSendMode.Clone
            ? await account.BuildUnsignedCloneBusinessObjectTransactionAsync(
                new CloneBusinessObjectRequest(
                    request.ObjectId,
                    UniqueObjectId(request.ObjectId),
                    SafeText(request.ObjectType, "business-object"),
                    SafeJson(request.StateJson),
                    SafeText(request.SchemaVersion, "1.0"),
                    request.ReceiverAddress),
                cancellationToken).ConfigureAwait(false)
            : await account.BuildUnsignedTransferBusinessObjectTransactionAsync(
                new TransferBusinessObjectRequest(
                    request.ObjectId,
                    request.ReceiverAddress,
                    null,
                    SafeJson(request.StateJson),
                    SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false);

        BusinessObjectTransactionMode mode = request.Mode == BusinessObjectSendMode.Clone
            ? BusinessObjectTransactionMode.CloneObject
            : BusinessObjectTransactionMode.TransferObject;
        return await SignAndSubmitAsync(
            mode,
            unsigned,
            request.Mode == BusinessObjectSendMode.Clone ? "BO clone send" : "BO send",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        Amount priceAmount = PositiveOrDefault(request.PriceAmount, 1);
        string nextState = BuildSaleStateJson(request, account.Address, priceAmount, timeProvider.UtcNow);
        LedgerTransaction unsigned = await account.BuildUnsignedSetBusinessObjectPriceTransactionAsync(
            new SetBusinessObjectPriceRequest(
                request.ObjectId,
                priceAmount,
                request.TokenId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return await SignAndSubmitAsync(BusinessObjectTransactionMode.UpdateObject, unsigned, "BO sale update", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
        ProfileEditRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        string nextState = BuildProfileStateJson(request, accountNameOverride ?? options.AccountName);
        LedgerTransaction unsigned = request.CreateNew
            ? await account.BuildUnsignedCreateBusinessObjectTransactionAsync(
                new CreateBusinessObjectRequest(
                    request.ObjectId,
                    "profile",
                    nextState,
                    MainAmount: Amount.Zero,
                    ExternalRef: SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false)
            : await account.BuildUnsignedUpdateBusinessObjectTransactionAsync(
                new UpdateBusinessObjectRequest(
                    request.ObjectId,
                    nextState,
                    SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false);

        return await SignAndSubmitAsync(BusinessObjectTransactionMode.UpdateObject, unsigned, request.CreateNew ? "Profile create" : "Profile update", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LedgerTransactionSummary> history = await ledgerReadClient
            .QueryTransactionsAsync(
                new LedgerTransactionQuery(
                    Context,
                    BusinessObjectId: request.ObjectId,
                    IncludeData: true,
                    Skip: 0,
                    Take: 100),
                cancellationToken)
            .ConfigureAwait(false);
        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            request.ObjectType,
            request.ObjectId,
            history);
        BusinessObjectTicketVerificationMessage message = BusinessObjectTicketVerification.CreateVerificationMessage(
            request.ObjectId,
            request.CurrentOutPoint,
            request.OwnerAddress,
            timeProvider.UtcNow);
        if (!validation.IsValid)
        {
            return new BusinessObjectTicketVerificationResult(
                Success: false,
                validation.Summary,
                message.Message,
                message.MessageHash,
                SignatureJson: null,
                message.GeneratedAtUtc,
                message.ExpiresAtUtc,
                validation);
        }

        MessageSecurityActionResult signature = await SignMessageAsync(
            new MessageSecuritySignRequest(accountNameOverride ?? options.AccountName, message.Message),
            cancellationToken).ConfigureAwait(false);
        return new BusinessObjectTicketVerificationResult(
            signature.Success,
            signature.Success ? "Ticket ownership code was signed for the current unused ticket." : signature.Summary,
            message.Message,
            message.MessageHash,
            signature.PayloadJson,
            message.GeneratedAtUtc,
            message.ExpiresAtUtc,
            validation);
    }

    public async Task<EconomyTransactionExecutionResult> UseTicketAsync(
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        IReadOnlyList<LedgerTransactionSummary> history = await ledgerReadClient
            .QueryTransactionsAsync(
                new LedgerTransactionQuery(
                    Context,
                    BusinessObjectId: request.ObjectId,
                    IncludeData: true,
                    Skip: 0,
                    Take: 100),
                cancellationToken)
            .ConfigureAwait(false);
        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            "ticket",
            request.ObjectId,
            history);
        if (!validation.IsValid)
        {
            return new EconomyTransactionExecutionResult(
                BusinessObjectTransactionMode.UpdateObject,
                Accepted: false,
                TxId: null,
                LedgerSequence: null,
                validation.Summary);
        }

        string nextState = BusinessObjectTicketVerification.BuildUsedStateJson(
            request.StateJson,
            request.ObjectId,
            timeProvider.UtcNow,
            request.VerificationHash);
        LedgerTransaction unsigned = await account.BuildUnsignedUpdateBusinessObjectTransactionAsync(
            new UpdateBusinessObjectRequest(
                request.ObjectId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return await SignAndSubmitAsync(BusinessObjectTransactionMode.UpdateObject, unsigned, "Ticket use", cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default)
        => BusinessObjectEventLookup.LookupAsync(
            ledgerReadClient,
            castRegistry,
            Context,
            request.Query,
            cancellationToken);

    public async Task<EconomyTransactionExecutionResult> CreateTokenAsync(
        EconomyTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        string tokenNamespace = PermissionTokenAuthority.NormalizeNamespace(request.TokenNamespace);
        string tokenName = NormalizeTokenSegment(request.TokenName, "asset");
        var tokenId = new TokenId($"{tokenNamespace}.{tokenName}");
        LedgerTransaction unsigned = await account.BuildUnsignedTokenMintTransactionAsync(
            new TokenMintRequest(tokenId, PositiveOrDefault(request.Amount, 1)),
            cancellationToken).ConfigureAwait(false);

        return await SignAndSubmitAsync(BusinessObjectTransactionMode.Create, unsigned, $"Token {tokenId.Value}", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
        EconomyRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        AccountRuleDefinition rule = RuleTokenAuthority.Normalize(request.Rule);
        LedgerTransaction unsigned = await account.BuildUnsignedCreateRuleTokenTransactionAsync(
            new RuleTokenCreateRequest(rule, PermissionSourceOutPoint: request.PermissionSourceOutPoint),
            cancellationToken).ConfigureAwait(false);

        return await SignAndSubmitAsync(BusinessObjectTransactionMode.CreateRuleToken, unsigned, $"Rule {rule.RuleId}", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MessageSecurityActionResult> SignMessageAsync(
        MessageSecuritySignRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return MessageSecurityActionResult.Fail("Message cannot be empty.");
        }

        try
        {
            IAccountKeySource source = RequiredKeySource;
            HashHex payloadHash = await CreateMessagePayloadHashAsync(request.Message, cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> privateKey = await source.GetPrivateKeyMaterialAsync(cancellationToken).ConfigureAwait(false);
            byte[] signatureBytes = await cryptoDriver
                .SignEcdsaP256Sha256Async(Encoding.UTF8.GetBytes(payloadHash.Value), privateKey, cancellationToken)
                .ConfigureAwait(false);
            var signature = new EconomyMessageSignature(
                source.Algorithm,
                source.KeyId,
                source.Address,
                source.PublicKeyHash,
                payloadHash,
                signatureBytes,
                source.PublicKeyMaterial.ToArray());

            return MessageSecurityActionResult.Ok(
                $"Signed by {signature.SignerAddress.Value}. Payload hash {ShortHash(signature.PayloadHash.Value)}.",
                JsonSerializer.Serialize(signature, MessageJsonOptions));
        }
        catch (Exception exception)
        {
            return MessageSecurityActionResult.Fail(exception.Message);
        }
    }

    public async Task<MessageSecurityActionResult> VerifyMessageAsync(
        MessageSecurityVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return MessageSecurityActionResult.Fail("Verification message cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.SignatureJson))
        {
            return MessageSecurityActionResult.Fail("Signature JSON is required.");
        }

        try
        {
            EconomyMessageSignature? signature = JsonSerializer.Deserialize<EconomyMessageSignature>(
                request.SignatureJson,
                MessageJsonOptions);
            if (signature is null)
            {
                return MessageSecurityActionResult.Fail("Signature JSON could not be parsed.");
            }

            if (!signature.SignerAddress.MatchesPublicKey(signature.PublicKeyMaterial)
                || Address.CalculatePublicKeyHash(signature.PublicKeyMaterial) != signature.PublicKeyHash)
            {
                return MessageSecurityActionResult.Fail("Signature public key does not match its signer address.");
            }

            HashHex payloadHash = await CreateMessagePayloadHashAsync(request.Message, cancellationToken).ConfigureAwait(false);
            if (signature.PayloadHash != payloadHash)
            {
                return MessageSecurityActionResult.Fail("Signature is not valid for this message or public key.");
            }

            bool valid = await cryptoDriver
                .VerifyEcdsaP256Sha256Async(
                    Encoding.UTF8.GetBytes(payloadHash.Value),
                    signature.SignatureBytes,
                    signature.PublicKeyMaterial,
                    cancellationToken)
                .ConfigureAwait(false);

            return valid
                ? MessageSecurityActionResult.Ok($"Signature is valid for {signature.SignerAddress.Value}.")
                : MessageSecurityActionResult.Fail("Signature is not valid for this message or public key.");
        }
        catch (Exception exception)
        {
            return MessageSecurityActionResult.Fail(exception.Message);
        }
    }

    public async Task<MessageSecurityActionResult> EncryptMessageAsync(
        MessageSecurityEncryptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return MessageSecurityActionResult.Fail("Message cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.ReceiverAccountAlias))
        {
            return MessageSecurityActionResult.Fail("Receiver address or contact name is required.");
        }

        try
        {
            IAccountKeySource source = RequiredKeySource;
            Address receiverAddress = await ResolveMessageReceiverAddressAsync(
                source.Address,
                request.ReceiverAccountAlias,
                cancellationToken).ConfigureAwait(false);
            var discovery = new LedgerTransactionPublicKeyDiscovery(ledgerReadClient);
            LedgerPublicKeyDiscoveryResult? receiverKey = await discovery
                .FindPublicKeyAsync(Context, receiverAddress, cancellationToken)
                .ConfigureAwait(false);
            if (receiverKey is null)
            {
                return MessageSecurityActionResult.Fail(
                    $"Cannot encrypt for {receiverAddress.Value}: no signed transaction input from that address exposes its public key yet.");
            }

            EconomyEncryptedMessage encrypted = await AccountMessageSecurity
                .EncryptMessageAsync(
                    source,
                    request.Message,
                    receiverAddress,
                    receiverKey.PublicKeyMaterial,
                    cryptoDriver,
                    cancellationToken)
                .ConfigureAwait(false);
            HashHex sharedSecretHash = await AccountMessageSecurity
                .DeriveSharedSecretHashAsync(source, receiverKey.PublicKeyMaterial, cryptoDriver, cancellationToken)
                .ConfigureAwait(false);

            return MessageSecurityActionResult.Ok(
                $"Encrypted for {receiverAddress.Value}. Receiver public key found in {ShortHash(receiverKey.TxId.Value)}. Shared-secret hash {ShortHash(sharedSecretHash.Value)}.",
                JsonSerializer.Serialize(encrypted, MessageJsonOptions));
        }
        catch (Exception exception)
        {
            return MessageSecurityActionResult.Fail(exception.Message);
        }
    }

    public async Task<MessageSecurityActionResult> DecryptMessageAsync(
        MessageSecurityDecryptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EncryptedMessageJson))
        {
            return MessageSecurityActionResult.Fail("Encrypted message JSON is required.");
        }

        try
        {
            EconomyEncryptedMessage? encrypted = JsonSerializer.Deserialize<EconomyEncryptedMessage>(
                request.EncryptedMessageJson,
                MessageJsonOptions);
            if (encrypted is null)
            {
                return MessageSecurityActionResult.Fail("Encrypted message JSON could not be parsed.");
            }

            IAccountKeySource source = RequiredKeySource;
            byte[] senderPublicKey = encrypted.SenderPublicKeyMaterial;
            if (senderPublicKey.Length == 0)
            {
                var discovery = new LedgerTransactionPublicKeyDiscovery(ledgerReadClient);
                LedgerPublicKeyDiscoveryResult? senderKey = await discovery
                    .FindPublicKeyAsync(Context, encrypted.SenderAddress, cancellationToken)
                    .ConfigureAwait(false);
                if (senderKey is null)
                {
                    return MessageSecurityActionResult.Fail(
                        $"Cannot decrypt message from {encrypted.SenderAddress.Value}: sender public key was not found.");
                }

                senderPublicKey = senderKey.PublicKeyMaterial;
            }

            string plainText = await AccountMessageSecurity
                .DecryptMessageAsync(source, encrypted, senderPublicKey, cryptoDriver, cancellationToken)
                .ConfigureAwait(false);

            return MessageSecurityActionResult.Ok(
                $"Decrypted message from {encrypted.SenderAddress.Value}.",
                JsonSerializer.Serialize(new
                {
                    encrypted.Algorithm,
                    encrypted.SenderAddress,
                    encrypted.ReceiverAddress,
                    plainText
                }, MessageJsonOptions),
                plainText);
        }
        catch (Exception exception)
        {
            return MessageSecurityActionResult.Fail(exception.Message);
        }
    }

    public async Task<EconomyTransactionExecutionResult> SendSecureMessageAsync(
        SecureMessageComposeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return new EconomyTransactionExecutionResult(
                BusinessObjectTransactionMode.Create,
                Accepted: false,
                TxId: null,
                LedgerSequence: null,
                "Message body cannot be empty.");
        }

        try
        {
            IAccountKeySource source = RequiredKeySource;
            Address receiverAddress = await ResolveMessageReceiverAddressAsync(
                source.Address,
                request.Receiver,
                cancellationToken).ConfigureAwait(false);
            var discovery = new LedgerTransactionPublicKeyDiscovery(ledgerReadClient);
            LedgerPublicKeyDiscoveryResult? receiverKey = await discovery
                .FindPublicKeyAsync(Context, receiverAddress, cancellationToken)
                .ConfigureAwait(false);
            if (receiverKey is null)
            {
                return new EconomyTransactionExecutionResult(
                    BusinessObjectTransactionMode.Create,
                    Accepted: false,
                    TxId: null,
                    LedgerSequence: null,
                    $"Cannot send secure message: no signed transaction input from {receiverAddress.Value} exposes its public key yet.");
            }

            using var attachmentStreams = new AttachmentStreamScope(request.Attachments);
            SecureLedgerMessageBuildResult secureMessage = await SecureLedgerMessages
                .CreateAsync(
                    source,
                    receiverAddress,
                    receiverKey.PublicKeyMaterial,
                    request.Subject,
                    request.Body,
                    attachmentStreams.Sources,
                    cryptoDriver,
                    messageUploadService,
                    timeProvider.UtcNow,
                    cancellationToken)
                .ConfigureAwait(false);
            BusinessObjectId objectId = BusinessObjectCreateIdFactory.CreateOpaqueObjectId("secure-message");
            BusinessObjectAccount account = CreateAccount();
            LedgerTransaction unsigned = await account.BuildUnsignedCreateBusinessObjectTransactionAsync(
                new CreateBusinessObjectRequest(
                    objectId,
                    SecureLedgerMessages.ObjectType,
                    secureMessage.StateJson,
                    SecureLedgerMessages.SchemaVersion,
                    OwnerAddress: receiverAddress,
                    MainAmount: Amount.Zero,
                    ExternalRef: $"secure-message://{objectId.Value}"),
                cancellationToken).ConfigureAwait(false);

            return await SignAndSubmitAsync(
                BusinessObjectTransactionMode.Create,
                unsigned,
                request.Attachments.Count == 0 ? "Secure message" : $"Secure message with {request.Attachments.Count} attachment(s)",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new EconomyTransactionExecutionResult(
                BusinessObjectTransactionMode.Create,
                Accepted: false,
                TxId: null,
                LedgerSequence: null,
                exception.Message);
        }
    }

    public async Task<SecureMessageOpenResult> OpenSecureMessageAsync(
        SecureMessageOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SecureLedgerMessageContent content = await SecureLedgerMessages
                .DecryptContentAsync(RequiredKeySource, request.Payload, cryptoDriver, cancellationToken)
                .ConfigureAwait(false);
            return SecureMessageOpenResult.Ok($"Decrypted {request.TxId.Value}.", content);
        }
        catch (Exception exception)
        {
            return SecureMessageOpenResult.Fail(exception.Message);
        }
    }

    public async Task<SecureMessageAttachmentOpenResult> OpenSecureMessageAttachmentAsync(
        SecureMessageAttachmentOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (messageStorageDriver is null)
        {
            return SecureMessageAttachmentOpenResult.Fail("Encrypted attachment storage reader is not configured.");
        }

        try
        {
            SecureLedgerMessageContentAttachment? descriptor = request.Content.Attachments
                .FirstOrDefault(item => item.AttachmentId.Equals(request.AttachmentId, StringComparison.Ordinal));
            if (descriptor is null)
            {
                return SecureMessageAttachmentOpenResult.Fail("Attachment metadata was not found in the decrypted message content.");
            }

            SecureLedgerMessageAttachmentPlaintext attachment = await SecureLedgerMessages
                .DecryptAttachmentAsync(
                    RequiredKeySource,
                    request.Payload,
                    request.AttachmentId,
                    descriptor,
                    cryptoDriver,
                    messageStorageDriver,
                    cancellationToken)
                .ConfigureAwait(false);
            return SecureMessageAttachmentOpenResult.Ok($"Decrypted {attachment.FileName}.", attachment);
        }
        catch (Exception exception)
        {
            return SecureMessageAttachmentOpenResult.Fail(exception.Message);
        }
    }

    private LedgerAccountContext Context
        => new(new LedgerId(options.LedgerId), string.IsNullOrWhiteSpace(options.ScenarioId) ? null : new ScenarioId(options.ScenarioId));

    private IAccountKeySource RequiredKeySource
        => keySource ?? throw new InvalidOperationException("The WASM account workspace adapter needs an unlocked account key before use.");

    private BusinessObjectAccount CreateAccount()
        => new(Context, RequiredKeySource, ledgerGateway, ledgerReadClient, transactionBuilder);

    private Task<LedgerTransaction> BuildUnsignedTransactionAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
        => draft.SelectedMode switch
        {
            BusinessObjectTransactionMode.Create => account.BuildUnsignedCreateBusinessObjectTransactionAsync(
                new CreateBusinessObjectRequest(
                    draft.ObjectId,
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.OwnerAddress,
                    draft.MainAmount,
                    draft.TokenId is not null && draft.TokenAmount is not null
                        ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                        : null,
                    ExternalRef: SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.TransferObject => account.BuildUnsignedTransferBusinessObjectTransactionAsync(
                new TransferBusinessObjectRequest(
                    draft.ObjectId,
                    draft.ReceiverAddress,
                    draft.MainAmount,
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.UpdateObject => account.BuildUnsignedUpdateBusinessObjectTransactionAsync(
                new UpdateBusinessObjectRequest(draft.ObjectId, SafeJson(draft.StateJson), SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.CloseObject => account.BuildUnsignedCloseBusinessObjectTransactionAsync(
                new CloseBusinessObjectRequest(draft.ObjectId, SafeJson(draft.StateJson), SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.CloneObject => account.BuildUnsignedCloneBusinessObjectTransactionAsync(
                new CloneBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    draft.TargetObjectId ?? new BusinessObjectId($"{draft.ObjectId.Value}:copy"),
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.OwnerAddress,
                    draft.TokenId is not null && draft.TokenAmount is not null
                        ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                        : null),
                cancellationToken),

            BusinessObjectTransactionMode.MergeObjects => account.BuildUnsignedMergeBusinessObjectsTransactionAsync(
                new MergeBusinessObjectsRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    draft.SecondObjectId ?? draft.ObjectId,
                    draft.TargetObjectId ?? new BusinessObjectId($"{draft.ObjectId.Value}:merged"),
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.SplitBusinessObjectPayment => account.BuildUnsignedSplitBusinessObjectPaymentTransactionAsync(
                new SplitBusinessObjectPaymentRequest(
                    draft.ObjectId,
                    new[]
                    {
                        new BusinessObjectPaymentOutput(draft.PaymentReceiverAddress, PositiveOrDefault(draft.PaymentAmount, 1)),
                        new BusinessObjectPaymentOutput(draft.ReceiverAddress, PositiveOrDefault(draft.SecondaryPaymentAmount ?? Amount.Zero, 1))
                    },
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.MainPayment => account.BuildUnsignedPaymentTransactionAsync(
                new PaymentRequest(draft.PaymentReceiverAddress, PositiveOrDefault(draft.PaymentAmount, 1)),
                cancellationToken),

            BusinessObjectTransactionMode.TokenTransfer => account.BuildUnsignedTokenTransferTransactionAsync(
                new TokenTransferRequest(
                    draft.TokenId ?? new TokenId("token"),
                    PositiveOrDefault(draft.TokenAmount ?? Amount.Zero, 1),
                    draft.PaymentReceiverAddress,
                    draft.MainAmount),
                cancellationToken),

            BusinessObjectTransactionMode.UtxoMerge => account.BuildUnsignedUtxoMergeTransactionAsync(
                new UtxoMergeRequest(
                    draft.SelectedUtxosOrEmpty,
                    ResolveOptionalAddress(draft.PaymentReceiverAddress)),
                cancellationToken),

            BusinessObjectTransactionMode.UtxoSplit => account.BuildUnsignedUtxoSplitTransactionAsync(
                new UtxoSplitRequest(
                    draft.SelectedUtxosOrEmpty,
                    draft.UtxoSplitOutputsOrEmpty),
                cancellationToken),

            _ => throw new InvalidOperationException($"Unsupported transaction mode: {draft.SelectedMode}.")
        };

    private static IReadOnlyList<EconomyTokenCreationAuthority> CreateTokenCreationAuthorities(
        IReadOnlyList<LedgerPermissionSource> permissionSources)
        => permissionSources
            .Where(source => source.Output.BusinessObject is not null)
            .Select(source => new EconomyTokenCreationAuthority(
                source.Grant.Namespace,
                source.Output.BusinessObject!.ObjectId,
                source.Output.Address))
            .Distinct()
            .ToArray();

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions(
        LedgerAccountBalance balance,
        IReadOnlyList<LedgerTokenType> tokenTypes,
        IReadOnlyList<EconomyTokenCreationAuthority> authorities)
    {
        var rows = new List<EconomyTokenOption>
        {
            new(EconomyTokenOption.MainKey, "Main", null, balance.MainBalance, IsMain: true)
        };

        foreach (KeyValuePair<TokenId, Amount> token in balance.TokenBalances.OrderBy(token => token.Key.Value, StringComparer.Ordinal))
        {
            LedgerTokenType? type = tokenTypes.FirstOrDefault(item => item.TokenId == token.Key);
            rows.Add(new EconomyTokenOption(
                token.Key.Value,
                token.Key.Value,
                token.Key,
                token.Value,
                IsMain: false,
                CanCreateMore: authorities.Any(authority => PermissionTokenAuthority.IsTokenInNamespace(token.Key, authority.Namespace)),
                SourceAmount: type?.SourceAmount ?? Amount.Zero,
                UniqueAmount: type?.UniqueAmount ?? Amount.Zero,
                SourceUtxoCount: type?.SourceUtxoCount ?? 0,
                UniqueUtxoCount: type?.UniqueUtxoCount ?? 0,
                TotalUtxoCount: type?.TotalUtxoCount ?? 0));
        }

        return rows;
    }

    private static IReadOnlyList<EconomyAddressBookmark> CreateAddressBookmarks(
        Address accountAddress,
        IReadOnlyList<LedgerTransactionSummary> transactions)
    {
        Address[] addresses = transactions
            .SelectMany(transaction => transaction.InputAddresses.Concat(transaction.OutputAddresses))
            .Where(address => address != accountAddress)
            .Distinct()
            .Take(6)
            .ToArray();

        return addresses.Length == 0
            ? [new EconomyAddressBookmark("Current account", accountAddress)]
            : addresses.Select((address, index) => new EconomyAddressBookmark($"Recent address {index + 1}", address)).ToArray();
    }

    private async Task<EconomyTransactionDetailsModel> ResolveInputDetailsAsync(
        EconomyTransactionDetailsModel details,
        CancellationToken cancellationToken)
    {
        TransactionInputDetailResolver resolver = TransactionInputDetailResolver.Default;
        if (!resolver.NeedsResolution(details.Inputs, details.Outputs))
        {
            return details;
        }

        IReadOnlyList<EconomyTransactionInputDetails> inputs = await resolver
            .ResolveAsync(details.Inputs, details.Outputs, ResolvePreviousOutputAsync, cancellationToken)
            .ConfigureAwait(false);

        return details with { Inputs = inputs };
    }

    private async Task<EconomyTransactionOutputDetails?> ResolvePreviousOutputAsync(
        OutPoint previousOutPoint,
        CancellationToken cancellationToken)
    {
        LedgerTransactionSummary? previous = await ledgerReadClient
            .GetTransactionAsync(Context, previousOutPoint.TxId, includeData: false, cancellationToken)
            .ConfigureAwait(false);

        return previous?.Outputs
            .Where(output => output.OutputIndex == previousOutPoint.OutputIndex)
            .Select(ToOutputDetails)
            .FirstOrDefault();
    }

    private static EconomyTransactionDetailsModel ToDetails(LedgerTransactionSummary summary)
        => new(
            summary.TxId,
            TxHash: null,
            summary.Kind,
            summary.Status,
            SigningMode.Independent,
            summary.Timestamp,
            summary.Data,
            summary.Inputs
                .OrderBy(input => input.InputIndex)
                .Select(input => new EconomyTransactionInputDetails(
                    input.InputIndex,
                    input.PreviousOutPoint,
                    input.Address,
                    MainAmount: null,
                    Token: null,
                    BusinessObject: null,
                    input.SignatureBundle,
                    input.SignaturePayloadHash,
                    input.OutPointKey))
                .ToArray(),
            summary.Outputs
                .OrderBy(output => output.OutputIndex)
                .Select(ToOutputDetails)
                .ToArray(),
            summary.LedgerSequence);

    private static EconomyTransactionOutputDetails ToOutputDetails(LedgerTransactionOutputSummary output)
        => new(
            output.OutputIndex,
            output.Address,
            output.MainAmount,
            output.Token,
            output.BusinessObject,
            output.MetadataHash);

    private async Task<EconomyTransactionExecutionResult> SignAndSubmitAsync(
        BusinessObjectTransactionMode mode,
        LedgerTransaction unsigned,
        string label,
        CancellationToken cancellationToken)
    {
        LedgerTransaction signed = await transactionSigner.SignAsync(unsigned, RequiredKeySource, cancellationToken).ConfigureAwait(false);
        LedgerSubmitResult result = await ledgerGateway.SubmitTransactionAsync(signed, cancellationToken).ConfigureAwait(false);
        return ToExecutionResult(mode, result, label);
    }

    private static EconomyTransactionExecutionResult ToExecutionResult(
        BusinessObjectTransactionMode mode,
        LedgerSubmitResult result,
        string label)
        => new(
            mode,
            result.Accepted,
            result.TxId,
            result.LedgerSequence,
            result.Accepted
                ? $"{label} accepted at ledger sequence {result.LedgerSequence}."
                : string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));

    private async ValueTask<HashHex> CreateMessagePayloadHashAsync(
        string message,
        CancellationToken cancellationToken)
    {
        byte[] hash = await cryptoDriver
            .Sha256Async(Encoding.UTF8.GetBytes(message), cancellationToken)
            .ConfigureAwait(false);
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    private async Task<Address> ResolveMessageReceiverAddressAsync(
        Address accountAddress,
        string receiver,
        CancellationToken cancellationToken)
    {
        string value = receiver.Trim();
        if (Address.TryParse(value, out Address parsed))
        {
            return parsed;
        }

        IReadOnlyList<EconomyAddressBookmark> bookmarks = [];
        try
        {
            IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
                .QueryTransactionsAsync(
                    new LedgerTransactionQuery(
                        Context,
                        Address: accountAddress,
                        IncludeData: false,
                        Skip: 0,
                        Take: 100),
                    cancellationToken)
                .ConfigureAwait(false);
            bookmarks = CreateAddressBookmarks(accountAddress, transactions);
        }
        catch (NotSupportedException)
        {
            // Saved directory entries are still usable without transaction query support.
        }

        AddressDirectorySeed seed = AddressDirectorySeed.FromBookmarks(accountAddress.Value, bookmarks);
        IReadOnlyList<AddressDirectoryEntry> entries = await addressDirectoryService
            .GetEntriesAsync(seed, cancellationToken)
            .ConfigureAwait(false);

        AddressDirectoryEntry? exact = entries.FirstOrDefault(entry =>
            entry.Address.Equals(value, StringComparison.OrdinalIgnoreCase)
            || entry.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase)
            || entry.TagsOrEmpty.Any(tag => tag.Equals(value, StringComparison.OrdinalIgnoreCase)));
        if (exact is not null && Address.TryParse(exact.Address, out Address exactAddress))
        {
            return exactAddress;
        }

        AddressDirectoryEntry[] partial = entries
            .Where(entry => entry.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (partial.Length == 1 && Address.TryParse(partial[0].Address, out Address partialAddress))
        {
            return partialAddress;
        }

        throw new InvalidOperationException(
            $"Receiver '{receiver}' is not a valid address and was not found as a unique saved contact for {accountAddress.Value}.");
    }

    private static BusinessObjectId UniqueObjectId(BusinessObjectId objectId)
        => new($"{objectId.Value}:{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

    private static string NormalizeTokenSegment(string value, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var builder = new StringBuilder(candidate.Length);
        foreach (char character in candidate)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-'
                ? char.ToLowerInvariant(character)
                : '-');
        }

        string normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string BuildSaleStateJson(
        BusinessObjectPriceRequest request,
        Address sellerAddress,
        Amount priceAmount,
        DateTimeOffset listedAtUtc)
    {
        if (request.ClearSale)
        {
            return BusinessObjectCommercePayloadBuilder.ClearSaleTermsStateJson(request.StateJson, request.ObjectId.Value);
        }

        BusinessObjectSaleTerms terms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            request.SaleMode,
            priceAmount,
            sellerAddress,
            listedAtUtc,
            request.SaleMode == BusinessObjectSaleMode.Copy ? request.MaxClones : null,
            request.TokenId,
            request.SharePath);
        return BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(request.StateJson, request.ObjectId.Value, terms);
    }

    internal static string BuildProfileStateJson(ProfileEditRequest request, string accountName)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("profile");
        string displayName = SafeText(request.DisplayName, accountName);
        string bio = SafeText(request.Bio, "Public account profile.");
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["displayName"] = displayName,
            ["currency"] = SafeText(request.Currency, "USD"),
            ["approvalMode"] = SafeText(request.ApprovalMode, "two-step"),
            ["simulationClock"] = SafeText(request.SimulationClock, "manual")
        };

        AddField(fields, "firstName", request.FirstName);
        AddField(fields, "surname", request.Surname);
        AddField(fields, "nickname", request.Nickname);
        AddField(fields, "bio", request.Bio);
        AddField(fields, "imageLink", request.ImageLink);
        AddField(fields, "age", request.Age);
        AddField(fields, "relationshipStatus", request.RelationshipStatus);
        AddField(fields, "contact", request.Contact);
        AddField(fields, "website", request.Website);
        AddField(fields, "role", request.Role);

        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            displayName,
            bio,
            bio,
            "#profile #account",
            Array.Empty<BusinessObjectDataItem>(),
            fields) with
            {
                Author = displayName,
                Link = SafeNullable(request.Website),
                ImageLink = SafeNullable(request.ImageLink)
            };

        return BusinessObjectPayloadBuilder.Serialize(payload);
    }

    private static void AddField(IDictionary<string, string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[key] = value.Trim();
        }
    }

    private static string ShortHash(string value)
        => value.Length > 12 ? $"{value[..12]}..." : value;

    private AccountWorkspaceCacheKey TransactionDetailsCacheKey(TxId txId)
        => new(
            "transaction-details",
            $"{Context.LedgerId.Value}|{Context.ScenarioId?.Value ?? "live"}",
            txId.Value);

    private static string LabelFor(BusinessObjectTransactionMode mode)
        => mode switch
        {
            BusinessObjectTransactionMode.MainPayment => "Payment",
            BusinessObjectTransactionMode.TokenTransfer => "Token transfer",
            BusinessObjectTransactionMode.TransferObject => "BusinessObject transfer",
            BusinessObjectTransactionMode.UpdateObject => "BusinessObject update",
            BusinessObjectTransactionMode.CloseObject => "BusinessObject close",
            BusinessObjectTransactionMode.CloneObject => "BusinessObject clone",
            BusinessObjectTransactionMode.MergeObjects => "BusinessObject merge",
            BusinessObjectTransactionMode.SplitBusinessObjectPayment => "BusinessObject split payment",
            BusinessObjectTransactionMode.UtxoMerge => "UTXO merge",
            BusinessObjectTransactionMode.UtxoSplit => "UTXO split",
            BusinessObjectTransactionMode.CreateRuleToken => "Rule BO create",
            _ => "Transaction"
        };

    private static Address? ResolveOptionalAddress(Address address)
        => Address.TryParse(address.Value, out Address parsed) ? parsed : null;

    private static string SafeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string SafeJson(string? value)
        => string.IsNullOrWhiteSpace(value) ? "{}" : value;

    private static string? SafeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Amount PositiveOrDefault(Amount amount, long fallback)
        => amount.Value > 0m ? amount : Amount.FromInt64(fallback);

    private sealed class AttachmentStreamScope : IDisposable
    {
        private readonly List<MemoryStream> streams = [];

        public AttachmentStreamScope(IReadOnlyList<SecureMessageAttachmentDraft> drafts)
        {
            Sources = drafts.Select(CreateSource).ToArray();
        }

        public IReadOnlyList<SecureLedgerMessageAttachmentSource> Sources { get; }

        public void Dispose()
        {
            foreach (MemoryStream stream in streams)
            {
                stream.Dispose();
            }
        }

        private SecureLedgerMessageAttachmentSource CreateSource(SecureMessageAttachmentDraft draft)
        {
            var stream = new MemoryStream(draft.Data, writable: false);
            streams.Add(stream);
            return new SecureLedgerMessageAttachmentSource(
                stream,
                draft.FileName,
                draft.ContentType,
                draft.Size,
                draft.Description);
        }
    }
}

public sealed class DevelopmentEconomyWorkspaceAdapter(
    EconomyDevelopmentWorkspaceSession session,
    IAccountWorkspaceCache workspaceCache) : IEconomyAccountWorkspaceAdapter
{
    public string HostMode => "SSR";

    public bool UsesServerSideAccountSigning => true;

    public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => session.GetWorkspaceAsync(cancellationToken);

    public async Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
        int maxTransactions = 10,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectWorkspaceModel workspace = await session.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        return AccountWorkspaceRefreshSnapshot.Create(workspace, maxTransactions, workspace.ViewedAtUtc);
    }

    public async Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        AccountWorkspaceCacheKey cacheKey = new("transaction-details", "development", txId.Value);
        EconomyTransactionDetailsModel? cached = await workspaceCache
            .GetJsonAsync<EconomyTransactionDetailsModel>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        EconomyTransactionDetailsModel? details = await session.GetTransactionDetailsAsync(txId, cancellationToken)
            .ConfigureAwait(false);
        if (details is not null)
        {
            await workspaceCache.SetJsonAsync(cacheKey, details, cancellationToken).ConfigureAwait(false);
        }

        return details;
    }

    public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
        => session.SubmitTransactionAsync(draft, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default)
        => session.SendBusinessObjectAsync(request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default)
        => session.SetBusinessObjectPriceAsync(request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
        ProfileEditRequest request,
        CancellationToken cancellationToken = default)
        => session.UpdateProfileAsync(request, cancellationToken);

    public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateTicketVerificationAsync(request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> UseTicketAsync(
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default)
        => session.UseTicketAsync(request, cancellationToken);

    public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default)
        => session.LookupEventBusinessObjectAsync(request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> CreateTokenAsync(
        EconomyTokenCreationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateTokenAsync(request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
        EconomyRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateRuleTokenAsync(request, cancellationToken);

    public Task<MessageSecurityActionResult> SignMessageAsync(
        MessageSecuritySignRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(MessageSecurityActionResult.Fail("The development adapter does not expose message signing."));
    }

    public Task<MessageSecurityActionResult> VerifyMessageAsync(
        MessageSecurityVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(MessageSecurityActionResult.Fail("The development adapter does not expose message verification."));
    }

    public Task<MessageSecurityActionResult> EncryptMessageAsync(
        MessageSecurityEncryptRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(MessageSecurityActionResult.Fail("The development adapter does not expose message encryption."));
    }

    public Task<MessageSecurityActionResult> DecryptMessageAsync(
        MessageSecurityDecryptRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(MessageSecurityActionResult.Fail("The development adapter does not expose message decryption."));
    }
}

public sealed class NodeEconomyWorkspaceAdapter(
    EconomyNodeWorkspaceSession session,
    IAccountWorkspaceCache workspaceCache,
    NodeAccountWorkspaceAdapterOptions? options = null) : IEconomyAccountWorkspaceAdapter
{
    private readonly NodeAccountWorkspaceAdapterOptions options = options ?? new();

    public string HostMode => "SSR";

    public bool UsesServerSideAccountSigning => true;

    public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => session.GetWorkspaceAsync(options.AccountName, cancellationToken);

    public async Task<AccountWorkspaceRefreshSnapshot> GetRefreshSnapshotAsync(
        int maxTransactions = 10,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectWorkspaceModel workspace = await session.GetWorkspaceAsync(options.AccountName, cancellationToken)
            .ConfigureAwait(false);
        return AccountWorkspaceRefreshSnapshot.Create(workspace, maxTransactions, workspace.ViewedAtUtc);
    }

    public async Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        AccountWorkspaceCacheKey cacheKey = new("transaction-details", $"node|{options.AccountName}", txId.Value);
        EconomyTransactionDetailsModel? cached = await workspaceCache
            .GetJsonAsync<EconomyTransactionDetailsModel>(cacheKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        EconomyTransactionDetailsModel? details = await session.GetTransactionDetailsAsync(txId, cancellationToken)
            .ConfigureAwait(false);
        if (details is not null)
        {
            await workspaceCache.SetJsonAsync(cacheKey, details, cancellationToken).ConfigureAwait(false);
        }

        return details;
    }

    public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
        => session.SubmitTransactionAsync(options.AccountName, draft, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default)
        => session.SendBusinessObjectAsync(options.AccountName, request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default)
        => session.SetBusinessObjectPriceAsync(options.AccountName, request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
        ProfileEditRequest request,
        CancellationToken cancellationToken = default)
        => session.UpdateProfileAsync(options.AccountName, request, cancellationToken);

    public Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateTicketVerificationAsync(options.AccountName, request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> UseTicketAsync(
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default)
        => session.UseTicketAsync(options.AccountName, request, cancellationToken);

    public Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default)
        => session.LookupEventBusinessObjectAsync(options.AccountName, request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> CreateTokenAsync(
        EconomyTokenCreationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateTokenAsync(options.AccountName, request, cancellationToken);

    public Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
        EconomyRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default)
        => session.CreateRuleTokenAsync(options.AccountName, request, cancellationToken);

    public Task<MessageSecurityActionResult> SignMessageAsync(
        MessageSecuritySignRequest request,
        CancellationToken cancellationToken = default)
        => session.SignMessageAsync(request with { AccountAlias = options.AccountName }, cancellationToken);

    public Task<MessageSecurityActionResult> VerifyMessageAsync(
        MessageSecurityVerifyRequest request,
        CancellationToken cancellationToken = default)
        => session.VerifyMessageAsync(request with { AccountAlias = options.AccountName }, cancellationToken);

    public Task<MessageSecurityActionResult> EncryptMessageAsync(
        MessageSecurityEncryptRequest request,
        CancellationToken cancellationToken = default)
        => session.EncryptMessageAsync(request with { AccountAlias = options.AccountName }, cancellationToken);

    public Task<MessageSecurityActionResult> DecryptMessageAsync(
        MessageSecurityDecryptRequest request,
        CancellationToken cancellationToken = default)
        => session.DecryptMessageAsync(request with { AccountAlias = options.AccountName }, cancellationToken);
}

public static class EconomyAccountWorkspaceAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddEconomyWasmAccountWorkspaceAdapter(this IServiceCollection services)
    {
        services.TryAddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.TryAddScoped<ITransactionHasher, TransactionHasher>();
        services.TryAddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.TryAddScoped<ITimeProvider, ComponentSystemClock>();
        services.AddCanDoItAllQrCode();
        services.AddBusinessObjectBrowserAccountSigning();
        services.AddBusinessObjectCasting();
        services.TryAddScoped<ITagOccurrenceService, TagOccurrenceService>();
        services.TryAddScoped<IAddressDirectoryService, AddressDirectoryService>();
        services.TryAddScoped<IAccountAppSettingsBackupService, AccountAppSettingsBackupService>();
        services.TryAddScoped<IBusinessObjectVerificationService, BusinessObjectVerificationService>();
        services.TryAddScoped<IAccountWorkspaceCache, InMemoryAccountWorkspaceCache>();
        services.TryAddScoped<BusinessObjectTransactionBuilder>();
        services.TryAddScoped<WasmAccountWorkspaceAdapter>();
        services.TryAddScoped<IEconomyAccountWorkspaceAdapter>(provider => provider.GetRequiredService<WasmAccountWorkspaceAdapter>());
        return services;
    }

    public static IServiceCollection AddEconomySsrAccountWorkspaceAdapters(this IServiceCollection services)
    {
        services.TryAddScoped<IAccountWorkspaceCache, InMemoryAccountWorkspaceCache>();
        services.AddCanDoItAllQrCode();
        services.TryAddScoped<ITagOccurrenceService, TagOccurrenceService>();
        services.TryAddScoped<IAddressDirectoryService, AddressDirectoryService>();
        services.TryAddScoped<IAccountAppSettingsBackupService, AccountAppSettingsBackupService>();
        services.TryAddScoped<IBusinessObjectVerificationService, BusinessObjectVerificationService>();
        services.TryAddScoped<DevelopmentEconomyWorkspaceAdapter>();
        services.TryAddScoped<NodeEconomyWorkspaceAdapter>();
        return services;
    }

    private sealed class ComponentSystemClock : ITimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
