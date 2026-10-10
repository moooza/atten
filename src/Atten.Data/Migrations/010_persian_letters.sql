UPDATE personnel
SET
    first_name = REPLACE(REPLACE(REPLACE(first_name, 'ي', 'ی'), 'ى', 'ی'), 'ك', 'ک'),
    last_name = REPLACE(REPLACE(REPLACE(last_name, 'ي', 'ی'), 'ى', 'ی'), 'ك', 'ک');

UPDATE clock_events
SET name = REPLACE(REPLACE(REPLACE(name, 'ي', 'ی'), 'ى', 'ی'), 'ك', 'ک');
