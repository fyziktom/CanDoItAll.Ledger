using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record NodeAdminAddressSummary(
    string Label,
    string Address,
    string Role,
    string Balance,
    int BusinessObjects,
    int Transactions,
    string Status,
    DateTimeOffset LastSeenUtc);

public sealed record NodeAdminTransactionSummary(
    string TxId,
    string Route,
    TransactionKind Kind,
    TransactionStatus Status,
    int Inputs,
    int Outputs,
    string PrimaryAddress,
    string Data,
    DateTimeOffset TimestampUtc);
