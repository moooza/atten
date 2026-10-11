ALTER TABLE calculation_days ADD COLUMN holiday_override INTEGER;

-- Friday that was cleared is an explicit working day.
UPDATE calculation_days
SET holiday_override = 0
WHERE holiday = 0 AND strftime('%w', date) = '5';

-- A non-Friday that was ticked is an explicit holiday.
UPDATE calculation_days
SET holiday_override = 1
WHERE holiday = 1 AND strftime('%w', date) != '5';
