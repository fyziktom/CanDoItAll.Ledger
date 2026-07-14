using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public interface IBusinessObjectLineageKeyFactory
{
    BusinessObjectLineageKey Create();
}

public sealed class RandomBusinessObjectLineageKeyFactory : IBusinessObjectLineageKeyFactory
{
    public static RandomBusinessObjectLineageKeyFactory Instance { get; } = new();

    private RandomBusinessObjectLineageKeyFactory()
    {
    }

    public BusinessObjectLineageKey Create()
        => new($"lineage:{Guid.NewGuid():N}");
}
