ALTER TABLE attendance RENAME TO clock_events;

DROP INDEX IF EXISTS attendance_remote_date_time;
CREATE INDEX IF NOT EXISTS clock_events_remote_date_time
    ON clock_events (remote_id, date, time);
