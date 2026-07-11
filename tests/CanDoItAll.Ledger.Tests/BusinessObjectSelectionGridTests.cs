using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectSelectionGridTests : TestContext
{
    private static readonly Address Owner = Address.FromPublicKey([1, 2, 3, 4]);

    public BusinessObjectSelectionGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Compatible_unselected_card_is_disabled_with_a_reason_at_the_selection_limit()
    {
        IBusinessObjectView first = CreateBusinessObject("bo:order:1");
        IBusinessObjectView second = CreateBusinessObject("bo:order:2");
        IBusinessObjectView third = CreateBusinessObject("bo:order:3");
        IRenderedComponent<BusinessObjectSelectionGrid> component = RenderComponent<BusinessObjectSelectionGrid>(parameters => parameters
            .Add(value => value.Items, new[] { first, second, third })
            .Add(value => value.Selected, new[] { first, second })
            .Add(value => value.MaximumSelections, 2)
            .Add(value => value.RequireCompatibleSelection, true));

        IElement selectedButton = component.Find("[data-testid='bo-select-bo-order-1'] button");
        IElement unavailableCard = component.Find("[data-testid='bo-select-bo-order-3']");
        IElement unavailableButton = unavailableCard.QuerySelector("button")!;

        Assert.False(selectedButton.HasAttribute("disabled"));
        Assert.Equal("true", selectedButton.GetAttribute("aria-pressed"));
        Assert.True(unavailableButton.HasAttribute("disabled"));
        Assert.Equal("false", unavailableButton.GetAttribute("aria-pressed"));
        Assert.Contains("Selection limit reached (2)", unavailableCard.TextContent, StringComparison.Ordinal);
        Assert.Equal(
            unavailableCard.QuerySelector(".cda-transaction-bo-picker-card__restriction")!.Id,
            unavailableButton.GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Selection_grid_uses_group_and_toggle_button_semantics()
    {
        IBusinessObjectView selected = CreateBusinessObject("bo:order:1");
        IBusinessObjectView available = CreateBusinessObject("bo:order:2");
        IRenderedComponent<BusinessObjectSelectionGrid> component = RenderComponent<BusinessObjectSelectionGrid>(parameters => parameters
            .Add(value => value.Items, new[] { selected, available })
            .Add(value => value.Selected, new[] { selected })
            .Add(value => value.MaximumSelections, 2));

        IElement selectionGroup = component.Find(".cda-transaction-bo-grid");
        IElement selectedButton = component.Find("[data-testid='bo-select-bo-order-1'] button");
        IElement availableButton = component.Find("[data-testid='bo-select-bo-order-2'] button");

        Assert.Equal("group", selectionGroup.GetAttribute("role"));
        Assert.Null(selectionGroup.GetAttribute("aria-multiselectable"));
        Assert.Equal("true", selectedButton.GetAttribute("aria-pressed"));
        Assert.StartsWith("Deselect ", selectedButton.GetAttribute("aria-label"), StringComparison.Ordinal);
        Assert.Equal("false", availableButton.GetAttribute("aria-pressed"));
        Assert.StartsWith("Select ", availableButton.GetAttribute("aria-label"), StringComparison.Ordinal);
    }

    [Fact]
    public void Tag_filters_expose_their_active_state_as_toggle_buttons()
    {
        IRenderedComponent<BusinessObjectSelectionGrid> component = RenderComponent<BusinessObjectSelectionGrid>(parameters => parameters
            .Add(value => value.Items, new[] { CreateBusinessObject("bo:order:1") }));

        IElement allTags = component.Find("[data-testid='bo-tag-all']");
        IElement purchaseTag = component.Find("[data-testid='bo-tag-purchase']");

        Assert.Equal("group", allTags.ParentElement!.GetAttribute("role"));
        Assert.Equal("true", allTags.GetAttribute("aria-pressed"));
        Assert.Equal("false", purchaseTag.GetAttribute("aria-pressed"));

        purchaseTag.Click();

        Assert.Equal("false", allTags.GetAttribute("aria-pressed"));
        Assert.Equal("true", purchaseTag.GetAttribute("aria-pressed"));
        Assert.True(purchaseTag.ClassList.Contains("is-active"));
    }

    private static IBusinessObjectView CreateBusinessObject(string objectId)
        => new GenericBusinessObjectView(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new BusinessObjectId(objectId),
            "purchase-order",
            "1.0",
            1,
            Owner,
            BusinessObjectOperation.Create,
            new HashHex($"hash-{objectId}"),
            new OutPoint(new TxId($"tx-{objectId}"), 0),
            "{}",
            null,
            TransactionDataItemType.BusinessObject,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
