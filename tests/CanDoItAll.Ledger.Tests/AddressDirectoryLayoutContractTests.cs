namespace CanDoItAll.Ledger.Tests;

public sealed class AddressDirectoryLayoutContractTests
{
    [Fact]
    public void Directory_prioritizes_contact_cards_and_delegates_creation_to_a_compact_dialog()
    {
        string directory = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Account", "AddressDirectoryDialogContent.razor");
        string addDialog = ReadRepoFile(
            "src", "CanDoItAll.Ledger.Components", "Components", "Account", "AddressDirectoryAddDialogContent.razor");
        Assert.Contains("<Toolbar data-testid=\"cda-address-directory-toolbar\">", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarRow>", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarFields", directory, StringComparison.Ordinal);
        Assert.Contains("<ToolbarActions>", directory, StringComparison.Ordinal);
        Assert.Contains("<Grid ColumnTemplate=\"repeat(auto-fit, minmax(min(100%, 20rem), 1fr))\"", directory, StringComparison.Ordinal);
        Assert.Contains("Class=\"min-h-0 flex-1 overflow-y-auto\"", directory, StringComparison.Ordinal);
        Assert.Contains("<SurfaceCard Class=\"h-full\"", directory, StringComparison.Ordinal);
        Assert.Contains("<Avatar ImageUrl=\"@entry.ProfileImageLink\"", directory, StringComparison.Ordinal);
        Assert.Contains("OpenAsync<AddressDirectoryAddDialogContent>", directory, StringComparison.Ordinal);
        Assert.Contains("Size = ModalSize.Compact", directory, StringComparison.Ordinal);
        Assert.Contains("CloseOnBackdrop = false", directory, StringComparison.Ordinal);
        Assert.Contains("CurrentDialog.CloseAsync(result)", directory, StringComparison.Ordinal);
        Assert.DoesNotContain("<DialogScaffold", directory, StringComparison.Ordinal);
        Assert.DoesNotContain("Class=\"cda-address-directory", directory, StringComparison.Ordinal);

        Assert.Contains("<FormStack data-testid=\"cda-address-directory-add-dialog-content\">", addDialog, StringComparison.Ordinal);
        Assert.Contains("Text=\"Display name\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("Text=\"Ledger address\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("<Alert AlertStyle=\"AlertStyle.Info\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"cda-address-directory-add-save\"", addDialog, StringComparison.Ordinal);
        Assert.Contains("CurrentDialog.CloseAsync(result)", addDialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Class=\"cda-address-directory", addDialog, StringComparison.Ordinal);
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
