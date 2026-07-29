using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed class SystemClock : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class ManualTimeProvider : ITimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow.ToUniversalTime();
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (gate)
            {
                return utcNow;
            }
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (gate)
        {
            utcNow = value.ToUniversalTime();
        }
    }

    public DateTimeOffset Advance(TimeSpan duration)
    {
        lock (gate)
        {
            utcNow = utcNow.Add(duration);
            return utcNow;
        }
    }
}
