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
        Assert.Contains(".cda-transaction-workbench__canvas", css, StringComparison.Ordinal);
        Assert.Contains("overflow: visible;", css, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.cda-transaction-composer\s+\.cda-material-icon[^\{]*\{[^}]*letter-spacing:\s*normal\s*!important;[^}]*text-transform:\s*none\s*!important;", css);
        Assert.Matches(@"(?s)\.cda-transaction-workflow-steps\s*>\s*div:last-child\s*>\s*\.rz-stack\s*>\s*div:first-child\s*\{[^}]*display:\s*none;", css);
        Assert.Matches(@"(?s)\.cda-transaction-context-rail__actions\s*\{[^}]*position:\s*sticky;", css);
        Assert.Matches(@"(?s)\.cda-transaction-picker-dialog__actions\s*\{[^}]*position:\s*sticky;", css);
        Assert.Matches(@"(?s)\.cda-split-output-grid__items\s*\{[^}]*grid-template-columns:\s*repeat\(2,[^;}]+;[^}]*max-height:[^;}]+;[^}]*overflow-y:\s*auto;", css);
        Assert.Contains(".cda-action-review-panel__facts", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(4, minmax(0, 1fr));", css, StringComparison.Ordinal);
        Assert.Contains("cda-split-output-empty", css, StringComparison.Ordinal);
        Assert.Contains("Class = \"cda-vef-dialog cda-token-creation-modal\"", accountHost, StringComparison.Ordinal);
        Assert.Contains(".cda-token-creation-modal", css, StringComparison.Ordinal);
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
