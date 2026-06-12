using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Models;

public sealed record BusinessObjectCreationWizardModel(
    string ObjectType,
    int Step,
    string Name,
    string Tags,
    string Description,
    string Text,
    IReadOnlyList<BusinessObjectDataItem> DataItems,
    BusinessObjectDataItem? LastUploadedItem);

public sealed record BusinessObjectGalleryItem(
    IMediaBusinessObjectView Object,
    string PreviewUrl,
    bool IsOwnObject,
    bool IsOffered,
    string Accent);
