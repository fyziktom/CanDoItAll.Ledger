using AngleSharp.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectEditUpdateComponentTests : BunitContext
{
    public BusinessObjectEditUpdateComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddLedgerSsrAccountWorkspaceAdapters();
    }

    [Fact]
    public async Task EditWizard_PrefillsSelectedBusinessObjectAndStagesUpdateDraft()
    {
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);
        string originalName = DisplayName(businessObject);
        BusinessObjectCreationWizardModel model = BusinessObjectCreationWizardModel.ForEdit(businessObject, step: 2);

        IRenderedComponent<BusinessObjectCreationWizard> component =
            Render<BusinessObjectCreationWizard>(parameters => parameters
                .Add(x => x.Model, model)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.StageBusinessObjectRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(
                        this,
                        draft =>
                        {
                            stagedDrafts.Add(draft);
                            return Task.CompletedTask;
                        })));

        IElement nameInput = component.Find("input[aria-label='BusinessObject name']");
        Assert.Equal(originalName, nameInput.GetAttribute("value"));

        nameInput.Input("Updated BO Name");
        await ClickButtonAsync(component, "Next");
        Assert.True(businessObject.ObjectId.IsCanonicalOutPointId());
        string sourceSummary = component.Find("[data-testid='bo-edit-source']").TextContent;
        Assert.Contains(businessObject.ObjectId.Value[..14], sourceSummary, StringComparison.Ordinal);
        Assert.Contains(businessObject.ObjectId.Value[^6..], sourceSummary, StringComparison.Ordinal);

        await ClickButtonAsync(component, "Stage BO Update");

        BusinessObjectTransactionDraft draft = Assert.Single(stagedDrafts);
        Assert.Equal(BusinessObjectTransactionMode.UpdateObject, draft.SelectedMode);
        Assert.Equal(businessObject.ObjectId, draft.ObjectId);
        Assert.Equal(businessObject.ObjectId, draft.SourceObjectId);
        Assert.Equal(businessObject.OwnerAddress, draft.OwnerAddress);
        Assert.Equal(businessObject.OwnerAddress, draft.ReceiverAddress);
        Assert.Equal(businessObject.OwnerAddress, draft.PaymentReceiverAddress);
        Assert.Equal(Amount.Zero, draft.MainAmount);
        Assert.Equal(Amount.Zero, draft.PaymentAmount);
        Assert.StartsWith("bo-edit://", draft.ExternalRef, StringComparison.Ordinal);

        BusinessObjectMediaPayload payload = BusinessObjectPayloadBuilder.ParseMediaPayload(draft.StateJson, "fallback");
        Assert.Equal("Updated BO Name", payload.Name);
        Assert.NotEqual(businessObject.StateJson, draft.StateJson);
    }

    [Fact]
    public void DetailsActions_RenderEditActionWithoutOwnershipVerifierScanner()
    {
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.EditRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(this, _ => Task.CompletedTask)));

        ClickTab(component, "Actions");

        IElement editButton = component.Find("[data-testid='cda-bo-edit-button']");
        Assert.Contains("Edit", editButton.TextContent, StringComparison.Ordinal);
        Assert.False(editButton.HasAttribute("disabled"));
        Assert.NotEmpty(component.FindAll("[data-testid='cda-bo-ownership-qr-button']"));
        Assert.DoesNotContain("Verify ownership", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetailsActions_RecycleButtonEmitsCloseDraft()
    {
        var stagedDrafts = new List<BusinessObjectTransactionDraft>();
        BusinessObjectWorkspaceModel workspace = LedgerComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            Render<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject)
                .Add(x => x.Workspace, workspace)
                .Add(
                    x => x.EditRequested,
                    EventCallback.Factory.Create<BusinessObjectTransactionDraft>(
                        this,
                        draft =>
                        {
                            stagedDrafts.Add(draft);
                            return Task.CompletedTask;
                        })));

        ClickTab(component, "Actions");
        await component.Find("[data-testid='cda-bo-recycle-button']").ClickAsync(new MouseEventArgs());

        BusinessObjectTransactionDraft draft = Assert.Single(stagedDrafts);
        Assert.Equal(BusinessObjectTransactionMode.CloseObject, draft.SelectedMode);
        Assert.Equal(businessObject.ObjectId, draft.ObjectId);
        Assert.Equal(businessObject.ObjectId, draft.SourceObjectId);
        Assert.Equal(businessObject.ObjectType, draft.ObjectType);
        Assert.Equal(businessObject.OwnerAddress, draft.OwnerAddress);
        Assert.Equal(businessObject.OwnerAddress, draft.ReceiverAddress);
        Assert.Equal(Amount.Zero, draft.MainAmount);
        Assert.Equal(Amount.Zero, draft.PaymentAmount);
        Assert.StartsWith("bo-recycle://", draft.ExternalRef, StringComparison.Ordinal);
    }

    private static Task ClickButtonAsync<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
        => component.FindAll("button")
            .First(button => button.TextContent.Contains(text, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

    private static void ClickTab<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
    {
        component.FindAll("[role='tab']")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();
    }

    private static string DisplayName(IBusinessObjectView item)
    {
        if (item is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.Name))
        {
            return media.Name;
        }

        return BusinessObjectPayloadBuilder.TryParseMediaPayload(item.StateJson, out BusinessObjectMediaPayload? payload)
            && !string.IsNullOrWhiteSpace(payload?.Name)
                ? payload.Name
                : item.ObjectId.Value;
    }
}
