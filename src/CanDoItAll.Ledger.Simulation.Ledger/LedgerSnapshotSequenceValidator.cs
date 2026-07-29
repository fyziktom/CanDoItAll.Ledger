namespace CanDoItAll.Ledger.Simulation.Ledger;

public static class LedgerSnapshotSequenceValidator
{
    public static void Validate(IReadOnlyList<LedgerSnapshotSimulationSource> snapshots)
    {
        var seen = new HashSet<long>();
        long previous = long.MinValue;
        foreach (LedgerSnapshotSimulationSource snapshot in snapshots)
        {
            if (!seen.Add(snapshot.Sequence))
            {
                throw new InvalidOperationException($"Duplicate ledger snapshot sequence '{snapshot.Sequence}'.");
            }

            if (snapshot.Sequence < previous)
            {
                throw new InvalidOperationException("Ledger snapshot sequences must be non-decreasing.");
            }

            previous = snapshot.Sequence;
        }
    }
}
