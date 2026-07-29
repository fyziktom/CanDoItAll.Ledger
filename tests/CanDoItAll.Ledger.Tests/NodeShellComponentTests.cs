using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.Node.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.Ledger.Tests;

public sealed class NodeShellComponentTests : BunitContext
{
    private const string MenuId = "ledger-node-primary";

    public NodeShellComponentTests()
    {
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<IJSRuntime>(new SideMenuTestJsRuntime());
    }

    [Fact]
    public async Task Shell_UsesBaseLibViewportCompositionAndPreservesWorkspaceHooks()
    {
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        SideMenuService sideMenus = Services.GetRequiredService<SideMenuService>();
        navigation.NavigateTo("/transactions");

        RenderFragment headerActions = builder =>
            builder.AddMarkupContent(0, "<button data-testid='node-header-action'>Refresh</button>");
        RenderFragment menuFooter = builder =>
            builder.AddMarkupContent(0, "<button data-testid='node-footer-action'>Sign out</button>");

        IRenderedComponent<NodeShell> component = Render<NodeShell>(parameters => parameters
            .Add(x => x.StatusText, "Healthy")
            .Add(x => x.StatusDescription, "All checks passed.")
            .Add(x => x.HeaderActions, headerActions)
            .Add(x => x.MenuFooter, menuFooter)
            .AddChildContent("<section data-testid='node-content'>Workspace content</section>"));

        Layout layout = component.FindComponent<Layout>().Instance;
        Body body = component.FindComponent<Body>().Instance;
        SideMenu sideMenu = component.FindComponent<SideMenu>().Instance;

        Assert.True(layout.FitViewport);
        Assert.True(layout.LockOverflow);
        Assert.True(body.FillHeight);
        Assert.True(body.Scrollable);
        Assert.True(sideMenu.FillHeight);
        Assert.Equal(MenuId, sideMenu.MenuId);
        Assert.Equal(6, sideMenu.Items.Count);

        component.WaitForAssertion(() =>
            Assert.Equal("transactions", sideMenus.GetSnapshot(MenuId).SelectedItemId));

        Assert.Single(component.FindAll("[data-testid='node-shell']"));
        Assert.Single(component.FindAll("[data-testid='node-route-navigation']"));
        Assert.Single(component.FindAll("[data-testid='node-route-header']"));
        Assert.Single(component.FindAll("[data-testid='node-header-action']"));
        Assert.Single(component.FindAll("[data-testid='node-content']"));

        await component.InvokeAsync(() =>
            component.Find("[data-testid='side-menu-ledger-node-primary-item-node-status']").Click());

        component.WaitForAssertion(() =>
        {
            Assert.Single(component.FindAll("[data-testid='node-route-status']"));
            Assert.Contains("Healthy", component.Markup, StringComparison.Ordinal);
            Assert.Contains("All checks passed.", component.Markup, StringComparison.Ordinal);
        });

        await component.InvokeAsync(() =>
            component.Find("[data-testid='side-menu-ledger-node-primary-item-session']").Click());

        component.WaitForAssertion(() =>
        {
            Assert.Single(component.FindAll("[data-testid='node-menu-footer']"));
            Assert.Single(component.FindAll("[data-testid='node-footer-action']"));
        });
    }

    [Fact]
    public async Task Shell_KeepsSideMenuSelectionAndRouteInSync()
    {
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        SideMenuService sideMenus = Services.GetRequiredService<SideMenuService>();
        navigation.NavigateTo("/");

        IRenderedComponent<NodeShell> component = Render<NodeShell>();

        component.WaitForAssertion(() =>
            Assert.Equal("accounts", sideMenus.GetSnapshot(MenuId).SelectedItemId));
        Assert.Equal(navigation.BaseUri, navigation.Uri);

        await component.InvokeAsync(() =>
            component.Find("[data-testid='side-menu-ledger-node-primary-item-admin']").Click());

        component.WaitForAssertion(() =>
        {
            Assert.EndsWith("/admin", navigation.Uri, StringComparison.Ordinal);
            Assert.Equal("admin", sideMenus.GetSnapshot(MenuId).SelectedItemId);
        });

        navigation.NavigateTo("/analytics?range=30d");

        component.WaitForAssertion(() =>
        {
            Assert.Equal("analytics", sideMenus.GetSnapshot(MenuId).SelectedItemId);
            Assert.Equal(
                "page",
                component.Find("[data-testid='side-menu-ledger-node-primary-item-analytics']")
                    .GetAttribute("aria-current"));
        });
    }

    private sealed class SideMenuTestJsRuntime : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "import" && typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            if (identifier == "initialize")
            {
                object initialization = Activator.CreateInstance(typeof(TValue), nonPublic: true)
                    ?? throw new InvalidOperationException("Could not create SideMenu interop initialization state.");
                return ValueTask.FromResult((TValue)initialization);
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
