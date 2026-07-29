namespace CanDoItAll.Ledger.Tests;

public sealed class TransactionComposerLayoutContractTests
{
    [Fact]
    public void Context_rail_uses_compact_BaseLib_composition_without_nested_scrolling()
    {
        string rail = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionContextRail.razor");

        Assert.Contains("<Toolbar", rail, StringComparison.Ordinal);
        Assert.Contains("<CompactStatStrip", rail, StringComparison.Ordinal);
        Assert.Contains("<CompactStat", rail, StringComparison.Ordinal);
        Assert.Contains("<Stack GapScale=\"LayoutGap.Small\" AlignItems=\"AlignItems.Stretch\">", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("<SurfaceCard", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("<SummaryTiles", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow-y", rail, StringComparison.Ordinal);
        Assert.False(RepoFileExists(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionContextRail.razor.css"));
    }

    [Fact]
    public void Desktop_workbench_keeps_wizards_wide_and_actions_persistent()
    {
        string shell = ReadRepoFile(
            "src",
            "CanDoItAll.Ledger.Components",
            "Components",
            "Transactions",
            "TransactionComposerShell.razor");
        string css = ReadRepoFile(
            "src",
            "CanDoItAll.Ledger.Components",
            "wwwroot",
            "css",
            "ledger-components.css");
        string workflow = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionWorkflowSteps.razor");
        string compactWorkflow = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "CompactWorkflowProgress.razor");
        string reviewWizard = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionDetailsReviewWizard.razor");
        string split = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionUtxoSplitForm.razor");
        string splitCss = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionUtxoSplitForm.razor.css");
        string picker = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "BusinessObjectPickerDialogContent.razor");
        string accountHost = ReadRepoFile(
            "src",
            "CanDoItAll.Ledger.Components",
            "Components",
            "Account",
            "LedgerAccountWorkspaceAdapterHost.razor");

        Assert.Contains("data-testid=\"transaction-workbench\"", shell, StringComparison.Ordinal);
        Assert.Contains("ColumnTemplateLg=\"minmax(0, 1fr) minmax(16rem, 19rem)\"", shell, StringComparison.Ordinal);
        Assert.Contains("<main class=\"min-w-0 w-full max-w-full overflow-hidden\">", shell, StringComparison.Ordinal);
        Assert.Contains("<TransactionContextRail", shell, StringComparison.Ordinal);
        Assert.Contains("ReviewReadyChanged", shell, StringComparison.Ordinal);
        Assert.Contains("class=\"w-full min-w-0 gap-1! rounded-none! border-0! bg-transparent! p-0! shadow-none!\"", workflow, StringComparison.Ordinal);
        Assert.Contains("<CompactWorkflowProgress Value=\"currentStep\"", reviewWizard, StringComparison.Ordinal);
        Assert.Contains("Steps=\"@ReviewSteps\"", reviewWizard, StringComparison.Ordinal);
        Assert.DoesNotContain("<StepsItem", reviewWizard, StringComparison.Ordinal);
        Assert.Contains("<Button", compactWorkflow, StringComparison.Ordinal);
        Assert.Contains("<StatusBadge", compactWorkflow, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", compactWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("<Steps", compactWorkflow, StringComparison.Ordinal);
        Assert.Contains("@if (currentStep == 0)", reviewWizard, StringComparison.Ordinal);
        Assert.False(RepoFileExists(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionWorkflowSteps.razor.css"));
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
            ReadRepoFile("src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "TransactionReviewPanel.razor"),
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
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "BusinessObjectCreateTransactionForm.razor");
        string operations = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Transactions", "BusinessObjectOperationWizard.razor");

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

    private static bool RepoFileExists(params string[] relativeParts)
        => File.Exists(Path.Combine([FindRepositoryRoot(), .. relativeParts]));

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
