namespace CanDoItAll.Economy.Tests;

public sealed class TransactionComposerLayoutContractTests
{
    [Fact]
    public void Delegated_context_rail_reserves_body_and_footer_rows_when_facts_are_absent()
    {
        string css = ReadRepoFile(
            "src",
            "CanDoItAll.Economy.Components",
            "wwwroot",
            "css",
            "economy-components.css");

        Assert.Matches(@"(?s)\.cda-transaction-context-rail__body\s*\{[^}]*grid-row:\s*3;", css);
        Assert.Matches(@"(?s)\.cda-transaction-context-rail__actions\s*\{[^}]*grid-row:\s*4;", css);
    }

    [Fact]
    public void Desktop_workbench_keeps_wizards_wide_and_actions_persistent()
    {
        string shell = ReadRepoFile(
            "src",
            "CanDoItAll.Economy.Components",
            "Components",
            "Transactions",
            "TransactionComposerShell.razor");
        string css = ReadRepoFile(
            "src",
            "CanDoItAll.Economy.Components",
            "wwwroot",
            "css",
            "economy-components.css");
        string accountHost = ReadRepoFile(
            "src",
            "CanDoItAll.Economy.Components",
            "Components",
            "Account",
            "EconomyAccountWorkspaceAdapterHost.razor");

        Assert.Contains("cda-transaction-workbench", shell, StringComparison.Ordinal);
        Assert.Contains("TransactionContextRail", shell, StringComparison.Ordinal);
        Assert.Contains("ReviewReadyChanged", shell, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) minmax(19rem, 22rem);", css, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.cda-transaction-workbench__canvas\s*\{[^}]*min-width:\s*0;[^}]*overflow-x:\s*clip;", css);
        Assert.Matches(@"(?s)\.cda-transaction-workflow-steps\s*>\s*ol\s*\{[^}]*width:\s*100%;[^}]*margin:\s*0;[^}]*list-style:\s*none;", css);
        Assert.DoesNotMatch(@"(?s)\.cda-transaction-workflow-steps\s*>\s*ol\s*\{[^}]*40rem", css);
        Assert.Matches(@"(?s)\.cda-transaction-composer\s*\{[^}]*container-name:\s*transaction-composer;[^}]*container-type:\s*inline-size;", css);
        Assert.Matches(@"(?s)@container\s+transaction-composer\s*\(max-width:\s*76rem\)\s*\{.*?\.cda-transaction-workbench\s*\{[^}]*grid-template-columns:\s*minmax\(0,\s*1fr\);[^}]*\}.*?\.cda-transaction-context-rail\s*\{[^}]*position:\s*static;[^}]*max-height:\s*none;", css);
        Assert.Matches(@"(?s)\.cda-transaction-composer\s+\.cda-material-icon[^\{]*\{[^}]*letter-spacing:\s*normal\s*!important;[^}]*text-transform:\s*none\s*!important;", css);
        Assert.Matches(@"(?s)\.cda-transaction-workflow-steps\s*>\s*div:last-child\s*>\s*\.rz-stack\s*>\s*div:first-child\s*\{[^}]*display:\s*none;", css);
        Assert.Matches(@"(?s)\.cda-transaction-context-rail__actions\s*\{[^}]*position:\s*sticky;", css);
        Assert.Matches(@"(?s)\.cda-transaction-picker-dialog__actions\s*\{[^}]*position:\s*sticky;", css);
        Assert.Matches(@"(?s)\.cda-split-output-grid__items\s*\{[^}]*grid-template-columns:\s*repeat\(2,[^;}]+;[^}]*max-height:[^;}]+;[^}]*overflow-y:\s*auto;", css);
        Assert.DoesNotContain(".cda-action-review-panel", css, StringComparison.Ordinal);
        Assert.Contains(
            "<ActionReviewPanel",
            ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionReviewPanel.razor"),
            StringComparison.Ordinal);
        Assert.Contains("cda-split-output-empty", css, StringComparison.Ordinal);
        Assert.Contains("Class = \"cda-ledger-dialog cda-token-creation-modal\"", accountHost, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^section\.cda-ledger-dialog--send\s*\{", css);
        Assert.Matches(@"(?m)^section\.cda-ledger-dialog--address-directory\s*\{", css);
        Assert.Matches(@"(?m)^section\.cda-token-creation-modal\s*\{", css);
        Assert.Matches(@"(?m)^section\.cda-transaction-picker-modal\s*\{", css);
        Assert.DoesNotMatch(@"(?m)^\.(?:cda-ledger-dialog--send|cda-ledger-dialog--address-directory|cda-token-creation-modal|cda-transaction-picker-modal)\s*\{", css);
    }

    [Fact]
    public void New_object_wizards_never_offer_an_editable_target_identity()
    {
        string create = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "BusinessObjectCreateTransactionForm.razor");
        string operations = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "BusinessObjectOperationWizard.razor");

        Assert.Contains("bo-create-object-id-pending", create, StringComparison.Ordinal);
        Assert.DoesNotContain("SetObjectIdAsync", create, StringComparison.Ordinal);
        Assert.Contains("bo-clone-object-id-pending", operations, StringComparison.Ordinal);
        Assert.Contains("bo-merge-object-id-pending", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("bo-clone-target-id", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("bo-merge-target-id", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("SetTargetObjectIdAsync", operations, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeParts)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativeParts]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
