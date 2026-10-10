CREATE TABLE leaves (
    id INTEGER PRIMARY KEY,
    personnel_id INTEGER NOT NULL REFERENCES personnel (id),
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    minutes INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    CHECK (minutes > 0),
    CHECK (start_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (end_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (end_date >= start_date)
);

CREATE INDEX leaves_personnel_start ON leaves (personnel_id, start_date);
