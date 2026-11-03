using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StudyGuardian.Core;

namespace StudyGuardian.Data;

public sealed partial class SqliteActivityRepository
{
    public Task<IReadOnlyList<StoredStudySession>> ReadSessionsAsync(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return RunAsync<IReadOnlyList<StoredStudySession>>(connection=>
        {
            using var command=connection.CreateCommand();
            command.CommandText="SELECT id,goal,start,end,strictness,status,policy_json FROM sessions ORDER BY start DESC,rowid DESC LIMIT $limit";
            command.Parameters.AddWithValue("$limit",limit);using var reader=command.ExecuteReader();List<StoredStudySession> rows=[];
            while(reader.Read())
            {
                InterventionOptions? intervention=null;int? supportive=null;
                try
                {
                    using var policy=JsonDocument.Parse(reader.GetString(6));
                    if(policy.RootElement.TryGetProperty("intervention",out var value) && value.ValueKind==JsonValueKind.Object)
                    { intervention=value.Deserialize<InterventionOptions>(PolicyJson);intervention?.Validate(); }
                    if(policy.RootElement.TryGetProperty("supportive_time_limit_minutes",out var minutes) && minutes.ValueKind==JsonValueKind.Number && minutes.TryGetInt32(out var count) && count>0) supportive=count;
                }
                catch(Exception e) when(e is JsonException or InvalidOperationException or ArgumentException) { intervention=null;supportive=null; }
                var strictness=Enum.TryParse<SessionStrictness>(reader.GetString(4),true,out var parsed) && Enum.IsDefined(parsed) ? parsed : SessionStrictness.Standard;
                rows.Add(new(reader.GetString(0),reader.GetString(1),DateTimeOffset.Parse(reader.GetString(2),CultureInfo.InvariantCulture),
                    reader.IsDBNull(3)?null:DateTimeOffset.Parse(reader.GetString(3),CultureInfo.InvariantCulture),strictness,reader.GetString(5),intervention,supportive));
            }
            return rows;
        });
    }
    public Task<IReadOnlyList<ActivitySegment>> ReadSessionSegmentsAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return RunAsync<IReadOnlyList<ActivitySegment>>(connection=>
        {
            using var command=connection.CreateCommand();command.CommandText="""
                SELECT id,session_id,window_handle,process_id,process,title,is_accessible,ts,end,duration_ticks,pause_reason,url,verdict,source,level
                FROM events WHERE session_id=$id ORDER BY ts,rowid;
                """;
            command.Parameters.AddWithValue("$id",sessionId);using var reader=command.ExecuteReader();List<ActivitySegment> rows=[];
            while(reader.Read()) rows.Add(ReadSegment(reader));
            return rows;
        });
    }
    public Task SaveEscalationResetAsync(string sessionId,DateTimeOffset at) => RunAsync(connection=>
    {
        using var command=connection.CreateCommand();command.CommandText="INSERT OR IGNORE INTO escalation_resets(session_id,ts) VALUES($id,$at)";
        command.Parameters.AddWithValue("$id",sessionId);command.Parameters.AddWithValue("$at",Format(at));command.ExecuteNonQuery();
    });
    public Task<IReadOnlyList<DateTimeOffset>> ReadEscalationResetsAsync(string sessionId) => RunAsync<IReadOnlyList<DateTimeOffset>>(connection=>
    {
        using var command=connection.CreateCommand();command.CommandText="SELECT ts FROM escalation_resets WHERE session_id=$id ORDER BY ts";
        command.Parameters.AddWithValue("$id",sessionId);using var reader=command.ExecuteReader();List<DateTimeOffset> rows=[];
        while(reader.Read()) rows.Add(DateTimeOffset.Parse(reader.GetString(0),CultureInfo.InvariantCulture));return rows;
    });
    private static ActivitySegment ReadSegment(SqliteDataReader reader) => new(reader.GetString(0),reader.GetString(1),
        new(reader.GetInt64(2),reader.GetInt32(3),reader.GetString(4),reader.GetString(5),reader.GetBoolean(6),reader.IsDBNull(11)?null:reader.GetString(11)),
        DateTimeOffset.Parse(reader.GetString(7),CultureInfo.InvariantCulture),DateTimeOffset.Parse(reader.GetString(8),CultureInfo.InvariantCulture),
        TimeSpan.FromTicks(reader.GetInt64(9)),Enum.Parse<PauseReason>(reader.GetString(10)),reader.IsDBNull(12)?null:ReadDecision(reader.GetString(12),reader.GetString(13)),reader.GetInt32(14));
}
