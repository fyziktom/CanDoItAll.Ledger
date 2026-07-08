using System;
using System.IO;

namespace CanDoItAll.Economy.Tests;

public sealed class ConsoleDemoParityTests
{
    [Fact]
    public void ConsoleDemoDriver_ExposesExplicitUtxoSplitMergeFacadeOverAccount()
    {
        string source = ReadRepoFile("examples", "CanDoItAll.Economy.ConsoleDemo", "DemoAccountDriver.cs");

        Assert.Contains("public Task<LedgerSubmitResult> SplitUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("=> Account.SplitUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new UtxoSplitRequest(", source, StringComparison.Ordinal);

        Assert.Contains("public Task<LedgerSubmitResult> MergeUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("=> Account.MergeUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new UtxoMergeRequest(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsoleDemoLesson7_DistinguishesPlainUtxoSplitMergeFromBusinessObjectOperations()
    {
        string source = ReadRepoFile("examples", "CanDoItAll.Economy.ConsoleDemo", "LearningScenarios.cs");

        Assert.Contains("Plain UTXO split and merge", source, StringComparison.Ordinal);
        Assert.Contains("SplitUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("MergeUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("SplitBusinessObjectPaymentAsync(", source, StringComparison.Ordinal);
        Assert.Contains("MergeBusinessObjectsAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsoleDemoReadme_DocumentsWasmParityTransactionSemantics()
    {
        string source = ReadRepoFile("examples", "CanDoItAll.Economy.ConsoleDemo", "README.md");

        Assert.Contains("UTXO split", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTXO merge", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one-token", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zero main", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepoFile(params string[] relativeParts)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativeParts]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.Ledger.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }
}
