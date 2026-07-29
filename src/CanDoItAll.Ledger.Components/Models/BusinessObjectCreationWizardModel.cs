using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Services;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record BusinessObjectCreationWizardModel(
    string ObjectType,
    int Step,
    string Name,
    string Tags,
    string Description,
    string Text,
    IReadOnlyList<BusinessObjectDataItem> DataItems,
    BusinessObjectDataItem? LastUploadedItem,
    BusinessObjectEditorModel? EditSource = null)
{
    public bool IsEdit => EditSource is not null;

    public static BusinessObjectCreationWizardModel ForEdit(IBusinessObjectView source, int step = 2)
    {
        BusinessObjectMediaPayload? payload = PayloadFor(source);
        IReadOnlyList<BusinessObjectDataItem> dataItems = DataItemsFor(source, payload);
        BusinessObjectDataItem? primaryItem = dataItems
            .OrderByDescending(item => item.IsMain)
            .FirstOrDefault() ?? dataItems.FirstOrDefault();

        return new BusinessObjectCreationWizardModel(
            source.ObjectType,
            Math.Clamp(step, 1, 3),
            DisplayNameFor(source, payload),
            TagsFor(source, payload),
            DescriptionFor(source, payload),
            TextFor(source, payload),
            dataItems,
            primaryItem,
            new BusinessObjectEditorModel(
                source.ObjectId,
                source.ObjectType,
                string.IsNullOrWhiteSpace(source.SchemaVersion) ? "1.0" : source.SchemaVersion,
                source.OwnerAddress,
                source.StateJson ?? "{}",
                source.ViewedAt));
    }

    private static BusinessObjectMediaPayload? PayloadFor(IBusinessObjectView source)
        => BusinessObjectPayloadBuilder.TryParseMediaPayload(source.StateJson, out BusinessObjectMediaPayload? payload)
            ? payload
            : null;

    private static IReadOnlyList<BusinessObjectDataItem> DataItemsFor(
        IBusinessObjectView source,
        BusinessObjectMediaPayload? payload)
        => source is IMediaBusinessObjectView media
            ? media.DataItems
            : payload?.DataItems ?? [];

    private static string DisplayNameFor(IBusinessObjectView source, BusinessObjectMediaPayload? payload)
        => source is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Name)
            ? media.Name
            : !string.IsNullOrWhiteSpace(payload?.Name)
                ? payload!.Name
                : "Untitled";

    private static string DescriptionFor(IBusinessObjectView source, BusinessObjectMediaPayload? payload)
        => source is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Description)
            ? media.Description
            : payload?.Description ?? string.Empty;

    private static string TextFor(IBusinessObjectView source, BusinessObjectMediaPayload? payload)
        => source is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Text)
            ? media.Text
            : payload?.Text ?? string.Empty;

    private static string TagsFor(IBusinessObjectView source, BusinessObjectMediaPayload? payload)
    {
        IEnumerable<string> tags = source is IMediaBusinessObjectView media
            ? media.Tags
            : payload?.TagList ?? [];

        return string.Join(' ', tags
            .Select(tag => AccountWorkspaceLocalServiceHelpers.NormalizeTag(tag))
            .Where(tag => tag is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .Select(tag => $"#{tag}"));
    }
}
