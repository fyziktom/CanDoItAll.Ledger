using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectTicketMintingComponentTests : BunitContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("ticket-test"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-ticket-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-ticket-public"));
    private static readonly Address SubAccount = Address.FromPublicKey(Hex.Utf8("ticket-subaccount-public"));
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-01T10:00:00Z");

    public BusinessObjectTicketMintingComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void TicketEventPrefill_MapsEventIdentityAndVenueFields()
    {
        ITypedBusinessObjectView eventObject = CreateEvent("Ledger Summit");

        BusinessObjectTicketEventPrefill prefill = BusinessObjectTicketEventPrefill.FromEvent(eventObject);

        Assert.Equal("Ledger Summit", prefill.EventName);
        Assert.Equal(eventObject.ObjectId.Value, prefill.Fields["eventId"]);
        Assert.Equal(Alice.Value, prefill.Fields["eventAddress"]);
        Assert.Equal("2026-08-10T18:30:00Z", prefill.Fields["eventDate"]);
        Assert.Equal("Main Hall", prefill.Fields["location"]);
        Assert.Equal("50.087,14.421", prefill.Fields["locationCoordinates"]);
        Assert.Equal("https://events.example/ledger-summit", prefill.Fields["authorLink"]);
        Assert.Equal("false", prefill.Fields["used"]);
        Assert.Equal("FreeToUse", prefill.Fields["status"]);
    }

    [Fact]
    public void EventPicker_RendersOnlyAvailableEvents()
    {
        ITypedBusinessObjectView eventObject = CreateEvent("Ledger Summit");
        ITypedBusinessObjectView postObject = CreateTypedObject(
            "post",
            "Ledger Post",
            new Dictionary<string, string>(StringComparer.Ordinal));

        IRenderedComponent<BusinessObjectEventPickerDialogContent> component =
            Render<BusinessObjectEventPickerDialogContent>(parameters => parameters
                .Add(x => x.Events, [eventObject, postObject]));

        Assert.Single(component.FindAll("[data-testid='cda-event-picker-event']"));
        Assert.Contains("Ledger Summit", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Main Hall", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Ledger Post", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TicketWizard_ShowsEventPickerPanelOnlyForTickets()
    {
        ITypedBusinessObjectView eventObject = CreateEvent("Ledger Summit");
        BusinessObjectWorkspaceModel workspace = CreateWorkspace([eventObject]);
        BusinessObjectCreationWizardModel model = new(
            "ticket",
            Step: 2,
            "New ticket",
            "#ticket",
            "Ticket, access pass, service case, or approval ticket.",
            string.Empty,
            [],
            null);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace));

        Assert.Contains("No event selected", component.Find("[data-testid='cda-ticket-event-panel']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Select Event", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EventWizard_RendersDomainControlsAndStagesMintAuthorAddress()
    {
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        BusinessObjectWorkspaceModel workspace = CreateWorkspace();
        BusinessObjectCreationWizardModel model = new(
            "event",
            Step: 2,
            "Ledger Summit",
            "#event",
            "Event, milestone, meeting, or operational occurrence.",
            string.Empty,
            [],
            null);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.StageBusinessObjectRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(
                        this,
                        request =>
                        {
                            stagedDrafts.Add(request);
                            return Task.CompletedTask;
                        })));

        Assert.Single(component.FindAll("[data-testid='cda-bo-field-eventDate-datetime']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-field-eventClass-dropdown']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-field-mintAuthorAddress-auto']"));
        Assert.Contains("Conference", component.Markup, StringComparison.Ordinal);

        await ClickButtonAsync(component, "Next");
        await ClickButtonAsync(component, "Stage BO Transaction");

        BusinessObjectTransactionDraft draft = Assert.Single(stagedDrafts);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "event");
        Assert.Equal("event", draft.ObjectType);
        Assert.Equal(Alice.Value, payload.Fields?["mintAuthorAddress"]);
        Assert.Equal("Conference", payload.Fields?["eventClass"]);
        Assert.Equal("Scheduled", payload.Fields?["status"]);
        Assert.Equal("2026-07-01T10:00:00Z", payload.Fields?["eventDate"]);
    }

    [Fact]
    public async Task TicketWizard_RendersDomainControlsAndStagesMintAuthorAddress()
    {
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        BusinessObjectWorkspaceModel workspace = CreateWorkspace([CreateEvent("Ledger Summit")]);
        BusinessObjectCreationWizardModel model = CreateTicketWizardModel(step: 2);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.StageBusinessObjectRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(
                        this,
                        request =>
                        {
                            stagedDrafts.Add(request);
                            return Task.CompletedTask;
                        })));

        Assert.Single(component.FindAll("[data-testid='cda-bo-field-eventDate-datetime']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-field-ticketClass-dropdown']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-field-ticketDuration-dropdown']"));
        Assert.Single(component.FindAll("[data-testid='cda-bo-field-mintAuthorAddress-auto']"));
        Assert.Contains("Free to use", component.Markup, StringComparison.Ordinal);

        await ClickButtonAsync(component, "Next");
        await ClickButtonAsync(component, "Stage BO Transaction");

        BusinessObjectTransactionDraft draft = Assert.Single(stagedDrafts);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "ticket");
        Assert.Equal("ticket", draft.ObjectType);
        Assert.Equal(Alice.Value, payload.Fields?["mintAuthorAddress"]);
        Assert.Equal("General", payload.Fields?["ticketClass"]);
        Assert.Equal("Day", payload.Fields?["ticketDuration"]);
        Assert.Equal("false", payload.Fields?["used"]);
        Assert.Equal("FreeToUse", payload.Fields?["status"]);
        Assert.Equal("2026-07-01T10:00:00Z", payload.Fields?["eventDate"]);
    }

    [Fact]
    public async Task TicketWizard_MultimintStagesDistinctDraftsToSelectedSubAccount()
    {
        var stagedBatches = new List<BusinessObjectBatchTransactionRequest>();
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(
            businessObjects: [CreateEvent("Ledger Summit")],
            subAccounts:
            [
                new BusinessObjectSubAccountSummary("Ticket desk", SubAccount, Amount.FromInt64(10), 1, 0, Now)
            ],
            bookmarks:
            [
                new EconomyAddressBookmark("Bob", Bob)
            ]);
        BusinessObjectCreationWizardModel model = CreateTicketWizardModel(step: 2);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.StageBusinessObjectBatchRequested,
                    EventCallback.Factory.Create<BusinessObjectBatchTransactionRequest>(
                        this,
                        request =>
                        {
                            stagedBatches.Add(request);
                            return Task.CompletedTask;
                        })));

        component.Find("[data-testid='cda-ticket-multimint-enabled']").Change(true);
        component.Find("[data-testid='cda-ticket-multimint-count']").Input("5");
        SelectDropDownOption(component, "cda-ticket-multimint-recipient-mode", "Subaccount");
        SelectDropDownOption(component, "cda-ticket-multimint-subaccount", "Ticket desk");
        await ClickButtonAsync(component, "Next");
        await ClickButtonAsync(component, "Stage BO Transaction");

        BusinessObjectBatchTransactionRequest batch = Assert.Single(stagedBatches);
        Assert.Equal("Ticket multimint", batch.Label);
        Assert.Equal(5, batch.Drafts.Count);
        Assert.All(batch.Drafts, draft =>
        {
            Assert.Equal(SubAccount, draft.OwnerAddress);
            Assert.Equal(SubAccount, draft.ReceiverAddress);
            Assert.Equal(SubAccount, draft.PaymentReceiverAddress);
            Assert.Equal(default, draft.ObjectId);
        });
        Assert.Equal(5, batch.Drafts.Select(draft => draft.ExternalRef).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Enumerable.Range(0, 5),
            Enumerable.Range(0, 5).Select(index => batch.CorrelationFor(index).DraftIndex));
        Assert.Equal(
            ["ticket-001", "ticket-002", "ticket-003", "ticket-004", "ticket-005"],
            batch.Drafts
                .Select(draft => BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "ticket"))
                .Select(payload => payload.Fields?["ticketNumber"] ?? string.Empty)
                .ToArray());
    }

    [Fact]
    public async Task TicketWizard_MultimintManualSingleRecipientRepeatsByCount()
    {
        var stagedBatches = new List<BusinessObjectBatchTransactionRequest>();
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(bookmarks: [new EconomyAddressBookmark("Bob", Bob)]);
        BusinessObjectCreationWizardModel model = CreateTicketWizardModel(step: 2);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.StageBusinessObjectBatchRequested,
                    EventCallback.Factory.Create<BusinessObjectBatchTransactionRequest>(
                        this,
                        request =>
                        {
                            stagedBatches.Add(request);
                            return Task.CompletedTask;
                        })));

        component.Find("[data-testid='cda-ticket-multimint-enabled']").Change(true);
        component.Find("[data-testid='cda-ticket-multimint-count']").Input("3");
        SelectDropDownOption(component, "cda-ticket-multimint-recipient-mode", "Recipient list");
        component.Find("[data-testid='cda-ticket-multimint-recipient-list']").Input(Bob.Value);
        await ClickButtonAsync(component, "Next");
        await ClickButtonAsync(component, "Stage BO Transaction");

        BusinessObjectBatchTransactionRequest batch = Assert.Single(stagedBatches);
        Assert.Equal(3, batch.Drafts.Count);
        Assert.All(batch.Drafts, draft => Assert.Equal(Bob, draft.OwnerAddress));
        Assert.All(batch.Drafts, draft => Assert.Equal(default, draft.ObjectId));
        Assert.Equal(3, batch.Drafts.Select(draft => draft.ExternalRef).Distinct(StringComparer.Ordinal).Count());
    }

    private static ITypedBusinessObjectView CreateEvent(string name)
        => CreateTypedObject(
            "event",
            name,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["startsAt"] = "2026-08-10T18:30:00Z",
                ["location"] = "Main Hall",
                ["locationCoordinates"] = "50.087,14.421",
                ["authorLink"] = "https://events.example/ledger-summit",
                ["organizer"] = "CanDoItAll"
            });

    private static ITypedBusinessObjectView CreateTypedObject(
        string objectType,
        string name,
        IReadOnlyDictionary<string, string> fields)
    {
        BusinessObjectFamilyDefinition definition = BusinessObjectFamilyCatalog.FindOrDefault(objectType);
        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.CreatePayload(
            definition,
            name,
            $"{name} description",
            $"{name} long description",
            "#test",
            [],
            fields);
        string stateJson = BusinessObjectPayloadBuilder.Serialize(payload);
        var objectId = new BusinessObjectId($"bo:{objectType}:{BusinessObjectDisplaySlug.Normalize(name)}");

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('7', 64)),
            new OutPoint(new TxId($"tx-{objectType}-{BusinessObjectDisplaySlug.Normalize(name)}"), 0),
            stateJson,
            $"ledger://{objectId.Value}",
            TransactionDataItemType.BusinessObject,
            Now,
            Now,
            payload.Name,
            payload.Description,
            payload.Text,
            payload.Author,
            payload.Link,
            payload.ImageLink,
            payload.TagList,
            payload.DataItems ?? Array.Empty<BusinessObjectDataItem>(),
            definition,
            payload.Fields ?? new Dictionary<string, string>(StringComparer.Ordinal),
            BusinessObjectPayloadBuilder.CreateFacts(definition, payload.Fields));
    }

    private static BusinessObjectCreationWizardModel CreateTicketWizardModel(int step)
        => new(
            "ticket",
            Step: step,
            "New ticket",
            "#ticket",
            "Ticket, access pass, service case, or approval ticket.",
            string.Empty,
            [],
            null);

    private static async Task ClickButtonAsync(IRenderedComponent<BusinessObjectCreationWizard> component, string text)
    {
        IElement button = component.FindAll("button")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal));
        await button.ClickAsync(new MouseEventArgs());
    }

    private static void SelectDropDownOption(
        IRenderedComponent<BusinessObjectCreationWizard> component,
        string testId,
        string optionText)
    {
        IElement select = component.Find($"[data-testid='{testId}']");
        IElement option = select.QuerySelectorAll("option")
            .Single(item => item.TextContent.Contains(optionText, StringComparison.Ordinal));
        string optionKey = Assert.IsType<string>(option.GetAttribute("value"));

        select.Change(optionKey);
    }

    private static BusinessObjectWorkspaceModel CreateWorkspace(
        IReadOnlyList<IBusinessObjectView>? businessObjects = null,
        IReadOnlyList<BusinessObjectSubAccountSummary>? subAccounts = null,
        IReadOnlyList<EconomyAddressBookmark>? bookmarks = null)
    {
        businessObjects ??= [];
        subAccounts ??= [];
        bookmarks ??= [];
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            businessObjects.Count,
            Now);
        BusinessObjectTransactionDraft draft = new(
            new BusinessObjectId("bo:ticket:draft"),
            "ticket",
            "1.0",
            Alice,
            Alice,
            Alice,
            Amount.Zero,
            Amount.Zero,
            "{}",
            string.Empty);
        BusinessObjectEditorModel editor = new(
            draft.ObjectId,
            draft.ObjectType,
            draft.SchemaVersion,
            Alice,
            "{}",
            Now);

        return new BusinessObjectWorkspaceModel(
            "Alice ticket account",
            Context,
            Alice,
            balance,
            [new(EconomyTokenOption.MainKey, "Main", null, Amount.FromInt64(1000), IsMain: true)],
            [],
            bookmarks,
            businessObjects,
            subAccounts,
            [],
            draft,
            editor,
            Now);
    }
}
