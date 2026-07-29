using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Sdk;

public sealed class LedgerClient
{
    private readonly ILedgerGateway ledgerGateway;
    private readonly ILedgerReadClient ledgerReadClient;
    private readonly BusinessObjectTransactionBuilder transactionBuilder;
    private readonly IAccountRuleExpressionEvaluator ruleExpressionEvaluator;

    public LedgerClient(
        ILedgerGateway ledgerGateway,
        ILedgerReadClient ledgerReadClient,
        BusinessObjectTransactionBuilder transactionBuilder)
        : this(
            ledgerGateway,
            ledgerReadClient,
            transactionBuilder,
            new LedgerAccountHandler(),
            UnavailableAccountRuleExpressionEvaluator.Instance)
    {
    }

    public LedgerClient(
        ILedgerGateway ledgerGateway,
        ILedgerReadClient ledgerReadClient,
        BusinessObjectTransactionBuilder transactionBuilder,
        ILedgerAccountHandler accountHandler)
        : this(
            ledgerGateway,
            ledgerReadClient,
            transactionBuilder,
            accountHandler,
            UnavailableAccountRuleExpressionEvaluator.Instance)
    {
    }

    public LedgerClient(
        ILedgerGateway ledgerGateway,
        ILedgerReadClient ledgerReadClient,
        BusinessObjectTransactionBuilder transactionBuilder,
        ILedgerAccountHandler accountHandler,
        IAccountRuleExpressionEvaluator ruleExpressionEvaluator)
    {
        this.ledgerGateway = ledgerGateway;
        this.ledgerReadClient = ledgerReadClient;
        this.transactionBuilder = transactionBuilder;
        this.ruleExpressionEvaluator = ruleExpressionEvaluator;
        AccountHandler = accountHandler;
    }

    public ILedgerAccountHandler AccountHandler { get; }

    public BusinessObjectAccount LoadAccount(LedgerAccountContext context, LedgerAccountKeyEnvelope key)
        => LoadAccount(context, key.ToKeySource());

    public Task<BusinessObjectAccount> LoadAccountAsync(
        LedgerAccountContext context,
        LedgerAccountKeyEnvelope key,
        CancellationToken cancellationToken)
        => Task.FromResult(LoadAccount(context, key));

    public BusinessObjectAccount LoadAccount(LedgerAccountContext context, IAccountKeySource keySource)
    {
        var account = new BusinessObjectAccount(
            context,
            keySource,
            ledgerGateway,
            ledgerReadClient,
            transactionBuilder,
            ruleExpressionEvaluator);
        AccountHandler.Observe(account);
        return account;
    }

    public Task<BusinessObjectAccount> LoadAccountAsync(
        LedgerAccountContext context,
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
        => Task.FromResult(LoadAccount(context, keySource));

    public BusinessObjectSubAccount CreateSubAccount(
        BusinessObjectAccount parentAccount,
        string name,
        LedgerAccountKeyEnvelope key)
    {
        BusinessObjectSubAccount subAccount = parentAccount.AddSubAccount(name, key.ToKeySource());
        AccountHandler.Observe(parentAccount);
        return subAccount;
    }

    public Task<BusinessObjectSubAccount> CreateSubAccountAsync(
        BusinessObjectAccount parentAccount,
        string name,
        LedgerAccountKeyEnvelope key,
        CancellationToken cancellationToken)
        => Task.FromResult(CreateSubAccount(parentAccount, name, key));

    public BusinessObjectSubAccount ImportSubAccount(
        BusinessObjectAccount parentAccount,
        LedgerSubAccountEnvelope envelope)
    {
        BusinessObjectSubAccount subAccount = parentAccount.AddSubAccount(envelope.Name, envelope.Key.ToKeySource());
        AccountHandler.Observe(parentAccount);
        return subAccount;
    }

    public Task<BusinessObjectSubAccount> ImportSubAccountAsync(
        BusinessObjectAccount parentAccount,
        LedgerSubAccountEnvelope envelope,
        CancellationToken cancellationToken)
        => Task.FromResult(ImportSubAccount(parentAccount, envelope));

    public async Task<LedgerAccountKeyEnvelope> ExportAccountKeyAsync(
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
        => await LedgerAccountKeyEnvelope.FromKeySourceAsync(keySource, cancellationToken).ConfigureAwait(false);

    public async Task<LedgerSubAccountEnvelope> ExportSubAccountAsync(
        BusinessObjectSubAccount subAccount,
        CancellationToken cancellationToken)
        => new(
            subAccount.Name,
            await ExportAccountKeyAsync(subAccount.Account.KeySource, cancellationToken).ConfigureAwait(false));

    public Task<LedgerSubmitResult> SubmitTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken)
        => ledgerGateway.SubmitTransactionAsync(transaction, cancellationToken);

    public Task<LedgerAccountBalance> GetBalanceAsync(
        LedgerAccountContext context,
        Address address,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetBalanceAsync(new LedgerAddressQuery(context, address), cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(
        LedgerAccountContext context,
        Address address,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetBusinessObjectsAsync(new LedgerAddressQuery(context, address), cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAccountContext context,
        Address address,
        CancellationToken cancellationToken)
        => GetTransactionsAsync(context, address, includeData: false, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAccountContext context,
        Address address,
        bool includeData,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetTransactionsAsync(new LedgerAddressQuery(context, address), includeData, cancellationToken);

    public Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetLedgerStateAsync(context, cancellationToken);

    public Task<LedgerTransactionSummary?> GetTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetTransactionAsync(context, txId, includeData, cancellationToken);

    public Task<LedgerBusinessObjectHolding?> GetBusinessObjectAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => ledgerReadClient.GetBusinessObjectAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => ledgerReadClient.QueryBusinessObjectsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
        => ledgerReadClient.QueryTransactionsAsync(query, cancellationToken);

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAccountContext context,
        Address address,
        int maxCount,
        CancellationToken cancellationToken)
        => await GetTransactionsAsync(context, address, maxCount, includeData: false, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAccountContext context,
        Address address,
        int maxCount,
        bool includeData,
        CancellationToken cancellationToken)
    {
        if (maxCount <= 0)
        {
            return Array.Empty<LedgerTransactionSummary>();
        }

        IReadOnlyList<LedgerTransactionSummary> transactions = await GetTransactionsAsync(context, address, includeData, cancellationToken).ConfigureAwait(false);
        return transactions.Take(maxCount).ToArray();
    }

    public Task<LedgerMessageSignature> SignMessageAsync(
        LedgerAccountKeyEnvelope key,
        string message,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.SignMessageAsync(key.ToKeySource(), message, cancellationToken);

    public bool VerifyMessage(string message, LedgerMessageSignature signature)
        => AccountMessageSecurity.VerifyMessage(message, signature);

    public Task<HashHex> DeriveSharedSecretHashAsync(
        LedgerAccountKeyEnvelope key,
        byte[] otherPublicKeyMaterial,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.DeriveSharedSecretHashAsync(key.ToKeySource(), otherPublicKeyMaterial, cancellationToken);

    public Task<LedgerEncryptedMessage> EncryptMessageAsync(
        LedgerAccountKeyEnvelope senderKey,
        string message,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.EncryptMessageAsync(senderKey.ToKeySource(), message, receiverAddress, receiverPublicKeyMaterial, cancellationToken);

    public Task<string> DecryptMessageAsync(
        LedgerAccountKeyEnvelope receiverKey,
        LedgerEncryptedMessage encryptedMessage,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.DecryptMessageAsync(receiverKey.ToKeySource(), encryptedMessage, cancellationToken);
}

public sealed record LedgerSubAccountEnvelope(string Name, LedgerAccountKeyEnvelope Key);

public sealed record LedgerAccountKeyEnvelope(
    Address Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial,
    HashHex? PublicKeyHash = null)
{
    public LedgerAccountKeyEnvelope(
        SignatureAlgorithmId algorithm,
        string keyId,
        byte[] publicKeyMaterial,
        byte[] privateKeyMaterial,
        HashHex? publicKeyHash = null)
        : this(
            Address.FromPublicKey(publicKeyMaterial),
            algorithm,
            keyId,
            publicKeyMaterial,
            privateKeyMaterial,
            publicKeyHash)
    {
    }

    public IAccountKeySource ToKeySource()
        => new InMemoryAccountKeySource(
            Address,
            Algorithm,
            KeyId,
            PublicKeyHash ?? Address.CalculatePublicKeyHash(PublicKeyMaterial),
            PublicKeyMaterial,
            PrivateKeyMaterial);

    public static async ValueTask<LedgerAccountKeyEnvelope> FromKeySourceAsync(
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource
            .GetPrivateKeyMaterialAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LedgerAccountKeyEnvelope(
            keySource.Address,
            keySource.Algorithm,
            keySource.KeyId,
            keySource.PublicKeyMaterial.ToArray(),
            privateKeyMaterial.ToArray(),
            keySource.PublicKeyHash);
    }

}
