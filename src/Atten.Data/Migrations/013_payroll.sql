CREATE TABLE payroll_runs (
    id INTEGER PRIMARY KEY,
    personnel_id INTEGER NOT NULL REFERENCES personnel (id),
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    overtime_minutes INTEGER NOT NULL,
    deficit_minutes INTEGER NOT NULL,
    leave_minutes INTEGER NOT NULL,
    remaining_leave_minutes INTEGER NOT NULL,
    status TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    created_by INTEGER NOT NULL REFERENCES users (id),
    updated_by INTEGER NOT NULL REFERENCES users (id),
    CHECK (overtime_minutes >= 0),
    CHECK (deficit_minutes >= 0),
    CHECK (leave_minutes >= 0),
    CHECK (status IN ('draft', 'confirmed')),
    CHECK (start_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (end_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    CHECK (end_date >= start_date),
    UNIQUE (personnel_id, start_date, end_date)
);

CREATE INDEX payroll_runs_personnel_start ON payroll_runs (personnel_id, start_date);
