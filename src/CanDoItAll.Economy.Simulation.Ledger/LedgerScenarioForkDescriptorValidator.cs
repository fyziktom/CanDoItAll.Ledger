namespace CanDoItAll.Economy.Simulation.Ledger;

public sealed class LedgerScenarioForkDescriptorValidator
{
    public IReadOnlyList<string> Validate(LedgerScenarioForkDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        List<string> errors = [];
        Require(errors, !string.IsNullOrWhiteSpace(descriptor.ForkId), "Fork id is required.");
        Require(errors, !string.IsNullOrWhiteSpace(descriptor.LedgerId), "Ledger id is required.");
        Require(errors, !string.IsNullOrWhiteSpace(descriptor.ScenarioId), "Scenario id is required.");
        Require(errors, descriptor.StartingSequence >= 0, "Starting sequence must be non-negative.");
        return errors;
    }

    private static void Require(List<string> errors, bool condition, string message)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }
}
