namespace CanDoItAll.Economy.Tests;

public sealed class AccountAppUiSourceContractTests
{
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
        Assert.Contains("data-testid=\"reset-recovery-dialog\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-recovery-countdown\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-recovery-continue\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-acknowledge\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-confirmation\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-final-acknowledge\"", home, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@(!CanExecuteResetRecovery)\"", home, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"reset-local-vault\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("FormSection Title=\"Reset local vault\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Account_setup_fields_update_before_navigation_click()
    {
        string home = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "Home.razor");

        Assert.Contains("@bind=\"Session.ImportBackupJson\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.ImportBackupPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.ImportNewPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.ImportConfirmPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.UnlockPassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.CreatePassword\"", home, StringComparison.Ordinal);
        Assert.Contains("@bind=\"Session.PastedKeyPassword\"", home, StringComparison.Ordinal);
        Assert.Equal(14, home.Split("@bind:event=\"oninput\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void BuyBo_assigns_commerce_identity_from_confirmed_output_only()
    {
        string buyBo = ReadRepoFile("examples", "CanDoItAll.Economy.AccountApp", "Pages", "BuyBo.razor");

        Assert.Contains("BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex)", buyBo, StringComparison.Ordinal);
        Assert.Contains("objectId.IsCanonicalOutPointId()", buyBo, StringComparison.Ordinal);
        Assert.Contains("purchaseResult.ResultBusinessObjectId", buyBo, StringComparison.Ordinal);
        Assert.Contains("$\"bo-commerce://payment/request/{paymentRequestId}\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePaymentObjectId", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("CreatePurchaseObjectId", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:payment:\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:purchase:\"", buyBo, StringComparison.Ordinal);
        Assert.DoesNotContain("StartsWith(\"bo:receipt:\"", buyBo, StringComparison.Ordinal);
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
