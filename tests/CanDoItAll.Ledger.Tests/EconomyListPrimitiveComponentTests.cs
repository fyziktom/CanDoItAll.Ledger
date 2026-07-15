using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyListPrimitiveComponentTests : TestContext
{
    public EconomyListPrimitiveComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void BusinessObjectList_uses_actionable_selection_items_and_preserves_selection_callback()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IBusinessObjectView item = workspace.BusinessObjects[0];
        IBusinessObjectView? selected = null;
        IRenderedComponent<BusinessObjectList> component = RenderComponent<BusinessObjectList>(parameters => parameters
            .Add(value => value.Items, [item])
            .Add(value => value.SelectedObjectId, item.ObjectId)
            .Add(value => value.SelectedObjectChanged, value => selected = value));

        component.Find(".cda-selection-list-item__button").Click();

        Assert.Same(item, selected);
        Assert.Contains(item.ObjectType, component.Markup, StringComparison.Ordinal);
        Assert.Contains($"schema {item.SchemaVersion}", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SubAccountList_uses_passive_surface_cards_and_preserves_create_callback()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        bool createRequested = false;
        IRenderedComponent<SubAccountList> component = RenderComponent<SubAccountList>(parameters => parameters
            .Add(value => value.Items, workspace.SubAccounts)
            .Add(value => value.CreateRequested, () => createRequested = true));

        component.FindAll("button")
            .Single(button => button.TextContent.Contains("New", StringComparison.Ordinal))
            .Click();

        Assert.True(createRequested);
        Assert.Equal(workspace.SubAccounts.Count, component.FindAll(".cda-surface-card--soft").Count);
        Assert.Contains("BO", component.Markup, StringComparison.Ordinal);
        Assert.Contains("UTXO", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-selection-list-item", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TransactionHistoryList_uses_passive_surface_cards_and_semantic_status_badges()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        LedgerTransactionSummary transaction = workspace.Transactions[0];
        IRenderedComponent<TransactionHistoryList> component = RenderComponent<TransactionHistoryList>(parameters => parameters
            .Add(value => value.Items, [transaction]));

        Assert.Single(component.FindAll(".cda-surface-card--soft"));
        Assert.Contains(transaction.Kind.ToString(), component.Markup, StringComparison.Ordinal);
        Assert.Contains(transaction.Status.ToString(), component.Find(".cda-status-badge").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-selection-list-item", component.Markup, StringComparison.Ordinal);
    }
}
