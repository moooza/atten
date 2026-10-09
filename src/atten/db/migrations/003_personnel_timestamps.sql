ALTER TABLE personnel ADD COLUMN created_at TEXT;
ALTER TABLE personnel ADD COLUMN updated_at TEXT;

UPDATE personnel
SET created_at = strftime('%Y-%m-%dT%H:%M:%S', 'now', 'localtime')
WHERE created_at IS NULL OR created_at = '';

UPDATE personnel
SET updated_at = created_at
WHERE updated_at IS NULL OR updated_at = '';
