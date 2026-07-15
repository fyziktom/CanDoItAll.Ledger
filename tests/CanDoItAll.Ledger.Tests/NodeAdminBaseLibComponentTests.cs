using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Economy.Tests;

public sealed class NodeAdminBaseLibComponentTests : TestContext
{
    public NodeAdminBaseLibComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public async Task Dashboard_uses_BaseLib_cards_summaries_and_actionable_transaction_rows()
    {
        IRenderedComponent<NodeAdminDashboard> component = RenderComponent<NodeAdminDashboard>();

        Assert.Single(component.FindAll("[data-testid='cda-node-admin-dashboard']"));
        Assert.NotEmpty(component.FindComponents<SummaryTiles>());
        Assert.DoesNotContain("cda-node-console__panel", component.Markup, StringComparison.Ordinal);

        await component.InvokeAsync(() => Tab(component, "Addresses").Click());
        component.WaitForAssertion(() =>
        {
            Assert.True(component.FindComponents<SurfaceCard>().Count >= 5);
            Assert.DoesNotContain("cda-node-address-card", component.Markup, StringComparison.Ordinal);
        });

        await component.InvokeAsync(() => Tab(component, "Transactions").Click());
        component.WaitForAssertion(() =>
        {
            Assert.NotEmpty(component.FindComponents<SelectionListItem>());
            Assert.Contains("Confirmed", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Address_details_use_BaseLib_summary_and_selection_primitives()
    {
        NodeAdminAddressSummary address = new(
            "Operations primary",
            "cdia1NZW6356kk5ecYvmz1AhdsRwCquyeBz8ZbK",
            "Account",
            "126500 main",
            7,
            18,
            "Active",
            new DateTimeOffset(2026, 5, 6, 14, 45, 0, TimeSpan.Zero));
        NodeAdminTransactionSummary transaction = new(
            "tx-node-admin-proof",
            "Operations primary → Treasury",
            TransactionKind.Transfer,
            TransactionStatus.Confirmed,
            1,
            1,
            address.Address,
            "Transfer",
            address.LastSeenUtc);
        NodeAdminTransactionSummary? selected = null;

        IRenderedComponent<NodeAddressDetailsDialogContent> component = RenderComponent<NodeAddressDetailsDialogContent>(parameters => parameters
            .Add(value => value.Address, address)
            .Add(value => value.Transactions, [transaction])
            .Add(value => value.TransactionSelected, value => selected = value));

        Assert.NotEmpty(component.FindComponents<SummaryTiles>());
        Assert.Single(component.FindComponents<SelectionListItem>());
        component.Find(".cda-selection-list-item__button").Click();
        Assert.Same(transaction, selected);
        Assert.DoesNotContain("cda-node-address-hero", component.Markup, StringComparison.Ordinal);
    }

    private static AngleSharp.Dom.IElement Tab(IRenderedComponent<NodeAdminDashboard> component, string text)
        => component.FindAll("[role='tab']")
            .Single(tab => tab.TextContent.Contains(text, StringComparison.Ordinal));
}
