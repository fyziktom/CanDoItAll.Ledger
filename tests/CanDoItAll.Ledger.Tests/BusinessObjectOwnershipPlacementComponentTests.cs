using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    public async Task HomeSecuritySurface_StillHostsOwnershipVerificationTool()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));
        IRenderedComponent<MessageSecurityPanel> security =
            RenderComponent<MessageSecurityPanel>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        await ClickTabAsync(security, "Ownership");

        Assert.NotEmpty(home.FindAll("[data-testid='cda-action-security']"));
        Assert.Contains("Ownership payload", security.Markup, StringComparison.Ordinal);
        Assert.Contains("Verify ownership", security.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomeBalances_ShowOnlyRealTokenSourcesInsideBalancesCard()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        LedgerSpendableOutput tokenSource = Assert.Single(
            workspace.SpendableOutputsOrEmpty,
            output => output.Token?.TokenId.Value == "USD-CREDIT"
                && output.BusinessObject is null);
        IReadOnlyList<LedgerSpendableOutput> businessObjectBackedSources = workspace.SpendableOutputsOrEmpty
            .Where(output => output.BusinessObject is not null)
            .ToArray();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        await ClickTabAsync(home, "Balances");

        Assert.Contains("Balances", home.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Token Sources<", home.Markup, StringComparison.Ordinal);
        home.WaitForAssertion(() => Assert.Contains("Source UTXOs", home.Markup, StringComparison.Ordinal));
        Assert.Contains(
            FormatCopyableValue(tokenSource.OutPoint.ToString(), startChars: 8, endChars: 6),
            home.Markup,
            StringComparison.Ordinal);
        Assert.All(
            businessObjectBackedSources,
            output => Assert.DoesNotContain(
                FormatCopyableValue(output.OutPoint.ToString(), startChars: 8, endChars: 6),
                home.Markup,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task HomeBalances_UsesResponsiveFullWidthLanesWhenSourcesExist()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();
        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        await ClickTabAsync(home, "Balances");

        var layout = home.Find("[data-testid='cda-balance-layout']");
        var summary = home.Find("[data-testid='cda-balance-summary-lane']");
        var sources = home.Find("[data-testid='cda-balance-sources-lane']");

        Assert.Contains("--cda-grid-columns-lg:repeat(2,minmax(0,1fr))", layout.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("w-full", summary.ClassList);
        Assert.Contains("min-w-0", summary.ClassList);
        Assert.Contains("w-full", sources.ClassList);
        Assert.Equal(2, layout.QuerySelectorAll("table").Length);
        Assert.Empty(home.FindComponents<FormSection>());
    }

    [Fact]
    public async Task HomeBalances_UsesOneLaneWhenNoTokenSourcesExist()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace() with
        {
            SpendableOutputs = []
        };
        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        await ClickTabAsync(home, "Balances");

        var layout = home.Find("[data-testid='cda-balance-layout']");
        Assert.Contains("--cda-grid-columns-lg:repeat(1,minmax(0,1fr))", layout.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Single(layout.QuerySelectorAll("table"));
        Assert.Empty(home.FindAll("[data-testid='cda-balance-sources-lane']"));
    }

    [Fact]
    public async Task HomeWorkspace_RendersRequestedTabOrderAndPanels()
    {
        BusinessObjectWorkspaceModel workspace = EconomyComponentSampleData.CreateWorkspace();

        IRenderedComponent<EconomyAccountWorkspace> home =
            RenderComponent<EconomyAccountWorkspace>(parameters => parameters
                .Add(x => x.Workspace, workspace));

        var tabs = home.FindAll("[role='tab']");

        Assert.Collection(
            tabs,
            tab => Assert.Contains("Overview", tab.TextContent, StringComparison.Ordinal),
            tab => Assert.Contains("Balances", tab.TextContent, StringComparison.Ordinal),
            tab => Assert.Contains("Commerce", tab.TextContent, StringComparison.Ordinal),
            tab => Assert.Contains("Transactions history", tab.TextContent, StringComparison.Ordinal));
        Assert.NotEmpty(home.FindAll("[data-testid='cda-account-analytics']"));

        await ClickTabAsync(home, "Balances");
        home.WaitForAssertion(() => Assert.Contains("Source UTXOs", home.Markup, StringComparison.Ordinal));

        await ClickTabAsync(home, "Commerce");
        home.WaitForAssertion(() => Assert.NotEmpty(home.FindAll("[data-testid='cda-commerce-dashboard']")));

        await ClickTabAsync(home, "Transactions history");
        home.WaitForAssertion(() => Assert.NotEmpty(home.FindAll("[data-testid='cda-recent-transactions']")));
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

    private static Task ClickTabAsync<TComponent>(IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
        => component.FindAll("[role='tab']")
            .Single(item => item.TextContent.Contains(text, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

    private static void AssertActionLabel(
        IRenderedComponent<EconomyAccountWorkspace> home,
        string testId,
        string expectedLabel)
    {
        string label = home.Find($"[data-testid='{testId}'] .rz-button-text").TextContent.Trim();
        Assert.Equal(expectedLabel, label);
    }

    private static string FormatCopyableValue(string value, int startChars, int endChars)
        => value.Length <= startChars + endChars + 3
            ? value
            : $"{value[..startChars]}...{value[^endChars..]}";
}
