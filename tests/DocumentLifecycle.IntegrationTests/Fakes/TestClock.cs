using DocumentLifecycle.Application.Abstractions.Time;

namespace DocumentLifecycle.IntegrationTests.Fakes;

public sealed class TestClock : IClock
{
    private DateTime utcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => utcNow;

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        utcNow = utcNow.Add(amount);
    }
}
