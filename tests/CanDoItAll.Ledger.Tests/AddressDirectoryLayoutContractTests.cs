namespace CanDoItAll.Economy.Tests;

public sealed class AddressDirectoryLayoutContractTests
{
    [Fact]
    public void Directory_prioritizes_contact_cards_and_delegates_creation_to_a_compact_dialog()
    {
        string directory = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Account", "AddressDirectoryDialogContent.razor");
        string addDialog = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "Components", "Account", "AddressDirectoryAddDialogContent.razor");
        string css = ReadRepoFile(
            "src", "CanDoItAll.Economy.Components", "wwwroot", "css", "economy-components.css");

        Assert.Contains("<Toolbar Class=\"cda-address-directory__control-band\"", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarRow>", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarFields", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarActions>", directory, StringComparison.Ordinal);
        Assert.Contains("<Grid ColumnTemplate=\"repeat(auto-fit, minmax(min(100%, 20rem), 1fr))\"", directory, StringComparison.Ordinal);
        Assert.Contains("OpenAsync<AddressDirectoryAddDialogContent>", directory, StringComparison.Ordinal);
        Assert.Contains("CloseOnBackdrop = false", directory, StringComparison.Ordinal);
        Assert.Contains("CurrentDialog.CloseAsync(result)", directory, StringComparison.Ordinal);
        Assert.DoesNotContain("<DialogScaffold", directory, StringComparison.Ordinal);
        Assert.DoesNotContain("cda-address-directory__add", directory, StringComparison.Ordinal);

        Assert.Contains("<FormStack Class=\"cda-address-directory-add-form\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("Text=\"Display name\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("Text=\"Ledger address\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("<Alert AlertStyle=\"AlertStyle.Info\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"cda-address-directory-add-save\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("CurrentDialog.CloseAsync(result)", addDialog, StringComparison.Ordinal);

        Assert.Matches(@"(?s)\.cda-address-directory__grid\s*\{[^}]*flex:\s*1\s+1\s+auto;[^}]*min-height:\s*0;[^}]*overflow-y:\s*auto;", css);
        Assert.Matches(@"(?s)\.cda-address-directory-card\s*\{[^}]*display:\s*flex;[^}]*flex-direction:\s*column;[^}]*min-height:\s*12rem;", css);
        Assert.DoesNotMatch(@"(?s)\.cda-address-directory-card\s*\{[^}]*aspect-ratio", css);
        Assert.Matches(@"(?m)^section\.cda-address-directory-add-modal\s*\{", css);
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
