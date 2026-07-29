using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Demo.SampleDomain;

public static class PurchaseOrderDemoData
{
    public static BusinessObjectWorkspaceModel CreateWorkspace()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
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
