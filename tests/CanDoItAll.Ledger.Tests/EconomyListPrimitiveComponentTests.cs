using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;

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
    public void TransactionHistoryList_uses_dense_latest_ten_grid_with_directory_names_and_scan_fields()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        LedgerTransactionSummary template = workspace.Transactions
            .Single(transaction => transaction.Kind == TransactionKind.Transfer);
        IReadOnlyList<LedgerTransactionSummary> transactions = Enumerable.Range(1, 12)
            .Select(index => template with
            {
                TxId = new TxId($"tx-history-{index:00}"),
                Timestamp = template.Timestamp.AddMinutes(index),
                LedgerSequence = index
            })
            .ToArray();
        EconomyAddressBookmark warehouseBookmark = workspace.AddressBookmarks
            .Single(bookmark => bookmark.Name == "Warehouse north");
        var savedDirectoryEntry = new AddressDirectoryEntry(
            warehouseBookmark.Address.Value,
            "North warehouse desk",
            AddressDirectoryEntryKind.External,
            Favorite: true);

        IRenderedComponent<TransactionHistoryList> component = RenderComponent<TransactionHistoryList>(parameters => parameters
            .Add(value => value.Items, transactions)
            .Add(value => value.AccountAddress, workspace.Address)
            .Add(value => value.AccountName, workspace.AccountName)
            .Add(value => value.Bookmarks, workspace.AddressBookmarks)
            .Add(value => value.DirectoryEntries, [savedDirectoryEntry]));

        string[] headers = component.FindAll("[data-testid='cda-recent-transaction-grid'] th")
            .Select(header => header.TextContent.Trim())
            .ToArray();
        IReadOnlyList<AngleSharp.Dom.IElement> rows = component.FindAll("[data-testid='cda-recent-transaction-row']");
        IReadOnlyList<AngleSharp.Dom.IElement> tableRows = component.FindAll("[data-testid='cda-recent-transaction-grid'] tbody tr");

        Assert.Equal(
            ["#", "Date & time", "Type", "Status", "Direction", "Flow", "Counterparty", "Transaction", "Actions"],
            headers);
        Assert.Equal(10, rows.Count);
        Assert.Contains("North warehouse desk", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Warehouse north<", component.Markup, StringComparison.Ordinal);
        Assert.Contains(warehouseBookmark.Address.Value, component.Markup, StringComparison.Ordinal);
        Assert.Contains("Outgoing", component.Markup, StringComparison.Ordinal);
        Assert.Contains("1 → 1", component.Markup, StringComparison.Ordinal);
        Assert.Contains("2026-", component.Markup, StringComparison.Ordinal);
        Assert.Equal("12", tableRows[0].QuerySelector("td")?.TextContent.Trim());
        Assert.Equal("3", tableRows[^1].QuerySelector("td")?.TextContent.Trim());
        Assert.DoesNotContain("cda-surface-card", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-selection-list-item", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransactionHistoryList_preserves_keyboard_and_details_activation()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        LedgerTransactionSummary transaction = workspace.Transactions[0];
        var requested = new List<LedgerTransactionSummary>();
        IRenderedComponent<TransactionHistoryList> component = RenderComponent<TransactionHistoryList>(parameters => parameters
            .Add(value => value.Items, [transaction])
            .Add(value => value.AccountAddress, workspace.Address)
            .Add(value => value.AccountName, workspace.AccountName)
            .Add(value => value.Bookmarks, workspace.AddressBookmarks)
            .Add(value => value.TransactionDetailsRequested, item => requested.Add(item)));

        await component.Find("[data-testid='cda-recent-transaction-grid'] tbody tr")
            .KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Assert.Equal([transaction], requested);

        requested.Clear();
        await component.FindAll("button")
            .Single(button => button.TextContent.Contains("Details", StringComparison.Ordinal))
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Equal([transaction], requested);
    }
}
