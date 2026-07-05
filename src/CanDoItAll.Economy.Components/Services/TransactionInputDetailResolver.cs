using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Services;

internal sealed class TransactionInputDetailResolver(IReadOnlyList<ITransactionInputResolutionStrategy> strategies)
{
    public static TransactionInputDetailResolver Default { get; } = new(
        [
            new SingleInputOutputTotalResolutionStrategy(),
            new PreviousTransactionOutputResolutionStrategy()
        ]);

    public async Task<IReadOnlyList<EconomyTransactionInputDetails>> ResolveAsync(
        IReadOnlyList<EconomyTransactionInputDetails> inputs,
        IReadOnlyList<EconomyTransactionOutputDetails> outputs,
        Func<OutPoint, CancellationToken, Task<EconomyTransactionOutputDetails?>> previousOutputProvider,
        CancellationToken cancellationToken)
    {
        if (!NeedsResolution(inputs, outputs))
        {
            return inputs;
        }

        var context = new TransactionInputResolutionContext(inputs, outputs, previousOutputProvider);
        var resolvedInputs = new List<EconomyTransactionInputDetails>(inputs.Count);
        foreach (EconomyTransactionInputDetails input in inputs)
        {
            EconomyTransactionInputDetails resolved = input;
            if (!NeedsResolution(resolved, outputs))
            {
                resolvedInputs.Add(resolved);
                continue;
            }

            foreach (ITransactionInputResolutionStrategy strategy in strategies)
            {
                EconomyTransactionInputDetails? candidate = await strategy
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
        IReadOnlyList<EconomyTransactionInputDetails> inputs,
        IReadOnlyList<EconomyTransactionOutputDetails> outputs)
        => inputs.Any(input => NeedsResolution(input, outputs));

    private static bool NeedsResolution(
        EconomyTransactionInputDetails input,
        IReadOnlyList<EconomyTransactionOutputDetails> outputs)
        => input.MainAmount is null || (input.Token is null && HasMultipleOutputTokenTypes(outputs));

    private static bool HasMultipleOutputTokenTypes(IReadOnlyList<EconomyTransactionOutputDetails> outputs)
        => outputs
            .Where(output => output.Token is not null)
            .Select(output => output.Token!.TokenId)
            .Distinct()
            .Skip(1)
            .Any();
}

internal sealed record TransactionInputResolutionContext(
    IReadOnlyList<EconomyTransactionInputDetails> Inputs,
    IReadOnlyList<EconomyTransactionOutputDetails> Outputs,
    Func<OutPoint, CancellationToken, Task<EconomyTransactionOutputDetails?>> PreviousOutputProvider);

internal interface ITransactionInputResolutionStrategy
{
    Task<EconomyTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        EconomyTransactionInputDetails input,
        CancellationToken cancellationToken);
}

internal sealed class SingleInputOutputTotalResolutionStrategy : ITransactionInputResolutionStrategy
{
    public Task<EconomyTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        EconomyTransactionInputDetails input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Inputs.Count != 1 || input.MainAmount is not null)
        {
            return Task.FromResult<EconomyTransactionInputDetails?>(null);
        }

        Amount mainAmount = new(context.Outputs.Sum(output => output.MainAmount.Value));
        TokenTransfer? token = input.Token ?? InferSingleToken(context.Outputs);
        return Task.FromResult<EconomyTransactionInputDetails?>(input with
        {
            MainAmount = mainAmount,
            Token = token
        });
    }

    private static TokenTransfer? InferSingleToken(IReadOnlyList<EconomyTransactionOutputDetails> outputs)
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
    public async Task<EconomyTransactionInputDetails?> TryResolveAsync(
        TransactionInputResolutionContext context,
        EconomyTransactionInputDetails input,
        CancellationToken cancellationToken)
    {
        EconomyTransactionOutputDetails? previousOutput = await context
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
