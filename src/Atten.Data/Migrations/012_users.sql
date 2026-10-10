CREATE TABLE users (
    id INTEGER PRIMARY KEY,
    display_name TEXT NOT NULL,
    created_at TEXT NOT NULL
);

INSERT INTO users (id, display_name, created_at)
VALUES (1, 'پیش‌فرض', strftime('%Y-%m-%dT%H:%M:%S', 'now', 'localtime'));

ALTER TABLE personnel ADD COLUMN created_by INTEGER REFERENCES users (id);
ALTER TABLE personnel ADD COLUMN updated_by INTEGER REFERENCES users (id);
UPDATE personnel SET created_by = 1 WHERE created_by IS NULL;
UPDATE personnel SET updated_by = 1 WHERE updated_by IS NULL;

ALTER TABLE clock_events ADD COLUMN created_by INTEGER REFERENCES users (id);
ALTER TABLE clock_events ADD COLUMN updated_by INTEGER REFERENCES users (id);
UPDATE clock_events SET created_by = 1 WHERE created_by IS NULL;
UPDATE clock_events SET updated_by = 1 WHERE updated_by IS NULL;

ALTER TABLE leaves ADD COLUMN created_by INTEGER REFERENCES users (id);
ALTER TABLE leaves ADD COLUMN updated_by INTEGER REFERENCES users (id);
UPDATE leaves SET created_by = 1 WHERE created_by IS NULL;
UPDATE leaves SET updated_by = 1 WHERE updated_by IS NULL;

ALTER TABLE calculation_days ADD COLUMN created_by INTEGER REFERENCES users (id);
ALTER TABLE calculation_days ADD COLUMN updated_by INTEGER REFERENCES users (id);
UPDATE calculation_days SET created_by = 1 WHERE created_by IS NULL;
UPDATE calculation_days SET updated_by = 1 WHERE updated_by IS NULL;

ALTER TABLE calculation_punches ADD COLUMN created_by INTEGER REFERENCES users (id);
ALTER TABLE calculation_punches ADD COLUMN updated_by INTEGER REFERENCES users (id);
UPDATE calculation_punches SET created_by = 1 WHERE created_by IS NULL;
UPDATE calculation_punches SET updated_by = 1 WHERE updated_by IS NULL;
