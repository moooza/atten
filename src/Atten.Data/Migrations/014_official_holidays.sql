CREATE TABLE official_holidays (
    id INTEGER PRIMARY KEY,
    date TEXT NOT NULL,
    title TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    created_by INTEGER NOT NULL REFERENCES users (id),
    updated_by INTEGER NOT NULL REFERENCES users (id),
    UNIQUE (date),
    CHECK (length(trim(title)) > 0),
    CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]')
);
