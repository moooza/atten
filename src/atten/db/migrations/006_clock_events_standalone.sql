CREATE TABLE clock_events_standalone (
    id INTEGER PRIMARY KEY,
    remote_id TEXT NOT NULL,
    name TEXT NOT NULL,
    date TEXT NOT NULL,
    time TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    CHECK (length(trim(remote_id)) > 0),
    CHECK (length(trim(name)) > 0),
    CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (time GLOB '[0-9][0-9]:[0-9][0-9]:[0-9][0-9]')
);

INSERT INTO clock_events_standalone (
    id, remote_id, name, date, time, created_at, updated_at
)
SELECT id, remote_id, name, date, time, created_at, updated_at
FROM clock_events;

DROP TABLE clock_events;
ALTER TABLE clock_events_standalone RENAME TO clock_events;

CREATE INDEX clock_events_remote_date_time
    ON clock_events (remote_id, date, time);
