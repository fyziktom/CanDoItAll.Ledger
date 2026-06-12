using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record BusinessObjectWorkspaceModel(
    string AccountName,
    LedgerAccountContext Context,
    Address Address,
    LedgerAccountBalance Balance,
    IReadOnlyList<EconomyTokenOption> TokenOptions,
    IReadOnlyList<EconomyTokenCreationAuthority> TokenCreationAuthorities,
    IReadOnlyList<EconomyAddressBookmark> AddressBookmarks,
    IReadOnlyList<IBusinessObjectView> BusinessObjects,
    IReadOnlyList<BusinessObjectSubAccountSummary> SubAccounts,
    IReadOnlyList<LedgerTransactionSummary> Transactions,
    BusinessObjectTransactionDraft TransactionDraft,
    BusinessObjectEditorModel Editor,
    DateTimeOffset ViewedAtUtc,
    string? AccountAlias = null,
    Uri? NodeBaseUri = null,
    string? ConnectionLabel = null,
    IReadOnlyList<LedgerSpendableOutput>? SpendableOutputs = null,
    IReadOnlyList<LedgerTokenType>? TokenTypes = null,
    IReadOnlyList<LedgerPermissionSource>? PermissionSources = null)
{
    public IReadOnlyList<LedgerSpendableOutput> SpendableOutputsOrEmpty => SpendableOutputs ?? [];

    public IReadOnlyList<LedgerTokenType> TokenTypesOrEmpty => TokenTypes ?? [];

    public IReadOnlyList<LedgerPermissionSource> PermissionSourcesOrEmpty => PermissionSources ?? [];
}
