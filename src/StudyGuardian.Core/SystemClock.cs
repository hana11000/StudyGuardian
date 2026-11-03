using System.Diagnostics;

namespace StudyGuardian.Core;

public sealed class SystemClock : IClock
{
    private readonly long _origin = Stopwatch.GetTimestamp();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_origin);
}
