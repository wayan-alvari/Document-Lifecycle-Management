using DocumentLifecycle.Application.Abstractions.Time;

namespace DocumentLifecycle.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
