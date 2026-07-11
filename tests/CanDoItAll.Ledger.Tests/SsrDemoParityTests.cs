using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class SsrDemoParityTests
{
    [Fact]
    public async Task SsrDevelopmentWorkspace_DefaultsBusinessObjectDraftToOneCarrierToken()
    {
        using ServiceProvider provider = CreateDevelopmentProvider();
        using IServiceScope scope = provider.CreateScope();
        EconomyDevelopmentWorkspaceSession session = scope.ServiceProvider.GetRequiredService<EconomyDevelopmentWorkspaceSession>();

        BusinessObjectWorkspaceModel workspace = await session.GetWorkspaceAsync(CancellationToken.None);

        Assert.Equal("Seeded in-memory SSR", workspace.ConnectionLabel);
        Assert.Equal("0", workspace.TransactionDraft.MainAmount.ToString());
        Assert.Equal(new TokenId("USD-CREDIT"), workspace.TransactionDraft.TokenId);
        Assert.Equal("1", workspace.TransactionDraft.TokenAmount!.Value.ToString());
    }

    [Fact]
    public async Task SsrDevelopmentWorkspace_SubmitsExplicitUtxoSplitAndMergeWithWasmShape()
    {
        using ServiceProvider provider = CreateDevelopmentProvider();
        using IServiceScope scope = provider.CreateScope();
        EconomyDevelopmentWorkspaceSession session = scope.ServiceProvider.GetRequiredService<EconomyDevelopmentWorkspaceSession>();

        BusinessObjectWorkspaceModel initial = await session.GetWorkspaceAsync(CancellationToken.None);
        LedgerSpendableOutput source = initial.SpendableOutputsOrEmpty
            .Where(IsPlainMainOutput)
            .OrderBy(output => output.MainAmount.Value)
            .First(output => output.MainAmount.Value >= 125m);

        EconomyTransactionExecutionResult split = await session.SubmitTransactionAsync(
            initial.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.UtxoSplit,
                SelectedUtxos = [source.OutPoint],
                UtxoSplitOutputs =
                [
                    new UtxoSplitOutput(initial.Address, Amount.FromInt64(40)),
                    new UtxoSplitOutput(initial.Address, Amount.FromInt64(60))
                ]
            },
            CancellationToken.None);
        Assert.True(split.Accepted, split.Message);
        TxId splitTxId = split.TxId ?? throw new InvalidOperationException("Split did not return a transaction id.");

        EconomyTransactionDetailsModel? splitDetails = await session.GetTransactionDetailsAsync(splitTxId, CancellationToken.None);
        Assert.NotNull(splitDetails);
        Assert.Equal(source.OutPoint, Assert.Single(splitDetails!.Inputs).PreviousOutPoint);
        Assert.Contains(splitDetails.Outputs, output => output.OutputIndex == 0 && output.Address == initial.Address && output.MainAmount.ToString() == "40");
        Assert.Contains(splitDetails.Outputs, output => output.OutputIndex == 1 && output.Address == initial.Address && output.MainAmount.ToString() == "60");

        Address mergeReceiver = initial.AddressBookmarks.First().Address;
        EconomyTransactionExecutionResult merge = await session.SubmitTransactionAsync(
            initial.TransactionDraft with
            {
                SelectedMode = BusinessObjectTransactionMode.UtxoMerge,
                PaymentReceiverAddress = mergeReceiver,
                SelectedUtxos = [new OutPoint(splitTxId, 0), new OutPoint(splitTxId, 1)]
            },
            CancellationToken.None);
        Assert.True(merge.Accepted, merge.Message);
        TxId mergeTxId = merge.TxId ?? throw new InvalidOperationException("Merge did not return a transaction id.");

        EconomyTransactionDetailsModel? mergeDetails = await session.GetTransactionDetailsAsync(mergeTxId, CancellationToken.None);
        Assert.NotNull(mergeDetails);
        Assert.Equal(2, mergeDetails!.Inputs.Count);
        EconomyTransactionOutputDetails mergedOutput = Assert.Single(mergeDetails.Outputs);
        Assert.Equal(mergeReceiver, mergedOutput.Address);
        Assert.Equal("100", mergedOutput.MainAmount.ToString());
        Assert.Null(mergedOutput.Token);
        Assert.Null(mergedOutput.BusinessObject);
    }

    [Fact]
    public void SsrRealNodeSession_RoutesUtxoModesThroughBusinessObjectAccount()
    {
        string source = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Services", "EconomyNodeWorkspaceSession.cs");

        Assert.Contains("TokenAmount: defaultToken is null ? null : Amount.FromInt64(1)", source, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectTransactionMode.UtxoMerge => await account.MergeUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new UtxoMergeRequest(", source, StringComparison.Ordinal);
        Assert.Contains("BusinessObjectTransactionMode.UtxoSplit => await account.SplitUtxosAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new UtxoSplitRequest(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SsrTransactionComposer_ExposesDistinctUtxoMergeAndSplitModes()
    {
        string composerSource = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Components", "Transactions", "TransactionComposerShell.razor");
        string catalogSource = ReadRepoFile("src", "CanDoItAll.Economy.Components", "Models", "TransactionFlowFoundation.cs");

        Assert.Contains("new(BusinessObjectTransactionMode.UtxoMerge, TransactionFlowFamily.Utxo, \"Merge UTXOs\"", catalogSource, StringComparison.Ordinal);
        Assert.Contains("new(BusinessObjectTransactionMode.UtxoSplit, TransactionFlowFamily.Utxo, \"Split UTXOs\"", catalogSource, StringComparison.Ordinal);
        Assert.Contains("case BusinessObjectTransactionMode.UtxoMerge:", composerSource, StringComparison.Ordinal);
        Assert.Contains("case BusinessObjectTransactionMode.UtxoSplit:", composerSource, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateDevelopmentProvider()
    {
        var services = new ServiceCollection();
        services.AddEconomyDevelopmentWorkspaceSession();
        return services.BuildServiceProvider();
    }

    private static bool IsPlainMainOutput(LedgerSpendableOutput output)
        => output.BusinessObject is null && output.Token is null && output.MainAmount.Value > 0m;

    private static string ReadRepoFile(params string[] relativeParts)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. relativeParts]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.Ledger.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
