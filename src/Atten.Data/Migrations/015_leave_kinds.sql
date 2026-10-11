ALTER TABLE leaves ADD COLUMN kind TEXT NOT NULL DEFAULT 'leave'
    CHECK (kind IN ('leave', 'settlement'));

CREATE TABLE leave_year_transfers (
    id INTEGER PRIMARY KEY,
    personnel_id INTEGER NOT NULL REFERENCES personnel (id),
    year INTEGER NOT NULL,
    carry_minutes INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    created_by INTEGER NOT NULL REFERENCES users (id),
    updated_by INTEGER NOT NULL REFERENCES users (id),
    UNIQUE (personnel_id, year),
    CHECK (year > 0),
    CHECK (carry_minutes > 0)
);
