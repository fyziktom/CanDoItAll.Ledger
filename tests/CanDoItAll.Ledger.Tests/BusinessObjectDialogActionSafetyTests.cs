using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectDialogActionSafetyTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("bo-dialog-action-safety"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("bo-dialog-action-safety-alice"));

    public BusinessObjectDialogActionSafetyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddLedgerSsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void ObjectActions_DisableUnimplementedAndUnboundCommands()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject)
                .Add(x => x.Workspace, workspace));

        ClickTab(component, "Actions");

        Assert.True(component.Find("[data-testid='cda-bo-upload-data-item-button']").HasAttribute("disabled"));
        Assert.True(component.Find("[data-testid='cda-bo-send-button']").HasAttribute("disabled"));
        Assert.True(component.Find("[data-testid='cda-bo-set-sale-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ObjectActions_EnableAndInvokeBoundCallbacks()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);
        var sentObjects = new List<IBusinessObjectView>();
        var saleObjects = new List<IBusinessObjectView>();

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.SendRequested,
                    EventCallback.Factory.Create<IBusinessObjectView>(this, item => sentObjects.Add(item)))
                .Add(
                    x => x.SetPriceRequested,
                    EventCallback.Factory.Create<IBusinessObjectView>(this, item => saleObjects.Add(item))));

        ClickTab(component, "Actions");

        IElement sendButton = component.Find("[data-testid='cda-bo-send-button']");
        IElement setSaleButton = component.Find("[data-testid='cda-bo-set-sale-button']");
        Assert.False(sendButton.HasAttribute("disabled"));
        Assert.False(setSaleButton.HasAttribute("disabled"));

        await sendButton.ClickAsync(new MouseEventArgs());
        await setSaleButton.ClickAsync(new MouseEventArgs());

        Assert.Same(businessObject, Assert.Single(sentObjects));
        Assert.Same(businessObject, Assert.Single(saleObjects));
        Assert.True(component.Find("[data-testid='cda-bo-upload-data-item-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task TicketUse_AcceptedResultUpdatesVisibleStateAndPreventsResubmission()
    {
        IBusinessObjectView ticket = CreateTicket(used: false);
        int requestCount = 0;

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, ticket)
                .Add(
                    x => x.TicketUseRequested,
                    _ =>
                    {
                        requestCount++;
                        return Task.FromResult(new LedgerTransactionExecutionResult(
                            BusinessObjectTransactionMode.UpdateObject,
                            Accepted: true,
                            new TxId("tx-ticket-used-action-safety"),
                            LedgerSequence: 42,
                            "Ticket use accepted."));
                    }));

        ClickTab(component, "Ticket");

        IRenderedComponent<Button> useButton = FindButton(component, "Use Ticket");
        Assert.False(useButton.Instance.Disabled);
        Assert.Equal("false", FindStat(component, "used").Value);

        await component.InvokeAsync(() => useButton.Instance.Click.InvokeAsync());

        Assert.Equal(1, requestCount);
        Assert.Equal("true", FindStat(component, "used").Value);
        Assert.NotEqual("none", FindStat(component, "used at").Value);

        useButton = FindButton(component, "Use Ticket");
        Assert.True(useButton.Instance.Disabled);

        // Invoke the callback parameter directly to prove the component guard also blocks
        // duplicate submissions when an event races with the disabled-state render.
        await component.InvokeAsync(() => useButton.Instance.Click.InvokeAsync());
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void TicketActions_DisableWhenNoHandlerIsBound()
    {
        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, CreateTicket(used: false)));

        ClickTab(component, "Ticket");

        Assert.True(FindButton(component, "Sign Ownership").Instance.Disabled);
        Assert.True(FindButton(component, "Use Ticket").Instance.Disabled);
    }

    private static CompactStat FindStat(
        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component,
        string label)
        => component.FindComponents<CompactStat>()
            .Single(stat => string.Equals(stat.Instance.Label, label, StringComparison.Ordinal))
            .Instance;

    private static IRenderedComponent<Button> FindButton(
        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component,
        string text)
        => component.FindComponents<Button>()
            .Single(button => string.Equals(button.Instance.Text, text, StringComparison.Ordinal));

    private static void ClickTab(
        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component,
        string text)
    {
        component.FindAll("[role='tab']")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();
    }

    private static IBusinessObjectView CreateTicket(bool used)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault("ticket");
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            "Action safety ticket",
            "Ticket action regression proof.",
            "Ticket action regression proof.",
            "#ticket",
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventId"] = "bo:event:action-safety",
                ["ticketClass"] = "General",
                ["ticketDuration"] = "Day",
                ["used"] = used ? "true" : "false"
            });

        return new GenericBusinessObjectView(
            Context,
            new BusinessObjectId("bo:ticket:action-safety"),
            "ticket",
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('a', 64)),
            new OutPoint(new TxId("tx-ticket-action-safety"), 0),
            BusinessObjectPayloadBuilder.Serialize(payload),
            "bo-ticket://action-safety",
            TransactionDataItemType.BusinessObject,
            DateTimeOffset.Parse("2026-07-16T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-16T12:00:00Z"));
    }
}
