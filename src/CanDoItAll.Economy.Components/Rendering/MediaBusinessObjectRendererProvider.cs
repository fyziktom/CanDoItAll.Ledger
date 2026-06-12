using CanDoItAll.Economy.BusinessObjects;

namespace CanDoItAll.Economy.Components.Rendering;

public sealed class MediaBusinessObjectRendererProvider : IBusinessObjectRendererProvider
{
    public int Priority => 90;

    public bool CanRender(IBusinessObjectView businessObject)
        => businessObject is IMediaBusinessObjectView;

    public BusinessObjectRendererDescriptor CreateDescriptor(IBusinessObjectView businessObject)
        => new(
            "media-dataitems",
            "Media DataItems",
            typeof(MediaBusinessObjectRenderer),
            Priority);
}
