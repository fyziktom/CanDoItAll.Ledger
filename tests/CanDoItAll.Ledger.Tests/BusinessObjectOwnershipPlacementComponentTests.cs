using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Components.Services;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectOwnershipPlacementComponentTests : TestContext
{
    public BusinessObjectOwnershipPlacementComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
        Services.AddEconomySsrAccountWorkspaceAdapters();
    }

    [Fact]
    public void DetailsActions_KeepOwnerQrButDoNotExposeOwnershipVerifierScanner()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item is IMediaBusinessObjectView);

        IRenderedComponent<BusinessObjectLedgerDetailsDialogContent> component =
            RenderComponent<BusinessObjectLedgerDetailsDialogContent>(parameters => parameters
                .Add(x => x.Object, businessObject)
                .Add(x => x.ViewedAtUtc, workspace.ViewedAtUtc));

        ClickTab(component, "Actions");

        Assert.NotEmpty(component.FindAll("[data-testid='cda-bo-ownership-qr-button']"));
        Assert.DoesNotContain("Verify ownership", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessObjectVerificationDialogContent", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeSecuritySurface_StillHostsOwnershipVerificationTool()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));
        IRenderedComponent<MessageSecurityPanel> security =
            RenderComponent<MessageSecurityPanel>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        ClickTab(security, "Ownership");

        Assert.NotEmpty(home.FindAll("[data-testid='cda-action-security']"));
        Assert.Contains("Ownership payload", security.Markup, StringComparison.Ordinal);
        Assert.Contains("Verify ownership", security.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeBalances_ShowOnlyRealTokenSourcesInsideBalancesCard()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        Assert.Contains("Balances", home.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Token Sources<", home.Markup, StringComparison.Ordinal);
        Assert.Contains("tx-token-source", home.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-permission-source", home.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-rule-permission-source", home.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("tx-rule-cashpool", home.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeActionGroup_RendersVisibleShortLabels()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        AssertActionLabel(home, "cda-action-send", "Send");
        AssertActionLabel(home, "cda-action-token", "Token");
        AssertActionLabel(home, "cda-action-address-directory", "Addresses");
        AssertActionLabel(home, "cda-action-security", "Security");
        AssertActionLabel(home, "cda-action-settings", "Settings");
        AssertActionLabel(home, "cda-action-settings-backup", "Backup");
        AssertActionLabel(home, "cda-action-refresh", "Refresh");
        AssertActionLabel(home, "cda-action-subaccount", "Subaccount");
    }

    private static void ClickTab<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
    {
        component.FindAll("[role='tab']")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();
    }

    private static void AssertActionLabel(
        IRenderedComponent<EconomyAccountWorkspace> home,
        string testId,
        string expectedLabel)
    {
        string label = home.Find($"[data-testid='{testId}'] .rz-button-text").TextContent.Trim();
        Assert.Equal(expectedLabel, label);
    }
}
