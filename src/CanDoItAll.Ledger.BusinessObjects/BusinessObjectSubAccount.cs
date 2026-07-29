using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed class BusinessObjectSubAccount(string name, BusinessObjectAccount parentAccount, BusinessObjectAccount account)
{
    public string Name { get; } = name;

    public BusinessObjectAccount ParentAccount { get; } = parentAccount;

    public BusinessObjectAccount Account { get; } = account;

    public Address Address => Account.Address;

    public Task<LedgerSubmitResult> CreateBusinessObjectAsync(CreateBusinessObjectRequest request, CancellationToken cancellationToken)
        => Account.CreateBusinessObjectAsync(request, cancellationToken);

    public Task<LedgerSubmitResult> TransferBusinessObjectAsync(TransferBusinessObjectRequest request, CancellationToken cancellationToken)
        => Account.TransferBusinessObjectAsync(request, cancellationToken);

    public Task<LedgerSubmitResult> SendPaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
        => Account.SendPaymentAsync(request, cancellationToken);

    public Task<LedgerSubmitResult> SendTokenTransferAsync(TokenTransferRequest request, CancellationToken cancellationToken)
        => Account.SendTokenTransferAsync(request, cancellationToken);

    public Task<LedgerSubmitResult> MintTokenAsync(TokenMintRequest request, CancellationToken cancellationToken)
        => Account.MintTokenAsync(request, cancellationToken);

    public Task<LedgerSubmitResult> IssuePermissionAsync(PermissionIssueRequest request, CancellationToken cancellationToken)
        => Account.IssuePermissionAsync(request, cancellationToken);

    public Task<LedgerAccountBalance> GetBalanceAsync(CancellationToken cancellationToken)
        => Account.GetBalanceAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(CancellationToken cancellationToken)
        => Account.GetSpendableOutputsAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerTokenType>> GetTokenTypesAsync(CancellationToken cancellationToken)
        => Account.GetTokenTypesAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetTokenSourceOutputsAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
        => Account.GetTokenSourceOutputsAsync(tokenId, cancellationToken);

    public Task<IReadOnlyList<LedgerPermissionSource>> GetPermissionSourcesAsync(CancellationToken cancellationToken)
        => Account.GetPermissionSourcesAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerPermissionSource>> GetMintPermissionSourcesAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
        => Account.GetMintPermissionSourcesAsync(tokenId, cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(CancellationToken cancellationToken)
        => Account.GetBusinessObjectsAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(CancellationToken cancellationToken)
        => Account.GetTransactionsAsync(cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        bool includeData,
        CancellationToken cancellationToken)
        => Account.GetTransactionsAsync(includeData, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        int maxCount,
        CancellationToken cancellationToken)
        => Account.GetTransactionsAsync(maxCount, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        int maxCount,
        bool includeData,
        CancellationToken cancellationToken)
        => Account.GetTransactionsAsync(maxCount, includeData, cancellationToken);

    public Task<IReadOnlyList<IBusinessObjectView>> GetBusinessObjectViewsAsync(
        IBusinessObjectCastRegistry castRegistry,
        CancellationToken cancellationToken)
        => Account.GetBusinessObjectViewsAsync(castRegistry, cancellationToken);
}
