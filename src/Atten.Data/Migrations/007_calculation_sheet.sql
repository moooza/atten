CREATE TABLE calculation_days (
    id INTEGER PRIMARY KEY,
    remote_id TEXT NOT NULL,
    date TEXT NOT NULL,
    balance_minutes INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    UNIQUE (remote_id, date),
    CHECK (length(trim(remote_id)) > 0),
    CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]')
);

CREATE TABLE calculation_punches (
    id INTEGER PRIMARY KEY,
    remote_id TEXT NOT NULL,
    date TEXT NOT NULL,
    slot INTEGER NOT NULL,
    time TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    UNIQUE (remote_id, date, slot),
    CHECK (length(trim(remote_id)) > 0),
    CHECK (slot >= 0),
    CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (time IS NULL OR time GLOB '[0-9][0-9]:[0-9][0-9]:[0-9][0-9]')
);
