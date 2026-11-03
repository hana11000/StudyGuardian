namespace StudyGuardian.Core;

public interface IActivityRepository
{
    Task InitializeAsync();
    Task StartSessionAsync(string sessionId, DateTimeOffset startedAt, StudySessionOptions options);
    Task SaveSegmentAsync(ActivitySegment segment);
    Task EndSessionAsync(string sessionId, DateTimeOffset endedAt);
    Task<IReadOnlyList<ActivitySegment>> ReadRecentAsync(int limit);
}
