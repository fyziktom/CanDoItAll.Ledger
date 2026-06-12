using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Sdk;

public sealed class EconomyClient
{
    private readonly ILedgerGateway ledgerGateway;
    private readonly ILedgerReadClient ledgerReadClient;
    private readonly BusinessObjectTransactionBuilder transactionBuilder;

    public EconomyClient(
        ILedgerGateway ledgerGateway,
        ILedgerReadClient ledgerReadClient,
        BusinessObjectTransactionBuilder transactionBuilder)
        : this(ledgerGateway, ledgerReadClient, transactionBuilder, new EconomyAccountHandler())
    {
    }

    public EconomyClient(
        ILedgerGateway ledgerGateway,
        ILedgerReadClient ledgerReadClient,
        BusinessObjectTransactionBuilder transactionBuilder,
        IEconomyAccountHandler accountHandler)
    {
        this.ledgerGateway = ledgerGateway;
        this.ledgerReadClient = ledgerReadClient;
        this.transactionBuilder = transactionBuilder;
        AccountHandler = accountHandler;
    }

    public IEconomyAccountHandler AccountHandler { get; }

    public BusinessObjectAccount LoadAccount(LedgerAccountContext context, EconomyAccountKeyEnvelope key)
        => LoadAccount(context, key.ToKeySource());

    public Task<BusinessObjectAccount> LoadAccountAsync(
        LedgerAccountContext context,
        EconomyAccountKeyEnvelope key,
        CancellationToken cancellationToken)
        => Task.FromResult(LoadAccount(context, key));

    public BusinessObjectAccount LoadAccount(LedgerAccountContext context, IAccountKeySource keySource)
    {
        var account = new BusinessObjectAccount(context, keySource, ledgerGateway, ledgerReadClient, transactionBuilder);
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
        EconomyAccountKeyEnvelope key)
    {
        BusinessObjectSubAccount subAccount = parentAccount.AddSubAccount(name, key.ToKeySource());
        AccountHandler.Observe(parentAccount);
        return subAccount;
    }

    public Task<BusinessObjectSubAccount> CreateSubAccountAsync(
        BusinessObjectAccount parentAccount,
        string name,
        EconomyAccountKeyEnvelope key,
        CancellationToken cancellationToken)
        => Task.FromResult(CreateSubAccount(parentAccount, name, key));

    public BusinessObjectSubAccount ImportSubAccount(
        BusinessObjectAccount parentAccount,
        EconomySubAccountEnvelope envelope)
    {
        BusinessObjectSubAccount subAccount = parentAccount.AddSubAccount(envelope.Name, envelope.Key.ToKeySource());
        AccountHandler.Observe(parentAccount);
        return subAccount;
    }

    public Task<BusinessObjectSubAccount> ImportSubAccountAsync(
        BusinessObjectAccount parentAccount,
        EconomySubAccountEnvelope envelope,
        CancellationToken cancellationToken)
        => Task.FromResult(ImportSubAccount(parentAccount, envelope));

    public async Task<EconomyAccountKeyEnvelope> ExportAccountKeyAsync(
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
        => await EconomyAccountKeyEnvelope.FromKeySourceAsync(keySource, cancellationToken).ConfigureAwait(false);

    public async Task<EconomySubAccountEnvelope> ExportSubAccountAsync(
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

    public Task<EconomyMessageSignature> SignMessageAsync(
        EconomyAccountKeyEnvelope key,
        string message,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.SignMessageAsync(key.ToKeySource(), message, cancellationToken);

    public bool VerifyMessage(string message, EconomyMessageSignature signature)
        => AccountMessageSecurity.VerifyMessage(message, signature);

    public Task<HashHex> DeriveSharedSecretHashAsync(
        EconomyAccountKeyEnvelope key,
        byte[] otherPublicKeyMaterial,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.DeriveSharedSecretHashAsync(key.ToKeySource(), otherPublicKeyMaterial, cancellationToken);

    public Task<EconomyEncryptedMessage> EncryptMessageAsync(
        EconomyAccountKeyEnvelope senderKey,
        string message,
        Address receiverAddress,
        byte[] receiverPublicKeyMaterial,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.EncryptMessageAsync(senderKey.ToKeySource(), message, receiverAddress, receiverPublicKeyMaterial, cancellationToken);

    public Task<string> DecryptMessageAsync(
        EconomyAccountKeyEnvelope receiverKey,
        EconomyEncryptedMessage encryptedMessage,
        CancellationToken cancellationToken)
        => AccountMessageSecurity.DecryptMessageAsync(receiverKey.ToKeySource(), encryptedMessage, cancellationToken);
}

public sealed record EconomySubAccountEnvelope(string Name, EconomyAccountKeyEnvelope Key);

public sealed record EconomyAccountKeyEnvelope(
    Address Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    byte[] PublicKeyMaterial,
    byte[] PrivateKeyMaterial,
    HashHex? PublicKeyHash = null)
{
    public EconomyAccountKeyEnvelope(
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

    public static async ValueTask<EconomyAccountKeyEnvelope> FromKeySourceAsync(
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> privateKeyMaterial = await keySource
            .GetPrivateKeyMaterialAsync(cancellationToken)
            .ConfigureAwait(false);

        return new EconomyAccountKeyEnvelope(
            keySource.Address,
            keySource.Algorithm,
            keySource.KeyId,
            keySource.PublicKeyMaterial.ToArray(),
            privateKeyMaterial.ToArray(),
            keySource.PublicKeyHash);
    }

}
