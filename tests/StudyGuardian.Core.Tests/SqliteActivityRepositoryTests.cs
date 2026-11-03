using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;
using StudyGuardian.Data;
using System.Text.Json;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class SqliteActivityRepositoryTests
{
    private string _directory = null!;
    private string _path = null!;
    private SqliteActivityRepository _repository = null!;
    private readonly DateTimeOffset _start = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    private static StudySessionOptions Options => new("学习 '增值税'", TimeSpan.FromMinutes(37), SessionStrictness.Strict,
        [new("a", RuleKind.Allow, RuleMatchType.Process, "Notion.exe"),
         new("s", RuleKind.Supportive, RuleMatchType.Domain, "bilibili.com"),
         new("b", RuleKind.Block, RuleMatchType.TitleRegex, "游戏|热搜")]);

    [TestInitialize]
    public async Task SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "StudyGuardian.Tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "activity.db");
        _repository = new(_path);
        await _repository.InitializeAsync();
        await _repository.StartSessionAsync("session", _start, Options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // The randomly allocated test directory is the sole recursive delete target.
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private ActivitySegment Segment(string id, double seconds, PauseReason reason = PauseReason.None) =>
        new(id, "session", new(123, 42, "Notion.exe", "税法 '增值税'; DROP TABLE events; --"),
            _start, _start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds), reason);

    [TestMethod]
    public async Task DecisionCachePersistsAcrossInstancesAndExpiresAtBoundary()
    {
        var entry = new CachedDecision("quoted'key", new(StudyVerdict.Supportive, DecisionSource.Llm, .95, "税法 '讲解'"), _start.AddMinutes(30));
        await _repository.SaveDecisionAsync(entry, default);
        var reopened = new SqliteActivityRepository(_path);
        Assert.AreEqual(entry, await reopened.FindDecisionAsync(entry.Key, _start.AddMinutes(29), default));
        Assert.IsNull(await reopened.FindDecisionAsync(entry.Key, _start.AddMinutes(30), default));
        Assert.HasCount(0, await _repository.ReadRecentAsync(10));
    }

    [TestMethod]
    public async Task DecisionCacheReplacesExistingKey()
    {
        var entry = new CachedDecision("k", new(StudyVerdict.Uncertain, DecisionSource.Llm, .5, "待确认"), _start.AddMinutes(5));
        await _repository.SaveDecisionAsync(entry, default);
        var replacement = entry with { Decision = new(StudyVerdict.OnTask, DecisionSource.Llm, 1, "学习"), ExpiresAt = _start.AddMinutes(10) };
        await _repository.SaveDecisionAsync(replacement, default);
        Assert.AreEqual(replacement, await _repository.FindDecisionAsync("k", _start.AddMinutes(6), default));
    }

    [TestMethod]
    public async Task UnicodeAndSqlLikeTitlesSurviveRealDatabaseRoundTrip()
    {
        var expected = Segment("first", 12.125);
        await _repository.SaveSegmentAsync(expected);
        var rows = await _repository.ReadRecentAsync(10);
        Assert.HasCount(1, rows);
        Assert.AreEqual(expected, rows[0]);
    }

    [TestMethod]
    public async Task CheckpointUpsertDoesNotDuplicateOrRegressDuration()
    {
        await _repository.SaveSegmentAsync(Segment("first", 15));
        await _repository.SaveSegmentAsync(Segment("first", 30));
        await _repository.SaveSegmentAsync(Segment("first", 16));
        var rows = await _repository.ReadRecentAsync(10);
        Assert.HasCount(1, rows);
        Assert.AreEqual(30, rows[0].Duration.TotalSeconds);
    }

    [TestMethod]
    public async Task PauseReasonSurvivesRestartAndDoesNotBecomeActive()
    {
        await _repository.SaveSegmentAsync(Segment("first", 90, PauseReason.Away));
        await _repository.EndSessionAsync("session", _start.AddSeconds(90));
        var reopened = new SqliteActivityRepository(_path);
        await reopened.InitializeAsync();
        var row = (await reopened.ReadRecentAsync(10)).Single();
        Assert.AreEqual(PauseReason.Away, row.PauseReason);
        Assert.AreEqual(0, row.ActiveSeconds);
    }

    [TestMethod]
    public async Task RecoveryClosesInterruptedSessionAtLastSavedTime()
    {
        await _repository.SaveSegmentAsync(Segment("first", 15));
        var reopened = new SqliteActivityRepository(_path);
        await reopened.InitializeAsync();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT status, end FROM sessions WHERE id='session'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("interrupted", reader.GetString(0));
        Assert.AreEqual(_start.AddSeconds(15), DateTimeOffset.Parse(reader.GetString(1)));
    }

    [TestMethod]
    public async Task RecoveryWithoutAnyEventsUsesSessionStart()
    {
        await new SqliteActivityRepository(_path).InitializeAsync();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT end FROM sessions WHERE id='session'";
        Assert.AreEqual(_start, DateTimeOffset.Parse((string)command.ExecuteScalar()!));
    }

    [TestMethod]
    public async Task CompletingSessionIsNotOverwrittenByRecovery()
    {
        await _repository.EndSessionAsync("session", _start.AddSeconds(20));
        await new SqliteActivityRepository(_path).InitializeAsync();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM sessions WHERE id='session'";
        Assert.AreEqual("completed", command.ExecuteScalar());
    }

    [TestMethod]
    public async Task RecentRecordsAreLimitedAndIncludePreviousSessions()
    {
        await _repository.SaveSegmentAsync(Segment("first", 10));
        await _repository.EndSessionAsync("session", _start.AddSeconds(10));
        await _repository.StartSessionAsync("second", _start.AddSeconds(10), Options);
        await _repository.SaveSegmentAsync(Segment("next", 5) with
        { SessionId = "second", StartedAt = _start.AddSeconds(10), EndedAt = _start.AddSeconds(15) });
        var rows = await _repository.ReadRecentAsync(1);
        Assert.HasCount(1, rows);
        Assert.AreEqual("second", rows[0].SessionId);
        Assert.HasCount(2, await _repository.ReadRecentAsync(10));
    }

    [TestMethod]
    public async Task MissingSessionFailsRatherThanSilentlyCreatingOrphanEvents()
    {
        await Assert.ThrowsExactlyAsync<SqliteException>(() => _repository.SaveSegmentAsync(Segment("first", 5) with { SessionId = "missing" }));
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    [TestMethod]
    public void SessionSavesGoalDurationStrictnessAndAllRuleGroups()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT goal, policy_json, strictness FROM sessions WHERE id='session'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("学习 '增值税'", reader.GetString(0));
        Assert.AreEqual("strict", reader.GetString(2));
        using var policy = JsonDocument.Parse(reader.GetString(1));
        Assert.AreEqual(2220, policy.RootElement.GetProperty("duration_seconds").GetDouble());
        var rules = policy.RootElement.GetProperty("rules");
        Assert.AreEqual(3, rules.GetArrayLength());
        Assert.AreEqual("allow", rules[0].GetProperty("kind").GetString());
        Assert.AreEqual("supportive", rules[1].GetProperty("kind").GetString());
        Assert.AreEqual("title_regex", rules[2].GetProperty("match_type").GetString());
    }

    [TestMethod]
    [DataRow(StudyVerdict.OnTask, DecisionSource.Rule, "on_task", "rule")]
    [DataRow(StudyVerdict.Supportive, DecisionSource.Rule, "supportive", "rule")]
    [DataRow(StudyVerdict.Distracted, DecisionSource.Rule, "distracted", "rule")]
    [DataRow(StudyVerdict.Unknown, DecisionSource.Fallback, "unknown", "fallback")]
    [DataRow(StudyVerdict.Neutral, DecisionSource.Rule, "neutral", "rule")]
    public async Task VerdictSourceAndUrlRoundTrip(StudyVerdict verdict, DecisionSource source, string expectedVerdict, string expectedSource)
    {
        var expected = Segment("decision", 15) with
        {
            Decision = new(verdict, source),
            Window = new(1, 20, "chrome.exe", "学习", Url: "https://example.com/Learn?q=Tax")
        };
        await _repository.SaveSegmentAsync(expected);
        Assert.AreEqual(expected, (await _repository.ReadRecentAsync(10)).Single());
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT verdict, source FROM events WHERE id='decision'";
        using var reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expectedVerdict, reader.GetString(0));
        Assert.AreEqual(expectedSource, reader.GetString(1));
    }

    [TestMethod]
    public async Task LegacyM1RowIsNotReclassifiedOnReopen()
    {
        await _repository.SaveSegmentAsync(Segment("legacy", 20));
        await _repository.EndSessionAsync("session", _start.AddSeconds(20));
        var reopened = new SqliteActivityRepository(_path);
        await reopened.InitializeAsync();
        var legacy = (await reopened.ReadRecentAsync(10)).Single();
        Assert.IsNull(legacy.Decision);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT source FROM events WHERE id='legacy' AND verdict IS NULL";
        Assert.AreEqual("monitor", command.ExecuteScalar());
    }

    [TestMethod]
    public async Task DebounceSplitPreservesUnknownHistoryAndFocusDuration()
    {
        var clock = new FakeClock();
        var window = new WindowSnapshot(1, 10, "Notion.exe", "学习");
        var session = new StudySession(clock, new(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)), [], 999);
        session.Start("session", Options, window, TimeSpan.Zero);
        clock.Advance(4);
        session.Observe(window, TimeSpan.Zero);
        foreach (var segment in session.Checkpoint()) await _repository.SaveSegmentAsync(segment);
        clock.Advance(1);
        foreach (var segment in session.Observe(window, TimeSpan.Zero)) await _repository.SaveSegmentAsync(segment);
        clock.Advance(10);
        foreach (var segment in session.Stop()) await _repository.SaveSegmentAsync(segment);
        var rows = (await _repository.ReadRecentAsync(10)).OrderBy(row => row.StartedAt).ToArray();
        Assert.HasCount(2, rows);
        Assert.AreEqual(5, rows[0].Duration.TotalSeconds);
        Assert.AreEqual(StudyVerdict.Unknown, rows[0].Decision!.Verdict);
        Assert.AreEqual(10, rows[1].Duration.TotalSeconds);
        Assert.AreEqual(StudyVerdict.OnTask, rows[1].Decision!.Verdict);
        Assert.AreEqual(rows[0].EndedAt, rows[1].StartedAt);
    }

    [TestMethod]
    public async Task IdleSplitAfterCheckpointDoesNotOverlapPersistedSegments()
    {
        var clock = new FakeClock();
        var window = new WindowSnapshot(1, 10, "Notion.exe", "学习");
        var recorder = new ActivityRecorder(clock, TimeSpan.FromSeconds(300), [], 999);
        recorder.Start("session", window, TimeSpan.Zero);
        clock.Advance(299.999);
        recorder.Observe(window, TimeSpan.FromSeconds(299.999));
        clock.Advance(0.002);
        await _repository.SaveSegmentAsync(recorder.Checkpoint()!);
        clock.Advance(0.999);
        foreach (var segment in recorder.Observe(window, TimeSpan.FromSeconds(301)))
            await _repository.SaveSegmentAsync(segment);
        await _repository.SaveSegmentAsync(recorder.Stop()!);
        var rows = (await _repository.ReadRecentAsync(10)).OrderBy(row => row.StartedAt).ToArray();
        Assert.HasCount(2, rows);
        Assert.AreEqual(rows[0].EndedAt, rows[1].StartedAt);
        Assert.AreEqual(300, rows[0].ActiveSeconds);
        Assert.AreEqual(301, rows.Sum(row => row.Duration.TotalSeconds));
    }

    [TestMethod]
    public async Task SessionEndMatchesTheFinalEventAfterForwardClockChangeAndDelayedWrite()
    {
        var clock = new FakeClock();
        var recorder = new ActivityRecorder(clock, TimeSpan.FromSeconds(300), [], 999);
        recorder.Start("session", new(1, 10, "Notion.exe", "学习"), TimeSpan.Zero);
        clock.Advance(12);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var last = recorder.Stop()!;
        await _repository.SaveSegmentAsync(last);
        clock.Advance(5);
        Assert.IsNotNull(recorder.EndedAt);
        await _repository.EndSessionAsync("session", recorder.EndedAt.Value);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT end FROM sessions WHERE id='session'";
        Assert.AreEqual(last.EndedAt, DateTimeOffset.Parse((string)command.ExecuteScalar()!));
    }
}
