namespace CanDoItAll.Economy.Tests;

public sealed class AccountAppUiSourceContractTests
{
    [Fact]
    public void App_shells_keep_page_and_portal_hosts_inside_one_theme_boundary()
    {
        string[] shells =
        [
            ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Layout", "MainLayout.razor"),
            ReadRepoFile("examples", "CanDoItAll.Economy.Components.Demo", "Components", "App.razor"),
            ReadRepoFile("examples", "CanDoItAll.Ledger.Simulator.App", "Components", "App.razor"),
            ReadRepoFile("src", "CanDoItAll.Economy.Node", "Components", "App.razor")
        ];

        foreach (string shell in shells)
        {
            Assert.Equal(1, shell.Split("<ThemeHost>", StringSplitOptions.None).Length - 1);
            Assert.Equal(1, shell.Split("</ThemeHost>", StringSplitOptions.None).Length - 1);

            int themeStart = shell.IndexOf("<ThemeHost>", StringComparison.Ordinal);
            int themeEnd = shell.IndexOf("</ThemeHost>", StringComparison.Ordinal);
            int pageContent = shell.Contains("@Body", StringComparison.Ordinal)
                ? shell.IndexOf("@Body", StringComparison.Ordinal)
                : shell.IndexOf("<Routes", StringComparison.Ordinal);

            Assert.True(pageContent > themeStart && pageContent < themeEnd);
            Assert.True(shell.IndexOf("<DialogHost", StringComparison.Ordinal) > themeStart);
            Assert.True(shell.IndexOf("<DialogHost", StringComparison.Ordinal) < themeEnd);
            Assert.True(shell.IndexOf("<Notification", StringComparison.Ordinal) > themeStart);
            Assert.True(shell.IndexOf("<Notification", StringComparison.Ordinal) < themeEnd);
            Assert.True(shell.IndexOf("<Tooltip", StringComparison.Ordinal) > themeStart);
            Assert.True(shell.IndexOf("<Tooltip", StringComparison.Ordinal) < themeEnd);
        }
    }

    [Fact]
    public void App_hosts_load_their_generated_scoped_style_bundles()
    {
        (string Host, string Bundle)[] hosts =
        [
            (ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "wwwroot", "index.html"), "CanDoItAll.Economy.AccountApp.styles.css"),
            (ReadRepoFile("examples", "CanDoItAll.Economy.Components.Demo", "Components", "App.razor"), "CanDoItAll.Economy.Components.Demo.styles.css"),
            (ReadRepoFile("examples", "CanDoItAll.Ledger.Simulator.App", "Components", "App.razor"), "CanDoItAll.Economy.Simulator.App.styles.css"),
            (ReadRepoFile("src", "CanDoItAll.Economy.Node", "Components", "App.razor"), "CanDoItAll.Economy.Node.styles.css")
        ];

        foreach ((string host, string bundle) in hosts)
        {
            Assert.Contains($"href=\"{bundle}\"", host, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Home_uses_steps_for_import_and_removes_unlocked_workspace_frame()
    {
        string home = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");

        Assert.Contains("<Steps @bind-Value=\"importStep\"", home, StringComparison.Ordinal);
        Assert.Contains("<StepsItem Text=\"Import\"", home, StringComparison.Ordinal);
        Assert.Contains("<StepsItem Text=\"Password\"", home, StringComparison.Ordinal);
        Assert.Contains("<StepsItem Text=\"Finish\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"import-account-steps\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"account-entry\"", home, StringComparison.Ordinal);
        Assert.Contains("<main class=\"account-entry\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"first-run-account-flow\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"stored-vault-login\"", home, StringComparison.Ordinal);
        Assert.Contains("@if (Session.HasStoredVault)", home, StringComparison.Ordinal);
        Assert.DoesNotContain("account-app__workspace-frame", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_guard_requires_cancel_reveal_countdown_and_double_confirmation()
    {
        string home = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");
        string session = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Services", "AccountAppSession.cs");

        Assert.Contains("ResetConfirmationPhrase = \"RESET LOCAL VAULT\"", session, StringComparison.Ordinal);
        Assert.Contains("ResetAcknowledged", session, StringComparison.Ordinal);
        Assert.Contains("ResetConfirmation.Trim()", session, StringComparison.Ordinal);
        Assert.Contains("vaultStore.DeleteAsync(Options.VaultId", session, StringComparison.Ordinal);
        Assert.Contains("ClearSessionState();", session, StringComparison.Ordinal);
        Assert.Contains("ResetRevealCancelCount = 5", home, StringComparison.Ordinal);
        Assert.Contains("ResetCountdownSeconds = 5", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"unlock-cancel\"", home, StringComparison.Ordinal);
        Assert.Contains("TestId=\"reset-recovery-dialog\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-recovery-countdown\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-recovery-continue\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-acknowledge\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-confirmation\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-final-acknowledge\"", home, StringComparison.Ordinal);
        Assert.Contains("Disabled=\"@(!CanExecuteResetRecovery)\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-local-vault\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("FormSection Title=\"Reset local vault\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Account_setup_fields_update_before_navigation_click()
    {
        string home = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");

        Assert.Contains("@bind-Value=\"Session.ImportBackupJson\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.ImportBackupPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.ImportNewPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.ImportConfirmPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.UnlockPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.CreatePassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind-Value=\"Session.PastedKeyPassword\"", home, StringComparison.Ordinal);
        Assert.True(home.Split("Immediate=\"true\"", StringSplitOptions.None).Length - 1 >= 14);
        Assert.Contains("<FormField", home, StringComparison.Ordinal);
        Assert.Contains("<TextBox", home, StringComparison.Ordinal);
        Assert.Contains("<TextArea", home, StringComparison.Ordinal);
        Assert.Contains("<SummaryTiles data-testid=\"import-review\">", home, StringComparison.Ordinal);
        Assert.Contains("<Dialog IsOpen=\"true\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("max-w-[1120px]", home, StringComparison.Ordinal);
        Assert.DoesNotContain("account-button", home, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind:event=\"oninput\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void BuyBo_assigns_commerce_identity_from_confirmed_output_only()
    {
        string buyBo = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "BuyBo.razor");

        Assert.Contains("BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex)", buyBo, StringComparison.Ordinal);
        Assert.Contains("objectId.IsCanonicalOutPointId()", buyBo, StringComparison.Ordinal);
        Assert.Contains("purchaseResult.ResultBusinessObjectId", buyBo, StringComparison.Ordinal);
        Assert.Contains("$\"bo-commerce://payment/request/{paymentRequestId}\"", buyBo, StringComparison.Ordinal);
        Assert.Contains("<SummaryTiles>", buyBo, StringComparison.Ordinal);
        Assert.Contains("<main class=\"buy-bo-page\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("max-w-[1440px]", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("buy-bo-facts", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePaymentObjectId", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePurchaseObjectId", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:payment:\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:purchase:\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:receipt:\"", buyBo, StringComparison.Ordinal);
    }

    [Fact]
    public void Standalone_routes_keep_a_main_landmark_after_the_shell_wrapper_is_removed()
    {
        string home = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");
        string buyBo = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "BuyBo.razor");
        string notFound = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "NotFound.razor");
        string homeStyles = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor.css");

        Assert.Contains("<main class=\"account-entry\"", home, StringComparison.Ordinal);
        Assert.Contains("<main class=\"buy-bo-page\"", buyBo, StringComparison.Ordinal);
        Assert.Contains("<Body Class=\"p-3 sm:p-4\">", notFound, StringComparison.Ordinal);
        Assert.Contains("grid-row: 1 / span 3;", homeStyles, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.Ledger.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find repository root.");
        }

        string path = Path.Combine([directory.FullName, .. segments]);
        return File.ReadAllText(path);
    }
}
