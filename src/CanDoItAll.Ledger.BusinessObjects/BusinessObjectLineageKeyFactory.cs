using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

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
