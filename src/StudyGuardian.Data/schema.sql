CREATE TABLE IF NOT EXISTS sessions (
    id TEXT PRIMARY KEY,
    goal TEXT NOT NULL,
    policy_json TEXT NOT NULL,
    start TEXT NOT NULL,
    end TEXT,
    strictness TEXT NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('active', 'completed', 'interrupted'))
);

CREATE TABLE IF NOT EXISTS events (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    ts TEXT NOT NULL,
    end TEXT NOT NULL,
    process TEXT NOT NULL,
    process_id INTEGER NOT NULL,
    window_handle INTEGER NOT NULL,
    title TEXT NOT NULL,
    url TEXT,
    verdict TEXT,
    source TEXT NOT NULL,
    level INTEGER NOT NULL DEFAULT 0,
    duration_seconds REAL NOT NULL CHECK(duration_seconds >= 0),
    duration_ticks INTEGER NOT NULL CHECK(duration_ticks >= 0),
    active_seconds REAL NOT NULL CHECK(active_seconds >= 0 AND active_seconds <= duration_seconds),
    pause_reason TEXT NOT NULL,
    is_accessible INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_events_session_time ON events(session_id, ts);
CREATE INDEX IF NOT EXISTS ix_events_time ON events(ts DESC);
CREATE TABLE IF NOT EXISTS decision_cache (
    key TEXT PRIMARY KEY,
    verdict TEXT NOT NULL,
    reason TEXT,
    confidence REAL NOT NULL,
    expires_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_cache_expiry ON decision_cache(expires_at);
PRAGMA user_version = 2;
