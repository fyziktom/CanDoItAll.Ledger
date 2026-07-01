using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectTicketMintingComponentTests : TestContext
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("ticket-test"));
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-ticket-public"));
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-01T10:00:00Z");

    public BusinessObjectTicketMintingComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
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
            RenderComponent<BusinessObjectEventPickerDialogContent>(parameters => parameters
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
        BusinessObjectWorkspaceModel workspace = CreateWorkspace(eventObject);
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
            RenderComponent<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace));

        Assert.Contains("No event selected", component.Find("[data-testid='cda-ticket-event-panel']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Select Event", component.Markup, StringComparison.Ordinal);
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
        var objectId = new BusinessObjectId($"bo:{objectType}:{BusinessObjectCreateIdFactory.NormalizeName(name)}");

        return new GenericTypedBusinessObjectView(
            Context,
            objectId,
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex(new string('7', 64)),
            new OutPoint(new TxId($"tx-{objectType}-{BusinessObjectCreateIdFactory.NormalizeName(name)}"), 0),
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

    private static BusinessObjectWorkspaceModel CreateWorkspace(params IBusinessObjectView[] businessObjects)
    {
        LedgerAccountBalance balance = new(
            Context,
            Alice,
            Amount.FromInt64(1000),
            new Dictionary<TokenId, Amount>(),
            businessObjects.Length,
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
            [],
            [],
            [],
            businessObjects,
            [],
            [],
            draft,
            editor,
            Now);
    }
}
