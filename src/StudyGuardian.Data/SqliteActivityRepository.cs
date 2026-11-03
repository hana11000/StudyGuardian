using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using StudyGuardian.Core;

namespace StudyGuardian.Data;

public sealed partial class SqliteActivityRepository : IActivityRepository, IDecisionCache
{
    private readonly object _gate = new();
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions PolicyJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
    public string DatabasePath { get; }

    public SqliteActivityRepository(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Pooling = false
        }.ToString();
    }

    public Task InitializeAsync() => RunAsync(connection =>
    {
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar(), CultureInfo.InvariantCulture) > 2)
            throw new InvalidDataException("数据库版本较新，请使用相应版本的 Study Guardian。");

        using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode=WAL";
        journal.ExecuteNonQuery();

        using var transaction = connection.BeginTransaction();
        using var schemaStream = typeof(SqliteActivityRepository).Assembly.GetManifestResourceStream("StudyGuardian.Data.schema.sql")!;
        using var reader = new StreamReader(schemaStream);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = reader.ReadToEnd();
        command.ExecuteNonQuery();
        command.CommandText = """
            UPDATE sessions SET
                end = COALESCE((SELECT MAX(end) FROM events WHERE session_id=sessions.id), start),
                status = 'interrupted'
            WHERE status='active' AND end IS NULL;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    });

    public Task StartSessionAsync(string sessionId, DateTimeOffset startedAt, StudySessionOptions options) => RunAsync(connection =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Goal);
        if (options.Duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions(id, goal, policy_json, start, strictness, status)
            VALUES($id, $goal, $policy, $start, $strictness, 'active');
            """;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$start", Format(startedAt));
        command.Parameters.AddWithValue("$goal", options.Goal);
        command.Parameters.AddWithValue("$policy", JsonSerializer.Serialize(new { duration_seconds = options.Duration.TotalSeconds, rules = options.Rules,
            goal_summary = options.Policy?.GoalSummary, supportive_time_limit_minutes = options.Policy?.SupportiveTimeLimitMinutes }, PolicyJson));
        command.Parameters.AddWithValue("$strictness", options.Strictness.ToString().ToLowerInvariant());
        command.ExecuteNonQuery();
    });

    public Task SaveSegmentAsync(ActivitySegment segment)
    {
        if (segment.Duration < TimeSpan.Zero || segment.EndedAt < segment.StartedAt)
            throw new ArgumentException("活动片段的时长不能为负。", nameof(segment));
        return RunAsync(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO events(id, session_id, ts, end, process, process_id, window_handle, title,
                    url, verdict, source, duration_seconds, duration_ticks, active_seconds, pause_reason, is_accessible, level)
                VALUES($id, $session, $start, $end, $process, $pid, $handle, $title,
                    $url, $verdict, $source, $seconds, $ticks, $active, $reason, $accessible, $level)
                ON CONFLICT(id) DO UPDATE SET end=excluded.end,
                    duration_seconds=excluded.duration_seconds, duration_ticks=excluded.duration_ticks,
                    active_seconds=excluded.active_seconds, level=MAX(events.level, excluded.level)
                WHERE excluded.duration_ticks >= events.duration_ticks;
                """;
            command.Parameters.AddWithValue("$id", segment.Id);
            command.Parameters.AddWithValue("$session", segment.SessionId);
            command.Parameters.AddWithValue("$start", Format(segment.StartedAt));
            command.Parameters.AddWithValue("$end", Format(segment.EndedAt));
            command.Parameters.AddWithValue("$process", segment.Window.ProcessName);
            command.Parameters.AddWithValue("$pid", segment.Window.ProcessId);
            command.Parameters.AddWithValue("$handle", segment.Window.Handle);
            command.Parameters.AddWithValue("$title", segment.Window.Title);
            command.Parameters.AddWithValue("$url", (object?)segment.Window.Url ?? DBNull.Value);
            command.Parameters.AddWithValue("$verdict", segment.Decision is { } decision ? VerdictText(decision.Verdict) : DBNull.Value);
            command.Parameters.AddWithValue("$source", segment.Decision?.Source.ToString().ToLowerInvariant() ?? "monitor");
            command.Parameters.AddWithValue("$seconds", segment.Duration.TotalSeconds);
            command.Parameters.AddWithValue("$ticks", segment.Duration.Ticks);
            command.Parameters.AddWithValue("$active", segment.ActiveSeconds);
            command.Parameters.AddWithValue("$reason", segment.PauseReason.ToString());
            command.Parameters.AddWithValue("$accessible", segment.Window.IsAccessible);
            command.Parameters.AddWithValue("$level", segment.Level);
            command.ExecuteNonQuery();
        });
    }

    public Task EndSessionAsync(string sessionId, DateTimeOffset endedAt) => RunAsync(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions SET end=MAX(start, $end,
                COALESCE((SELECT MAX(end) FROM events WHERE session_id=$id), start)), status='completed'
            WHERE id=$id;
            """;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$end", Format(endedAt));
        command.ExecuteNonQuery();
    });

    public Task<IReadOnlyList<ActivitySegment>> ReadRecentAsync(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return RunAsync<IReadOnlyList<ActivitySegment>>(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, session_id, window_handle, process_id, process, title, is_accessible,
                    ts, end, duration_ticks, pause_reason, url, verdict, source, level FROM events ORDER BY ts DESC, rowid DESC LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);
            using var reader = command.ExecuteReader();
            List<ActivitySegment> rows = [];
            while (reader.Read())
                rows.Add(new(reader.GetString(0), reader.GetString(1),
                    new(reader.GetInt64(2), reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetBoolean(6),
                        reader.IsDBNull(11) ? null : reader.GetString(11)),
                    DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
                    TimeSpan.FromTicks(reader.GetInt64(9)), Enum.Parse<PauseReason>(reader.GetString(10)),
                    reader.IsDBNull(12) ? null : ReadDecision(reader.GetString(12), reader.GetString(13)), reader.GetInt32(14)));
            return rows;
        });
    }

    private Task RunAsync(Action<SqliteConnection> action) => RunAsync(connection => { action(connection); return true; });

    private Task<T> RunAsync<T>(Func<SqliteConnection, T> action) => Task.Run(() =>
    {
        // SQLite's async APIs perform synchronous I/O; keep them off the UI/hook thread.
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return action(connection);
        }
    });

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string VerdictText(StudyVerdict verdict) => verdict == StudyVerdict.OnTask ? "on_task" : verdict.ToString().ToLowerInvariant();

    private static RuleDecision ReadDecision(string verdict, string source) =>
        Enum.TryParse<StudyVerdict>(verdict.Replace("_", ""), true, out var parsedVerdict) &&
        Enum.TryParse<DecisionSource>(source, true, out var parsedSource)
            ? new(parsedVerdict, parsedSource) : RuleDecision.Unknown;
}
