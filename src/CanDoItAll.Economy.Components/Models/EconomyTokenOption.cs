using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record EconomyTokenOption(
    string Key,
    string Label,
    TokenId? TokenId,
    Amount AvailableAmount,
    bool IsMain,
    bool CanCreateMore = false,
    Amount SourceAmount = default,
    Amount UniqueAmount = default,
    int SourceUtxoCount = 0,
    int UniqueUtxoCount = 0,
    int TotalUtxoCount = 0)
{
    public static string MainKey { get; } = "__main";

    public string AssetLabel => IsMain ? "Main" : TokenId?.Value ?? Label;

    public bool HasReusableSource => SourceUtxoCount > 0;
}

public sealed record EconomyTokenCreationAuthority(
    string Namespace,
    BusinessObjectId GrantObjectId,
    Address OwnerAddress);

public sealed record EconomyAddressBookmark(
    string Name,
    Address Address);

public sealed record EconomyTokenCreationRequest(
    string TokenNamespace,
    string TokenName,
    Amount Amount,
    Amount MainAmount,
    string Description);

public sealed record BusinessObjectSendRequest(
    BusinessObjectId ObjectId,
    BusinessObjectSendMode Mode,
    Address ReceiverAddress,
    string StateJson,
    string ObjectType,
    string SchemaVersion,
    string? ExternalRef = null);

public enum BusinessObjectSendMode
{
    Original = 0,
    Clone = 1
}

public sealed record BusinessObjectPriceRequest(
    BusinessObjectId ObjectId,
    Amount PriceAmount,
    TokenId? TokenId,
    string StateJson,
    string? ExternalRef = null,
    BusinessObjectSaleMode SaleMode = BusinessObjectSaleMode.Original,
    int? MaxClones = null,
    bool ClearSale = false,
    string? SharePath = null);

public sealed record ProfileEditRequest(
    BusinessObjectId ObjectId,
    string Currency,
    string ApprovalMode,
    string DisplayName,
    string SimulationClock,
    string StateJson,
    string? ExternalRef = null);
