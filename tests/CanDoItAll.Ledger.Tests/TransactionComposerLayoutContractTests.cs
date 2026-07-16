namespace CanDoItAll.Economy.Tests;

public sealed class TransactionComposerLayoutContractTests
{
    [Fact]
    public void Context_rail_uses_BaseLib_composition_with_only_scroll_behavior_isolated()
    {
        string rail = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionContextRail.razor");
        string isolatedCss = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionContextRail.razor.css");

        Assert.Contains("<SurfaceCard Soft=\"true\">", rail, StringComparison.Ordinal);
        Assert.Contains("<SummaryTiles ColumnsSm=\"2\" ColumnsXl=\"2\"", rail, StringComparison.Ordinal);
        Assert.Contains("<Stack GapScale=\"LayoutGap.Small\" AlignItems=\"AlignItems.Stretch\">", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-transaction-context-rail", rail, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.transaction-context-rail\s*\{[^}]*position:\s*sticky;[^}]*max-height:[^;}]+;[^}]*overflow-y:\s*auto;", isolatedCss);
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
        string workflowCss = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionWorkflowSteps.razor.css");
        string split = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionUtxoSplitForm.razor");
        string splitCss = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionUtxoSplitForm.razor.css");
        string picker = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Transactions", "BusinessObjectPickerDialogContent.razor");
        string accountHost = ReadRepoFile(
            "src",
            "CanDoItAll.Economy.Components",
            "Components",
            "Account",
            "EconomyAccountWorkspaceAdapterHost.razor");

        Assert.Contains("data-testid=\"transaction-workbench\"", shell, StringComparison.Ordinal);
        Assert.Contains("ColumnTemplateLg=\"minmax(0, 1fr) minmax(19rem, 22rem)\"", shell, StringComparison.Ordinal);
        Assert.Contains("<main class=\"min-w-0 w-full max-w-full overflow-hidden\">", shell, StringComparison.Ordinal);
        Assert.Contains("<TransactionContextRail", shell, StringComparison.Ordinal);
        Assert.Contains("ReviewReadyChanged", shell, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.cda-transaction-workflow-steps\s*>\s*ol\s*\{[^}]*width:\s*100%;[^}]*margin:\s*0;[^}]*list-style:\s*none;", workflowCss);
        Assert.DoesNotMatch(@"(?s)\.cda-transaction-workflow-steps\s*>\s*ol\s*\{[^}]*40rem", workflowCss);
        Assert.Matches(@"(?s)\.cda-transaction-workflow-steps\s*>\s*div:last-child\s*>\s*\.rz-stack\s*>\s*div:first-child\s*\{[^}]*display:\s*none;", workflowCss);
        Assert.DoesNotContain("cda-transaction-workflow-steps", css, StringComparison.Ordinal);
        Assert.Contains("<StickyActionFooter StatusLabel=\"Selection\"", picker, StringComparison.Ordinal);
        Assert.Contains("<Grid ColumnsLg=\"2\" Gap=\"LayoutGap.Medium\">", split, StringComparison.Ordinal);
        Assert.Contains("<EmptyState Eyebrow=\"Outputs\"", split, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.transaction-split-output-list\s*\{[^}]*max-height:[^;}]+;[^}]*overflow-y:\s*auto;", splitCss);
        Assert.DoesNotContain(".cda-transaction-workbench", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cda-transaction-context-rail", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cda-split-output-grid", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cda-action-review-panel", css, StringComparison.Ordinal);
        Assert.Contains(
            "<ActionReviewPanel",
            ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionReviewPanel.razor"),
            StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)Size = ModalSize\.Full,\s*DenseChrome = true,\s*CloseOnBackdrop = false,\s*Class = ""cda-ledger-dialog"",\s*TestId = ""cda-send-transaction-dialog""",
            accountHost);
        Assert.DoesNotContain("cda-ledger-dialog--send", accountHost, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?m)^section\.cda-ledger-dialog--send\s*\{", css);
        Assert.DoesNotContain("cda-token-creation-modal", css, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-transaction-picker-modal", css, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-ledger-dialog--address-directory", css, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?m)^\.cda-ledger-dialog--send\s*\{", css);
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
