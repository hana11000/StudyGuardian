using System.Globalization;
using StudyGuardian.Core;

namespace StudyGuardian.Data;

public sealed partial class SqliteActivityRepository
{
    public Task<CachedDecision?> FindDecisionAsync(string key, DateTimeOffset now, CancellationToken cancellationToken) => RunAsync<CachedDecision?>(connection =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM decision_cache WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", Format(now));
        command.ExecuteNonQuery();
        command.CommandText = "SELECT verdict, reason, confidence, expires_at FROM decision_cache WHERE key=$key AND expires_at > $now;";
        command.Parameters.AddWithValue("$key", key);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var verdict = ReadDecision(reader.GetString(0), "llm").Verdict;
        if (verdict is StudyVerdict.Unknown or StudyVerdict.Neutral) return null;
        var confidence = reader.GetDouble(2);
        if (!double.IsFinite(confidence) || confidence < 0 || confidence > 1) return null;
        return new(key, new(verdict, DecisionSource.Llm, confidence, reader.IsDBNull(1) ? null : reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture));
    });

    public Task SaveDecisionAsync(CachedDecision entry, CancellationToken cancellationToken) => RunAsync(connection =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO decision_cache(key, verdict, reason, confidence, expires_at) VALUES($key, $verdict, $reason, $confidence, $expires)
            ON CONFLICT(key) DO UPDATE SET verdict=excluded.verdict, reason=excluded.reason, confidence=excluded.confidence, expires_at=excluded.expires_at;
            """;
        command.Parameters.AddWithValue("$key", entry.Key);
        command.Parameters.AddWithValue("$verdict", VerdictText(entry.Decision.Verdict));
        command.Parameters.AddWithValue("$reason", (object?)entry.Decision.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("$confidence", entry.Decision.Confidence);
        command.Parameters.AddWithValue("$expires", Format(entry.ExpiresAt));
        command.ExecuteNonQuery();
    });
}
