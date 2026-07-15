using System.Globalization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components;

internal static class BusinessObjectCardDisplay
{
    public static string DisplayName(IBusinessObjectView item)
    {
        if (item is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Name))
        {
            return media.Name;
        }

        BusinessObjectMediaPayload? payload = PayloadFor(item);
        return string.IsNullOrWhiteSpace(payload?.Name)
            ? item.ObjectId.Value
            : payload.Name;
    }

    public static string Description(IBusinessObjectView item)
    {
        if (item is IMediaBusinessObjectView media)
        {
            string mediaDescription = !string.IsNullOrWhiteSpace(media.Description)
                ? media.Description
                : media.Text;
            if (!string.IsNullOrWhiteSpace(mediaDescription))
            {
                return TrimForCard(mediaDescription);
            }
        }

        BusinessObjectMediaPayload? payload = PayloadFor(item);
        string state = !string.IsNullOrWhiteSpace(payload?.Description)
            ? payload.Description
            : !string.IsNullOrWhiteSpace(payload?.Text)
                ? payload.Text
                : StateText(item);

        return TrimForCard(state);
    }

    public static string StateText(IBusinessObjectView item)
        => item.StateJson ?? string.Empty;

    public static IReadOnlyList<string> TagsFor(IBusinessObjectView item)
    {
        IEnumerable<string> mediaTags = item is IMediaBusinessObjectView media
            ? media.Tags
            : [];
        BusinessObjectMediaPayload? payload = PayloadFor(item);
        IEnumerable<string> payloadTags = string.IsNullOrWhiteSpace(payload?.Tags)
            ? []
            : payload.Tags
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(tag => tag.TrimStart('#'));

        return mediaTags
            .Concat(payloadTags)
            .Concat(item.ObjectType.Split(['-', ':', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat([item.Operation.ToString(), $"v{item.Version}"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    public static BusinessObjectDataItem? ImageFor(IBusinessObjectView item)
    {
        IReadOnlyList<BusinessObjectDataItem> mediaDataItems = item is IMediaBusinessObjectView media
            ? media.DataItems
            : [];
        BusinessObjectMediaPayload? payload = PayloadFor(item);
        BusinessObjectDataItem? dataItem = mediaDataItems
            .Concat(payload?.DataItems ?? [])
            .OrderByDescending(dataItem => dataItem.IsMain)
            .FirstOrDefault(dataItem =>
                dataItem.Type == BusinessObjectDataItemType.Image
                && (dataItem.Uri is not null
                    || dataItem.Storage == BusinessObjectDataItemStorage.Ipfs && !string.IsNullOrWhiteSpace(dataItem.Hash)));

        if (dataItem is not null)
        {
            return dataItem;
        }

        if (!string.IsNullOrWhiteSpace(payload?.ImageLink)
            && Uri.TryCreate(payload.ImageLink, UriKind.Absolute, out Uri? uri))
        {
            return new BusinessObjectDataItem(
                "Image",
                BusinessObjectDataItemType.Image,
                BusinessObjectDataItemStorage.Url,
                uri.ToString(),
                uri,
                "image/*",
                IsMain: true);
        }

        return null;
    }

    public static IReadOnlyList<BusinessObjectDisplayFact> FactsFor(IBusinessObjectView item)
    {
        if (item is ITypedBusinessObjectView typed)
        {
            return typed.Facts;
        }

        if (!BusinessObjectFamilyCatalog.TryFindByObjectType(item.ObjectType, out BusinessObjectFamilyDefinition definition))
        {
            return [];
        }

        return BusinessObjectPayloadBuilder.CreateFacts(definition, PayloadFor(item)?.Fields);
    }

    public static BusinessObjectSaleTerms? SaleTermsFor(IBusinessObjectView item)
        => BusinessObjectCommercePayloadBuilder.TryGetSaleTerms(item.StateJson, out BusinessObjectSaleTerms? terms)
            ? terms
            : null;

    public static DateTimeOffset SortTimestampFor(IBusinessObjectView item, BusinessObjectWorkspaceModel? workspace)
        => workspace?.Transactions
            .FirstOrDefault(transaction => transaction.TxId == item.CurrentOutPoint.TxId)
            ?.Timestamp
            ?? item.CreatedAt;

    public static int TransactionOrderFor(IBusinessObjectView item, BusinessObjectWorkspaceModel? workspace)
    {
        if (workspace is null)
        {
            return int.MaxValue;
        }

        for (int index = 0; index < workspace.Transactions.Count; index++)
        {
            if (workspace.Transactions[index].TxId == item.CurrentOutPoint.TxId)
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    public static string FormatPrice(decimal amount, string? tokenId)
        => $"{amount.ToString("0.#############################", CultureInfo.InvariantCulture)} {(string.IsNullOrWhiteSpace(tokenId) ? BusinessObjectCommercePayloadBuilder.MainCurrencyCode : tokenId)}";

    public static string PreviewIcon(IBusinessObjectView item)
        => BusinessObjectFamilyCatalog.TryFindByObjectType(item.ObjectType, out BusinessObjectFamilyDefinition definition)
            ? definition.Icon
            : item.ObjectType switch
            {
                "settings" or "account-settings" => "settings",
                "purchase-order" => "receipt_long",
                "inventory-lot" => "inventory_2",
                _ => "widgets"
            };

    public static string PreviewClass(IBusinessObjectView item)
        => BusinessObjectFamilyCatalog.TryFindByObjectType(item.ObjectType, out _)
            ? "cda-bo-gallery-preview cda-bo-gallery-preview--icon"
            : item.ObjectType switch
            {
                "settings" or "account-settings" => "cda-bo-gallery-preview cda-bo-gallery-preview--icon cda-bo-gallery-preview--settings",
                "purchase-order" => "cda-bo-gallery-preview cda-bo-gallery-preview--icon cda-bo-gallery-preview--order",
                "inventory-lot" => "cda-bo-gallery-preview cda-bo-gallery-preview--icon cda-bo-gallery-preview--inventory",
                _ => "cda-bo-gallery-preview cda-bo-gallery-preview--icon"
            };

    public static string ShortObjectId(BusinessObjectId objectId)
        => objectId.Value.Length > 18 ? $"{objectId.Value[..10]}...{objectId.Value[^6..]}" : objectId.Value;

    public static BusinessObjectMediaPayload? PayloadFor(IBusinessObjectView item)
        => BusinessObjectPayloadBuilder.TryParseMediaPayload(item.StateJson, out BusinessObjectMediaPayload? payload)
            ? payload
            : null;

    private static string TrimForCard(string value)
        => value.Length > 120 ? $"{value[..120]}..." : value;
}
