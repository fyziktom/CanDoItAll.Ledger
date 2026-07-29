using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Services;

internal sealed class TransactionInputDetailResolver(IReadOnlyList<ITransactionInputResolutionStrategy> strategies)
{
    public static TransactionInputDetailResolver Default { get; } = new(
        [
            new SingleInputOutputTotalResolutionStrategy(),
            new PreviousTransactionOutputResolutionStrategy()
        ]);

    public async Task<IReadOnlyList<LedgerTransactionInputDetails>> ResolveAsync(
        IReadOnlyList<LedgerTransactionInputDetails> inputs,
        IReadOnlyList<LedgerTransactionOutputDetails> outputs,
        Func<OutPoint, CancellationToken, Task<LedgerTransactionOutputDetails?>> previousOutputProvider,
        CancellationToken cancellationToken)
    {
        if (!NeedsResolution(inputs, outputs))
        {
            return inputs;
        }

        var context = new TransactionInputResolutionContext(inputs, outputs, previousOutputProvider);
        var resolvedInputs = new List<LedgerTransactionInputDetails>(inputs.Count);
        foreach (LedgerTransactionInputDetails input in inputs)
        {
            LedgerTransactionInputDetails resolved = input;
            if (!NeedsResolution(resolved, outputs))
            {
                resolvedInputs.Add(resolved);
                continue;
            }

            foreach (ITransactionInputResolutionStrategy strategy in strategies)
            {
                LedgerTransactionInputDetails? candidate = await strategy
                    .TryResolveAsync(context, resolved, cancellationToken)
                    .ConfigureAwait(false);
                if (candidate is null)
                {
                    continue;
                }

                resolved = candidate;
                if (!NeedsResolution(resolved, outputs))
                {
                    break;
                }
            }

            resolvedInputs.Add(resolved);
        }

        return resolvedInputs;
    }

    public bool NeedsResolution(
        IReadOnlyList<LedgerTransactionInputDetails> inputs,
        IReadOnlyList<LedgerTransactionOutputDetails> outputs)
        => inputs.Any(input => NeedsResolution(input, outputs));

    private static bool NeedsResolution(
        LedgerTransactionInputDetails input,
        IReadOnlyList<LedgerTransactionOutputDetails> outputs)
        => input.MainAmount is null || (input.Token is null && HasMultipleOutputTokenTypes(outputs));

    private static bool HasMultipleOutputTokenTypes(IReadOnlyList<LedgerTransactionOutputDetails> outputs)
        => outputs
            .Where(output => output.Token is not null)
            .Select(output => output.Token!.TokenId)
            .Distinct()
            .Skip(1)
            .Any();
}

internal sealed record TransactionInputResolutionContext(
    IReadOnlyList<LedgerTransactionInputDetails> Inputs,
    IReadOnlyList<LedgerTransactionOutputDetails> Outputs,
    Func<OutPoint, CancellationToken, Task<LedgerTransactionOutputDetails?>> PreviousOutputProvider);

internal interface ITransactionInputResolutionStrategy
{
    Task<LedgerTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        LedgerTransactionInputDetails input,
        CancellationToken cancellationToken);
}

internal sealed class SingleInputOutputTotalResolutionStrategy : ITransactionInputResolutionStrategy
{
    public Task<LedgerTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        LedgerTransactionInputDetails input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Inputs.Count != 1 || input.MainAmount is not null)
        {
            return Task.FromResult<LedgerTransactionInputDetails?>(null);
        }

        Amount mainAmount = new(context.Outputs.Sum(output => output.MainAmount.Value));
        TokenTransfer? token = input.Token ?? InferSingleToken(context.Outputs);
        return Task.FromResult<LedgerTransactionInputDetails?>(input with
        {
            MainAmount = mainAmount,
            Token = token
        });
    }

    private static TokenTransfer? InferSingleToken(IReadOnlyList<LedgerTransactionOutputDetails> outputs)
    {
        var tokenGroups = outputs
            .Where(output => output.Token is not null)
            .GroupBy(output => output.Token!.TokenId)
            .ToArray();
        if (tokenGroups.Length != 1)
        {
            return null;
        }

        return new TokenTransfer(
            tokenGroups[0].Key,
            new Amount(tokenGroups[0].Sum(output => output.Token!.Amount.Value)));
    }
}

internal sealed class PreviousTransactionOutputResolutionStrategy : ITransactionInputResolutionStrategy
{
    public async Task<LedgerTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        LedgerTransactionInputDetails input,
        CancellationToken cancellationToken)
    {
        LedgerTransactionOutputDetails? previousOutput = await context
            .PreviousOutputProvider(input.PreviousOutPoint, cancellationToken)
            .ConfigureAwait(false);
        if (previousOutput is null)
        {
            return null;
        }

        return input with
        {
            MainAmount = previousOutput.MainAmount,
            Token = previousOutput.Token,
            BusinessObject = previousOutput.BusinessObject
        };
    }
}
