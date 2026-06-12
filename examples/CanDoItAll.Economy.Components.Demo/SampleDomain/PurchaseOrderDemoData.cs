using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Demo.SampleDomain;

public static class PurchaseOrderDemoData
{
    public static BusinessObjectWorkspaceModel CreateWorkspace()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IBusinessObjectView source = workspace.BusinessObjects[0];

        var purchaseOrder = new PurchaseOrderBusinessObjectView(
            source.Context,
            source.ObjectId,
            source.ObjectType,
            source.SchemaVersion,
            source.Version,
            source.OwnerAddress,
            source.Operation,
            source.DataHash,
            source.CurrentOutPoint,
            source.StateJson,
            "Acme Metals",
            Amount.FromInt64(42000),
            "Approved",
            workspace.ViewedAtUtc.AddDays(12),
            source.CreatedAt,
            source.ViewedAt);

        IReadOnlyList<IBusinessObjectView> objects =
        [
            purchaseOrder,
            workspace.BusinessObjects[1],
            workspace.BusinessObjects[2]
        ];

        return workspace with
        {
            BusinessObjects = objects
        };
    }
}
