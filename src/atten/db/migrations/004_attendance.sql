CREATE TABLE IF NOT EXISTS attendance (
    id INTEGER PRIMARY KEY,
    remote_id TEXT NOT NULL,
    name TEXT NOT NULL,
    date TEXT NOT NULL,
    time TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (remote_id) REFERENCES personnel (remote_id) ON UPDATE CASCADE,
    CHECK (length(trim(remote_id)) > 0),
    CHECK (length(trim(name)) > 0),
    CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (time GLOB '[0-9][0-9]:[0-9][0-9]:[0-9][0-9]')
);

CREATE INDEX IF NOT EXISTS attendance_remote_date_time
    ON attendance (remote_id, date, time);
