namespace CanDoItAll.Economy.Simulation.Abstractions;

public sealed class SimulationClockState
{
    public int StepIndex { get; set; }
    public DateTimeOffset SimulatedAtUtc { get; set; }
    public TimeSpan StepDuration { get; set; } = TimeSpan.FromDays(1);
}
