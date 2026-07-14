using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Components.Services;

public sealed class EconomyNodeWorkspaceOptions
{
    public string NodeBaseUri { get; set; } = "http://localhost:5000/";

    public string ApiBasePath { get; set; } = "api/v1";

    public string ApiKeyHeaderName { get; set; } = "X-CDA-Economy-Key";

    public string? ApiKey { get; set; }

    public string? BearerToken { get; set; }

    public string TestKeyRootPath { get; set; } = @"C:\programovani\candoitall-economy\testkeys";

    public string KeySetName { get; set; } = "economy-console-demo";

    public string LedgerId { get; set; } = "default";

    public string? ScenarioId { get; set; }

    public string DefaultAccount { get; set; } = "alice";

    public string PeerAccount { get; set; } = "bob";

    public bool AutoSeedDemoAccounts { get; set; } = true;

    public decimal SeedMainAmount { get; set; } = 10_000m;

    public decimal SeedTokenSourceAmount { get; set; } = 6m;

    public string SeedTokenNamespace { get; set; } = "console.demo";

    public string SeedTokenId { get; set; } = "console.demo.message";

    public string SeedRuleNamespace { get; set; } = "rules.company";

    public decimal SeedRulePermissionAmount { get; set; } = 5m;

    public EconomyNodeLedgerClientOptions CreateLedgerClientOptions()
        => new()
        {
            ApiBasePath = ApiBasePath,
            ApiKeyHeaderName = ApiKeyHeaderName,
            ApiKey = ApiKey,
            BearerToken = BearerToken
        };
}

public sealed class EconomyNodeWorkspaceSession(
    EconomyNodeWorkspaceOptions options,
    IBusinessObjectCastRegistry castRegistry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim seedLock = new(1, 1);
    private bool seedChecked;

    public async Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedAccount = NormalizeAccountName(accountName);
        await EnsureDemoSeededAsync(cancellationToken).ConfigureAwait(false);

        using NodeAccountDriver driver = await LoadDriverAsync(normalizedAccount, cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = driver.Account;

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

        IReadOnlyList<EconomyTokenCreationAuthority> tokenAuthorities = CreateTokenCreationAuthorities(permissionSources);
        IReadOnlyList<EconomyTokenOption> tokenOptions = CreateTokenOptions(balance, tokenTypes, tokenAuthorities);
        IReadOnlyList<EconomyAddressBookmark> bookmarks = await CreateAddressBookmarksAsync(
            normalizedAccount,
            cancellationToken).ConfigureAwait(false);

        EconomyTokenOption? defaultToken = BusinessObjectCarrierTokenPolicy.SelectDefaultCarrierToken(tokenOptions);
        Address peerAddress = bookmarks.FirstOrDefault()?.Address ?? account.Address;
        DateTimeOffset viewedAt = DateTimeOffset.UtcNow;

        var draft = new BusinessObjectTransactionDraft(
            default,
            "message",
            "1.0",
            account.Address,
            peerAddress,
            peerAddress,
            Amount.Zero,
            Amount.FromInt64(1),
            JsonSerializer.Serialize(new
            {
                title = $"Message from {normalizedAccount}",
                text = "Created from the VENFT real-node demo.",
                account = account.Address.Value,
                viewedAtUtc = viewedAt
            }),
            $"venft://{normalizedAccount}/message/draft",
            TokenId: defaultToken?.TokenId,
            TokenAmount: defaultToken is null ? null : Amount.FromInt64(1));

        IBusinessObjectView? selected = businessObjects.FirstOrDefault();
        var editor = new BusinessObjectEditorModel(
            selected?.ObjectId ?? draft.ObjectId,
            selected?.ObjectType ?? draft.ObjectType,
            selected?.SchemaVersion ?? draft.SchemaVersion,
            account.Address,
            selected?.StateJson ?? draft.StateJson,
            viewedAt.AddDays(30));

        return new BusinessObjectWorkspaceModel(
            $"{ToDisplayName(normalizedAccount)} real-node account",
            driver.Context,
            account.Address,
            balance,
            tokenOptions,
            tokenAuthorities,
            bookmarks,
            businessObjects,
            [],
            transactions,
            draft,
            editor,
            viewedAt,
            AccountAlias: normalizedAccount,
            NodeBaseUri: driver.NodeBaseUri,
            ConnectionLabel: $"Real node {driver.NodeBaseUri}",
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: permissionSources);
    }

    public async Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        EconomyNodeTransactionDetails? details = await GetNodeTransactionDetailsAsync(
            txId.Value,
            includeData: true,
            cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        var inputs = new List<EconomyTransactionInputDetails>(details.Inputs.Count);
        foreach (EconomyNodeTransactionInputDetails input in details.Inputs.OrderBy(x => x.InputIndex))
        {
            EconomyNodeTransactionDetails? previous = await GetNodeTransactionDetailsAsync(
                input.PreviousTxId,
                includeData: false,
                cancellationToken).ConfigureAwait(false);
            EconomyNodeTransactionOutputDetails? previousOutput = previous?.Outputs
                .FirstOrDefault(output => output.OutputIndex == input.PreviousOutputIndex);

            inputs.Add(new EconomyTransactionInputDetails(
                input.InputIndex,
                new OutPoint(new TxId(input.PreviousTxId), input.PreviousOutputIndex),
                new Address(input.Address),
                previousOutput is null ? null : new Amount(previousOutput.MainAmount),
                CreateToken(previousOutput?.TokenId, previousOutput?.TokenAmount),
                CreateBusinessObjectDetails(previousOutput),
                input.SignatureBundle ?? SignatureBundle.Empty,
                ToHash(input.SignaturePayloadHash),
                ToOutPointKey(input.OutPointKey)));
        }

        IReadOnlyList<EconomyTransactionOutputDetails> outputs = details.Outputs
            .OrderBy(output => output.OutputIndex)
            .Select(output => new EconomyTransactionOutputDetails(
                output.OutputIndex,
                new Address(output.Address),
                new Amount(output.MainAmount),
                CreateToken(output.TokenId, output.TokenAmount),
                CreateBusinessObjectDetails(output),
                ToHash(output.BusinessObjectDataHash)))
            .ToArray();

        return new EconomyTransactionDetailsModel(
            new TxId(details.TxId),
            ToHash(details.TxHash),
            ParseEnum(details.Kind, TransactionKind.Transfer),
            ParseEnum(details.Status, TransactionStatus.Confirmed),
            SigningMode.Independent,
            details.Timestamp,
            CreateTransactionData(details),
            inputs,
            outputs,
            details.LedgerSequence,
            ToHash(details.LedgerEntryHash));
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
            using NodeAccountDriver driver = await LoadDriverAsync(request.AccountAlias, cancellationToken).ConfigureAwait(false);
            EconomyMessageSignature signature = await driver.Account.SignMessageAsync(request.Message, cancellationToken).ConfigureAwait(false);
            string json = JsonSerializer.Serialize(signature, MessageJsonOptions);
            return MessageSecurityActionResult.Ok(
                $"Signed by {signature.SignerAddress.Value}. Payload hash {ShortHash(signature.PayloadHash.Value)}.",
                json);
        }
        catch (Exception exception)
        {
            return MessageSecurityActionResult.Fail(exception.Message);
        }
    }

    public Task<MessageSecurityActionResult> VerifyMessageAsync(
        MessageSecurityVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Task.FromResult(MessageSecurityActionResult.Fail("Verification message cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(request.SignatureJson))
        {
            return Task.FromResult(MessageSecurityActionResult.Fail("Signature JSON is required."));
        }

        try
        {
            EconomyMessageSignature? signature = JsonSerializer.Deserialize<EconomyMessageSignature>(
                request.SignatureJson,
                MessageJsonOptions);
            if (signature is null)
            {
                return Task.FromResult(MessageSecurityActionResult.Fail("Signature JSON could not be parsed."));
            }

            bool valid = AccountMessageSecurity.VerifyMessage(request.Message, signature);
            return Task.FromResult(valid
                ? MessageSecurityActionResult.Ok($"Signature is valid for {signature.SignerAddress.Value}.")
                : MessageSecurityActionResult.Fail("Signature is not valid for this message or public key."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(MessageSecurityActionResult.Fail(exception.Message));
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

        try
        {
            using NodeAccountDriver driver = await LoadDriverAsync(request.AccountAlias, cancellationToken).ConfigureAwait(false);
            string receiverAlias = NormalizeAccountName(request.ReceiverAccountAlias);
            EconomyAccountKeyEnvelope receiverKey = await LoadKeyAsync(receiverAlias, cancellationToken).ConfigureAwait(false);
            EconomyEncryptedMessage encrypted = await driver.Account.EncryptMessageAsync(
                request.Message,
                receiverKey.Address,
                receiverKey.PublicKeyMaterial,
                cancellationToken).ConfigureAwait(false);
            HashHex sharedSecretHash = await driver.Account.DeriveSharedSecretHashAsync(
                receiverKey.PublicKeyMaterial,
                cancellationToken).ConfigureAwait(false);
            string json = JsonSerializer.Serialize(encrypted, MessageJsonOptions);

            return MessageSecurityActionResult.Ok(
                $"Encrypted for {receiverAlias} ({receiverKey.Address.Value}). Shared-secret hash {ShortHash(sharedSecretHash.Value)}.",
                json);
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

            using NodeAccountDriver driver = await LoadDriverAsync(request.AccountAlias, cancellationToken).ConfigureAwait(false);
            string plainText = await driver.Account.DecryptMessageAsync(encrypted, cancellationToken).ConfigureAwait(false);
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

    public async Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        string? accountName,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = driver.Account;
        LedgerSubmitResult result = draft.SelectedMode switch
        {
            BusinessObjectTransactionMode.Create => await account.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.OwnerAddress,
                    draft.MainAmount,
                    draft.TokenId is null || draft.TokenAmount is null
                        ? null
                        : new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value),
                    ExternalRef: SafeNullable(draft.ExternalRef)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.TransferObject => await account.TransferBusinessObjectAsync(
                new TransferBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    draft.ReceiverAddress,
                    draft.MainAmount,
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.UpdateObject => await account.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.CloseObject => await account.CloseBusinessObjectAsync(
                new CloseBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.CloneObject => await account.CloneBusinessObjectAsync(
                new CloneBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.ReceiverAddress,
                    draft.TokenId is not null && draft.TokenAmount is not null
                        ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                        : null),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.MainPayment => await account.SendPaymentAsync(
                new PaymentRequest(draft.PaymentReceiverAddress, PositiveOrDefault(draft.PaymentAmount, 1)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.TokenTransfer => await account.SendTokenTransferAsync(
                new TokenTransferRequest(
                    draft.TokenId ?? new TokenId(options.SeedTokenId),
                    PositiveOrDefault(draft.TokenAmount ?? Amount.Zero, 1),
                    draft.PaymentReceiverAddress,
                    draft.MainAmount),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.UtxoMerge => await account.MergeUtxosAsync(
                new UtxoMergeRequest(
                    draft.SelectedUtxosOrEmpty,
                    ResolveOptionalAddress(draft.PaymentReceiverAddress)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.UtxoSplit => await account.SplitUtxosAsync(
                new UtxoSplitRequest(
                    draft.SelectedUtxosOrEmpty,
                    draft.UtxoSplitOutputsOrEmpty),
                cancellationToken).ConfigureAwait(false),

            _ => throw new InvalidOperationException($"Unsupported real-node transaction mode: {draft.SelectedMode}.")
        };

        return ToExecutionResult(draft.SelectedMode, result, LabelFor(draft.SelectedMode));
    }

    public async Task<EconomyTransactionExecutionResult> SendBusinessObjectAsync(
        string? accountName,
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = driver.Account;
        LedgerSubmitResult result = request.Mode == BusinessObjectSendMode.Clone
            ? await account.CloneBusinessObjectAsync(
                new CloneBusinessObjectRequest(
                    request.ObjectId,
                    SafeText(request.ObjectType, "business-object"),
                    SafeJson(request.StateJson),
                    SafeText(request.SchemaVersion, "1.0"),
                    request.ReceiverAddress),
                cancellationToken).ConfigureAwait(false)
            : await account.TransferBusinessObjectAsync(
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
        return ToExecutionResult(mode, result, request.Mode == BusinessObjectSendMode.Clone ? "BO clone send" : "BO send");
    }

    public async Task<EconomyTransactionExecutionResult> SetBusinessObjectPriceAsync(
        string? accountName,
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        Amount priceAmount = PositiveOrDefault(request.PriceAmount, 1);
        string nextState = BuildSaleStateJson(request, driver.Account.Address, priceAmount, DateTimeOffset.UtcNow);
        LedgerSubmitResult result = await driver.Account.SetBusinessObjectPriceAsync(
            new SetBusinessObjectPriceRequest(
                request.ObjectId,
                priceAmount,
                request.TokenId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "BO sale update");
    }

    public async Task<EconomyTransactionExecutionResult> UpdateProfileAsync(
        string? accountName,
        ProfileEditRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        string nextState = WasmAccountWorkspaceAdapter.BuildProfileStateJson(
            request,
            ToDisplayName(NormalizeAccountName(accountName)));
        LedgerSubmitResult result = request.CreateNew
            ? await driver.Account.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    "profile",
                    nextState,
                    MainAmount: Amount.Zero,
                    ExternalRef: SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false)
            : await driver.Account.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(
                    request.ObjectId ?? throw new InvalidOperationException("Existing profile BusinessObject id is required for update."),
                    nextState,
                    SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "Profile update");
    }

    public async Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        string? accountName,
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> history = await driver.LedgerDriver.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                driver.Context,
                BusinessObjectId: request.ObjectId,
                IncludeData: true,
                Skip: 0,
                Take: 100),
            cancellationToken).ConfigureAwait(false);
        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            request.ObjectType,
            request.ObjectId,
            history);
        BusinessObjectTicketVerificationMessage message = BusinessObjectTicketVerification.CreateVerificationMessage(
            request.ObjectId,
            request.CurrentOutPoint,
            request.OwnerAddress,
            DateTimeOffset.UtcNow);
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

        EconomyMessageSignature signature = await driver.Account.SignMessageAsync(message.Message, cancellationToken).ConfigureAwait(false);
        string signatureJson = JsonSerializer.Serialize(signature, MessageJsonOptions);
        return new BusinessObjectTicketVerificationResult(
            Success: true,
            "Ticket ownership code was signed for the current unused ticket.",
            message.Message,
            message.MessageHash,
            signatureJson,
            message.GeneratedAtUtc,
            message.ExpiresAtUtc,
            validation);
    }

    public async Task<EconomyTransactionExecutionResult> UseTicketAsync(
        string? accountName,
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> history = await driver.LedgerDriver.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                driver.Context,
                BusinessObjectId: request.ObjectId,
                IncludeData: true,
                Skip: 0,
                Take: 100),
            cancellationToken).ConfigureAwait(false);
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
            DateTimeOffset.UtcNow,
            request.VerificationHash);
        LedgerSubmitResult result = await driver.Account.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(
                request.ObjectId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "Ticket use");
    }

    public async Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        string? accountName,
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        return await BusinessObjectEventLookup
            .LookupAsync(driver.LedgerDriver, castRegistry, driver.Context, request.Query, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EconomyTransactionExecutionResult> CreateTokenAsync(
        string? accountName,
        EconomyTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = driver.Account;
        string tokenNamespace = PermissionTokenAuthority.NormalizeNamespace(request.TokenNamespace);
        string tokenName = NormalizeTokenSegment(request.TokenName, "asset");
        var tokenId = new TokenId($"{tokenNamespace}.{tokenName}");
        Amount sourceAmount = PositiveOrDefault(request.Amount, 1);

        LedgerSubmitResult mintResult = await account.MintTokenAsync(
            new TokenMintRequest(tokenId, sourceAmount),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.Create, mintResult, $"Token {tokenId.Value}");
    }

    public async Task<EconomyTransactionExecutionResult> CreateRuleTokenAsync(
        string? accountName,
        EconomyRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        using NodeAccountDriver driver = await LoadDriverAsync(accountName, cancellationToken).ConfigureAwait(false);
        AccountRuleDefinition rule = RuleTokenAuthority.Normalize(request.Rule);
        LedgerSubmitResult result = await driver.Account.CreateRuleTokenAsync(
                new RuleTokenCreateRequest(rule, PermissionSourceOutPoint: request.PermissionSourceOutPoint),
                cancellationToken)
            .ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.CreateRuleToken, result, $"Rule {rule.RuleId}");
    }

    private async Task EnsureDemoSeededAsync(CancellationToken cancellationToken)
    {
        if (!options.AutoSeedDemoAccounts || seedChecked)
        {
            return;
        }

        await seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (seedChecked)
            {
                return;
            }

            using NodeAccountDriver alice = await LoadDriverAsync("alice", cancellationToken).ConfigureAwait(false);
            using NodeAccountDriver bob = await LoadDriverAsync("bob", cancellationToken).ConfigureAwait(false);

            await EnsureMainBalanceAsync(alice, cancellationToken).ConfigureAwait(false);
            await EnsureMainBalanceAsync(bob, cancellationToken).ConfigureAwait(false);
            await EnsureReusableTokenSourceAsync(alice, cancellationToken).ConfigureAwait(false);
            await EnsureRulePermissionAsync(alice, cancellationToken).ConfigureAwait(false);
            await EnsureRulePermissionAsync(bob, cancellationToken).ConfigureAwait(false);

            seedChecked = true;
        }
        finally
        {
            seedLock.Release();
        }
    }

    private async Task EnsureMainBalanceAsync(NodeAccountDriver driver, CancellationToken cancellationToken)
    {
        LedgerAccountBalance balance = await driver.Account.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        if (balance.MainBalance.Value >= 100m)
        {
            return;
        }

        await driver.LedgerDriver.MintAsync(
            new MintTransactionRequest(
                driver.Account.Address.Value,
                options.SeedMainAmount,
                null,
                null,
                null,
                null,
                null,
                null,
                driver.Context.ScenarioId?.Value),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureReusableTokenSourceAsync(NodeAccountDriver driver, CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerTokenType> tokenTypes = await driver.Account.GetTokenTypesAsync(cancellationToken).ConfigureAwait(false);
        if (tokenTypes.Any(type =>
            type.TokenId.Value.StartsWith(options.SeedTokenNamespace + ".", StringComparison.Ordinal)
            && type.SourceUtxoCount > 0))
        {
            return;
        }

        TokenId tokenId = tokenTypes.Any(type => type.TokenId.Value == options.SeedTokenId)
            ? new TokenId($"{options.SeedTokenNamespace}.ui{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}")
            : new TokenId(options.SeedTokenId);

        IReadOnlyList<LedgerPermissionSource> permissions = await driver.Account.GetMintPermissionSourcesAsync(tokenId, cancellationToken)
            .ConfigureAwait(false);
        if (permissions.Count == 0)
        {
            await GrantPermissionAsync(driver, options.SeedTokenNamespace, 1m, PermissionTokenAuthority.MintNewTokenTypeAction, cancellationToken)
                .ConfigureAwait(false);
        }

        LedgerSubmitResult result = await driver.Account.MintTokenAsync(
            new TokenMintRequest(tokenId, new Amount(options.SeedTokenSourceAmount)),
            cancellationToken).ConfigureAwait(false);
        if (!result.Accepted)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));
        }
    }

    private async Task EnsureRulePermissionAsync(NodeAccountDriver driver, CancellationToken cancellationToken)
    {
        string ruleNamespace = PermissionTokenAuthority.NormalizeNamespace(options.SeedRuleNamespace);
        IReadOnlyList<LedgerPermissionSource> permissions = await driver.Account.GetPermissionSourcesAsync(cancellationToken)
            .ConfigureAwait(false);
        if (permissions.Any(source => source.AllowsRuleCreation(ruleNamespace) && source.Amount.Value >= 1m))
        {
            return;
        }

        await GrantPermissionAsync(
                driver,
                ruleNamespace,
                options.SeedRulePermissionAmount <= 0m ? 1m : options.SeedRulePermissionAmount,
                RuleTokenAuthority.CreateRuleTokenAction,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task GrantPermissionAsync(
        NodeAccountDriver driver,
        string tokenNamespace,
        decimal amount,
        string action,
        CancellationToken cancellationToken)
    {
        await driver.LedgerDriver.GrantPermissionAsync(
            new PermissionGrantRequest(
                tokenNamespace,
                driver.Account.Address.Value,
                amount,
                true,
                action,
                driver.Context.ScenarioId?.Value),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeAccountDriver> LoadDriverAsync(
        string? accountName,
        CancellationToken cancellationToken)
    {
        string normalizedAccount = NormalizeAccountName(accountName);
        EconomyAccountKeyEnvelope key = await LoadKeyAsync(normalizedAccount, cancellationToken).ConfigureAwait(false);
        EconomyNodeLedgerClient ledgerDriver = CreateLedgerDriver();
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var signer = new AccountInputSigner(SignatureProviderRegistry.CreateDefault());
        var builder = new BusinessObjectTransactionBuilder(
            ledgerDriver,
            hasher,
            new SignaturePayloadBuilder(hasher),
            signer,
            new SystemClock());
        var client = new EconomyClient(ledgerDriver, ledgerDriver, builder);
        LedgerAccountContext context = CreateContext();
        BusinessObjectAccount account = await client.LoadAccountAsync(context, key, cancellationToken).ConfigureAwait(false);
        return new NodeAccountDriver(normalizedAccount, context, ledgerDriver, client, account, key);
    }

    private async Task<EconomyAccountKeyEnvelope> LoadKeyAsync(
        string accountName,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(
            options.TestKeyRootPath,
            options.KeySetName,
            "accounts",
            accountName,
            "account-key.json");
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return new EconomyAccountKeyEnvelope(
            new Address(ReadStringOrValueObject(root, "address")),
            ReadAlgorithm(root),
            ReadRequiredString(root, "keyId"),
            Convert.FromBase64String(ReadRequiredString(root, "publicKeyMaterialBase64")),
            Convert.FromBase64String(ReadRequiredString(root, "privateKeyMaterialBase64")),
            ToHash(ReadOptionalString(root, "publicKeyHash") ?? ReadOptionalValueObject(root, "publicKeyHash")));
    }

    private async Task<IReadOnlyList<EconomyAddressBookmark>> CreateAddressBookmarksAsync(
        string accountName,
        CancellationToken cancellationToken)
    {
        string peerName = string.Equals(accountName, "alice", StringComparison.OrdinalIgnoreCase)
            ? "bob"
            : options.PeerAccount;
        EconomyAccountKeyEnvelope peer = await LoadKeyAsync(peerName, cancellationToken).ConfigureAwait(false);
        return [new EconomyAddressBookmark(ToDisplayName(peerName), peer.Address)];
    }

    private static IReadOnlyList<EconomyTokenCreationAuthority> CreateTokenCreationAuthorities(
        IReadOnlyList<LedgerPermissionSource> permissionSources)
        => permissionSources
            .Where(source => string.Equals(source.Grant.Action, PermissionTokenAuthority.MintNewTokenTypeAction, StringComparison.Ordinal)
                && source.Output.BusinessObject is not null)
            .Select(source => new EconomyTokenCreationAuthority(
                source.Grant.Namespace,
                BusinessObjectId.FromOutPoint(source.Output.OutPoint),
                source.Output.Address))
            .OrderBy(authority => authority.Namespace, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions(
        LedgerAccountBalance balance,
        IReadOnlyList<LedgerTokenType> tokenTypes,
        IReadOnlyList<EconomyTokenCreationAuthority> tokenAuthorities)
    {
        Dictionary<TokenId, LedgerTokenType> tokenTypeById = tokenTypes.ToDictionary(type => type.TokenId);
        return
        [
            new(EconomyTokenOption.MainKey, "Main", null, balance.MainBalance, IsMain: true),
            .. balance.TokenBalances
                .OrderBy(token => token.Key.Value, StringComparer.Ordinal)
                .Select(token =>
                {
                    tokenTypeById.TryGetValue(token.Key, out LedgerTokenType? tokenType);
                    return new EconomyTokenOption(
                        token.Key.Value,
                        token.Key.Value,
                        token.Key,
                        token.Value,
                        IsMain: false,
                        CanCreateMore: tokenAuthorities.Any(authority => PermissionTokenAuthority.IsTokenInNamespace(token.Key, authority.Namespace)),
                        SourceAmount: tokenType?.SourceAmount ?? Amount.Zero,
                        UniqueAmount: tokenType?.UniqueAmount ?? Amount.Zero,
                        SourceUtxoCount: tokenType?.SourceUtxoCount ?? 0,
                        UniqueUtxoCount: tokenType?.UniqueUtxoCount ?? 0,
                        TotalUtxoCount: tokenType?.TotalUtxoCount ?? 0);
                })
        ];
    }

    private async Task<EconomyNodeTransactionDetails?> GetNodeTransactionDetailsAsync(
        string txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        using EconomyNodeLedgerClient ledgerDriver = CreateLedgerDriver();
        return await ledgerDriver
            .GetTransactionAsync(new TxId(txId), includeData, cancellationToken)
            .ConfigureAwait(false);
    }

    private EconomyNodeLedgerClient CreateLedgerDriver()
        => new(GetNodeBaseUri(), options.CreateLedgerClientOptions());

    private Uri GetNodeBaseUri()
    {
        string value = string.IsNullOrWhiteSpace(options.NodeBaseUri)
            ? "http://localhost:5000/"
            : options.NodeBaseUri.Trim();
        return new Uri(value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/", UriKind.Absolute);
    }

    private LedgerAccountContext CreateContext()
        => new(new LedgerId(SafeText(options.LedgerId, "default")), ToScenarioId(options.ScenarioId));

    private string NormalizeAccountName(string? accountName)
        => SafeText(accountName, options.DefaultAccount).Trim().ToLowerInvariant();

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

    private static TransactionData? CreateTransactionData(EconomyNodeTransactionDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.DataItemType)
            && string.IsNullOrWhiteSpace(details.DataContentType)
            && string.IsNullOrWhiteSpace(details.DataHash)
            && string.IsNullOrWhiteSpace(details.InlineJson)
            && string.IsNullOrWhiteSpace(details.ExternalRef))
        {
            return null;
        }

        return new TransactionData(
            details.DataContentType,
            ToHash(details.DataHash),
            details.InlineJson,
            details.ExternalRef,
            ParseEnum(details.DataItemType, TransactionDataItemType.Generic));
    }

    private static EconomyTransactionBusinessObjectDetails? CreateBusinessObjectDetails(
        EconomyNodeTransactionOutputDetails? output)
    {
        if (output is null || string.IsNullOrWhiteSpace(output.BusinessObjectId))
        {
            return null;
        }

        return new EconomyTransactionBusinessObjectDetails(
            output.BusinessObjectVersion ?? 0,
            ParseEnum(output.BusinessObjectOperation, BusinessObjectOperation.Create),
            ToHash(output.BusinessObjectDataHash) ?? new HashHex(new string('0', 64)));
    }

    private static TokenTransfer? CreateToken(string? tokenId, decimal? tokenAmount)
        => string.IsNullOrWhiteSpace(tokenId)
            ? null
            : new TokenTransfer(new TokenId(tokenId), new Amount(tokenAmount ?? 0m));

    private static HashHex? ToHash(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new HashHex(value);

    private static OutPointKey? ToOutPointKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new OutPointKey(new HashHex(value));

    private static ScenarioId? ToScenarioId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new ScenarioId(value.Trim());

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

    private static Amount PositiveOrDefault(Amount amount, long fallback)
        => amount.Value > 0m ? amount : Amount.FromInt64(fallback);

    private static string SafeJson(string value)
        => string.IsNullOrWhiteSpace(value) ? "{}" : value;

    private static string SafeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? SafeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToDisplayName(string accountName)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(accountName);

    private static string ShortHash(string value)
        => value.Length > 12 ? $"{value[..12]}..." : value;

    private static string LabelFor(BusinessObjectTransactionMode mode)
        => mode switch
        {
            BusinessObjectTransactionMode.TransferObject => "BO transfer",
            BusinessObjectTransactionMode.UpdateObject => "BO update",
            BusinessObjectTransactionMode.CloseObject => "BO close",
            BusinessObjectTransactionMode.CloneObject => "BO clone",
            BusinessObjectTransactionMode.MainPayment => "Main payment",
            BusinessObjectTransactionMode.TokenTransfer => "Token transfer",
            BusinessObjectTransactionMode.UtxoMerge => "UTXO merge",
            BusinessObjectTransactionMode.UtxoSplit => "UTXO split",
            BusinessObjectTransactionMode.CreateRuleToken => "Rule BO create",
            _ => "BO create"
        };

    private static Address? ResolveOptionalAddress(Address address)
        => Address.TryParse(address.Value, out Address parsed) ? parsed : null;

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
            return BusinessObjectCommercePayloadBuilder.ClearSaleTermsStateJson(request.StateJson, "Untitled BusinessObject");
        }

        BusinessObjectSaleTerms terms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            request.SaleMode,
            priceAmount,
            sellerAddress,
            listedAtUtc,
            request.SaleMode == BusinessObjectSaleMode.Copy ? request.MaxClones : null,
            request.TokenId,
            request.SharePath);
        return BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(request.StateJson, "Untitled BusinessObject", terms);
    }

    private static SignatureAlgorithmId ReadAlgorithm(JsonElement root)
    {
        JsonElement value = root.GetProperty("algorithm");
        return value.ValueKind == JsonValueKind.String
            ? Enum.Parse<SignatureAlgorithmId>(value.GetString()!, ignoreCase: true)
            : (SignatureAlgorithmId)value.GetInt32();
    }

    private static string ReadStringOrValueObject(JsonElement root, string propertyName)
    {
        JsonElement value = root.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetProperty("value").GetString() ?? string.Empty;
    }

    private static string? ReadOptionalValueObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.TryGetProperty("value", out JsonElement nested)
            ? nested.GetString()
            : null;
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
        => ReadOptionalString(root, propertyName)
            ?? throw new InvalidOperationException($"Key file is missing '{propertyName}'.");

    private static string? ReadOptionalString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record NodeAccountDriver(
        string Name,
        LedgerAccountContext Context,
        EconomyNodeLedgerClient LedgerDriver,
        EconomyClient Client,
        BusinessObjectAccount Account,
        EconomyAccountKeyEnvelope Key) : IDisposable
    {
        public Uri NodeBaseUri => LedgerDriver.NodeBaseUri;

        public void Dispose() => LedgerDriver.Dispose();
    }

}

public static class EconomyNodeWorkspaceServiceCollectionExtensions
{
    public static IServiceCollection AddEconomyNodeWorkspaceSession(
        this IServiceCollection services,
        Action<EconomyNodeWorkspaceOptions>? configure = null)
    {
        var options = new EconomyNodeWorkspaceOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddScoped<EconomyNodeWorkspaceSession>();
        return services;
    }
}
