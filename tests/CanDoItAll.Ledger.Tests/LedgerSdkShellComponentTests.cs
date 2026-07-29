using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerSdkShellComponentTests : BunitContext
{
    private const string MenuId = "economy-sdk-demo";

    public LedgerSdkShellComponentTests()
    {
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<IJSRuntime>(new SideMenuTestJsRuntime());
    }

    [Fact]
    public async Task Shell_KeepsFragmentNavigationAndSideMenuSelectionInSync()
    {
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        SideMenuService sideMenus = Services.GetRequiredService<SideMenuService>();
        navigation.NavigateTo("/sdk-components?view=full");

        IRenderedComponent<LedgerSdkShell> component = Render<LedgerSdkShell>();

        component.WaitForAssertion(() =>
            Assert.Equal("account", sideMenus.GetSnapshot(MenuId).SelectedItemId));
        Assert.EndsWith("/sdk-components?view=full", navigation.Uri, StringComparison.Ordinal);

        navigation.NavigateTo("/sdk-components?view=full#transactions");

        component.WaitForAssertion(() =>
        {
            Assert.Equal("transactions", sideMenus.GetSnapshot(MenuId).SelectedItemId);
            Assert.Equal(
                "page",
                component.Find("[data-testid='side-menu-economy-sdk-demo-item-transactions']")
                    .GetAttribute("aria-current"));
        });

        await component.InvokeAsync(() =>
            component.Find("[data-testid='side-menu-economy-sdk-demo-item-admin']").Click());

        component.WaitForAssertion(() =>
        {
            Assert.EndsWith("/sdk-components?view=full#admin", navigation.Uri, StringComparison.Ordinal);
            Assert.Equal("admin", sideMenus.GetSnapshot(MenuId).SelectedItemId);
        });

        navigation.NavigateTo("/sdk-components?view=full#analytics");

        component.WaitForAssertion(() =>
            Assert.Equal("analytics", sideMenus.GetSnapshot(MenuId).SelectedItemId));
    }

    [Fact]
    public void Custom_navigation_sidebar_can_stack_without_consuming_the_mobile_viewport()
    {
        RenderFragment navigation = builder =>
            builder.AddMarkupContent(0, "<nav data-testid='custom-sdk-navigation'>Custom navigation</nav>");

        IRenderedComponent<LedgerSdkShell> component = Render<LedgerSdkShell>(parameters => parameters
            .Add(x => x.Navigation, navigation));

        Sidebar sidebar = component.FindComponent<Sidebar>().Instance;

        Assert.False(sidebar.FillHeight);
        Assert.True(sidebar.Scrollable);
        Assert.Contains(
            "cda-sdk-shell--custom-nav",
            component.FindComponent<Layout>().Instance.Class ?? string.Empty,
            StringComparison.Ordinal);
        Assert.Empty(component.FindComponents<SideMenu>());
        Assert.Single(component.FindAll("[data-testid='custom-sdk-navigation']"));
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
