CREATE TABLE IF NOT EXISTS personnel (
    id INTEGER PRIMARY KEY,
    remote_id TEXT UNIQUE,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    daily_hours REAL NOT NULL CHECK (daily_hours > 0),
    mobile TEXT
);
