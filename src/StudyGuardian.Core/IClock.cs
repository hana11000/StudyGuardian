namespace StudyGuardian.Core;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    TimeSpan Elapsed { get; }
}
