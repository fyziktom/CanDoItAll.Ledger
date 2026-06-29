using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Samples;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Economy.Tests;

public sealed class AccountSetupComponentTests : TestContext
{
    [Fact]
    public async Task AccountBackupPanel_ExposesHostNeutralAccountFlowActions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var createRequests = 0;
        var unlockRequests = 0;
        var backupRequests = 0;
        var importBackupRequests = 0;
        var importKeyRequests = 0;
        var subAccountRequests = 0;

        IRenderedComponent<AccountBackupPanel> component = RenderComponent<AccountBackupPanel>(parameters => parameters
            .Add(x => x.Model, EconomyComponentSampleData.CreateAccountBackup())
            .Add(x => x.CreateAccountRequested, EventCallback.Factory.Create(this, () => createRequests++))
            .Add(x => x.UnlockAccountRequested, EventCallback.Factory.Create(this, () => unlockRequests++))
            .Add(x => x.BackupAccountRequested, EventCallback.Factory.Create(this, () => backupRequests++))
            .Add(x => x.ImportBackupRequested, EventCallback.Factory.Create(this, () => importBackupRequests++))
            .Add(x => x.ImportPastedPrivateKeyRequested, EventCallback.Factory.Create(this, () => importKeyRequests++))
            .Add(x => x.CreateSubAccountRequested, EventCallback.Factory.Create(this, () => subAccountRequests++)));

        await ClickByTextAsync(component, "Finish setup");
        await ClickByTextAsync(component, "Unlock account");
        await ClickByTextAsync(component, "Backup account");
        await ClickByTextAsync(component, "Import backup");
        await ClickByTextAsync(component, "Import private key");
        await ClickByTextAsync(component, "Add SubAccount");

        Assert.Equal(1, createRequests);
        Assert.Equal(1, unlockRequests);
        Assert.Equal(1, backupRequests);
        Assert.Equal(1, importBackupRequests);
        Assert.Equal(1, importKeyRequests);
        Assert.Equal(1, subAccountRequests);
    }

    private static async Task ClickByTextAsync(IRenderedComponent<AccountBackupPanel> component, string text)
    {
        IRenderedComponent<Button> button = component.FindComponents<Button>()
            .Single(button => string.Equals(button.Instance.Text, text, StringComparison.Ordinal));
        await component.InvokeAsync(() => button.Instance.Click.InvokeAsync());
    }
}
