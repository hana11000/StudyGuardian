using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    public TimeSpan Elapsed { get; private set; }
    public void Advance(double seconds)
    {
        Elapsed += TimeSpan.FromSeconds(seconds);
        UtcNow += TimeSpan.FromSeconds(seconds);
    }
}
