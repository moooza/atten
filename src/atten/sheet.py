"""Pair device punches with manual times and compare them to daily hours.

Manual times and the balance live outside clock_events.
Approved leave fills a shortfall on the working days it covers.
A balance is hours and minutes: -2.20 is minus 2 hours and 20 minutes.
"""

from dataclasses import dataclass
from datetime import date, timedelta

from atten.dates import storage_date
from atten.leave import spread_leave_minutes

_DIGITS = str.maketrans("۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩", "01234567890123456789")
_INVALID_TIME = "ساعت معتبر نیست."


@dataclass(frozen=True)
class SheetRow:
    date: str
    slots: tuple[str | None, ...]
    incomplete: bool
    balance_minutes: int
    holiday: bool
    leave_minutes: int = 0
    on_leave: bool = False


def parse_clock(text: str) -> str | None:
    cleaned = text.strip().translate(_DIGITS)
    if not cleaned:
        return None
    pieces = cleaned.split(":")
    if len(pieces) not in (2, 3) or not all(piece.isdigit() for piece in pieces):
        raise ValueError(_INVALID_TIME)
    hour = int(pieces[0])
    minute = int(pieces[1])
    second = int(pieces[2]) if len(pieces) == 3 else 0
    if hour > 23 or minute > 59 or second > 59:
        raise ValueError(_INVALID_TIME)
    return f"{hour:02d}:{minute:02d}:{second:02d}"


def format_balance(minutes: int) -> str:
    sign = "+" if minutes > 0 else "-" if minutes < 0 else ""
    hours, mins = divmod(abs(minutes), 60)
    return f"{sign}{hours}.{mins:02d}"


def required_minutes(daily_hours: float) -> int:
    return int(round(float(daily_hours) * 60))


def build_sheet(
    start: date,
    end: date,
    device_rows: list[dict[str, object]],
    overrides: dict[str, dict[int, str | None]],
    daily_hours: float,
    holidays: dict[str, bool] | None = None,
    leaves: list[dict[str, object]] | None = None,
) -> tuple[SheetRow, ...]:
    start_key = storage_date(start)
    end_key = storage_date(end)
    if start_key > end_key:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    device = {str(row["date"]): tuple(str(value) for value in row["times"]) for row in device_rows}
    chosen = {} if holidays is None else holidays
    expected = required_minutes(daily_hours)
    recorded = [] if leaves is None else leaves
    leave_credit = spread_leave_minutes(recorded, lambda day: _is_holiday(day, chosen))
    rows: list[SheetRow] = []
    current = start
    while storage_date(current) <= end_key:
        key = storage_date(current)
        slots = _day_slots(device.get(key, ()), overrides.get(key, {}))
        holiday = _is_holiday(current, chosen)
        required = 0 if holiday else expected
        worked = _worked_minutes(slots)
        credited = leave_credit.get(key, 0)
        credit = min(credited, max(0, required - worked))
        absent = not any(slots)
        rows.append(
            SheetRow(
                date=key,
                slots=slots,
                incomplete=False if absent and credit >= required else _incomplete(slots, holiday),
                balance_minutes=worked + credit - required,
                holiday=holiday,
                leave_minutes=credited,
                on_leave=_overlaps_leave(key, recorded),
            )
        )
        current += timedelta(days=1)
    return tuple(rows)


def _is_holiday(day: date, chosen: dict[str, bool]) -> bool:
    key = storage_date(day)
    if key in chosen:
        return chosen[key]
    return day.weekday() == 4


def _overlaps_leave(day_key: str, leaves: list[dict[str, object]]) -> bool:
    for leave in leaves:
        start = _day_key(leave["start_date"])
        end = _day_key(leave["end_date"])
        if start <= day_key <= end:
            return True
    return False


def _day_key(value: object) -> str:
    if isinstance(value, date):
        return storage_date(value)
    return str(value)[:10]


def _day_slots(
    device: tuple[str, ...],
    overrides: dict[int, str | None],
) -> tuple[str | None, ...]:
    count = len(device)
    if count == 0:
        count = 2
    elif count % 2 == 1:
        count += 1
    if overrides:
        count = max(count, max(overrides) + 1)
    if count % 2 == 1:
        count += 1
    slots: list[str | None] = []
    for index in range(count):
        if index in overrides:
            slots.append(overrides[index])
        elif index < len(device):
            slots.append(device[index])
        else:
            slots.append(None)
    return tuple(slots)


def _incomplete(slots: tuple[str | None, ...], holiday: bool) -> bool:
    if holiday and not any(slots):
        return False
    if not slots:
        return True
    for index in range(0, len(slots), 2):
        entry = slots[index]
        exit_time = slots[index + 1] if index + 1 < len(slots) else None
        if not entry or not exit_time:
            return True
        if _clock_seconds(exit_time) <= _clock_seconds(entry):
            return True
    return False


def _worked_minutes(slots: tuple[str | None, ...]) -> int:
    total_seconds = 0
    for index in range(0, len(slots) - 1, 2):
        entry = slots[index]
        exit_time = slots[index + 1]
        if not entry or not exit_time:
            continue
        start = _clock_seconds(entry)
        end = _clock_seconds(exit_time)
        if end <= start:
            continue
        total_seconds += end - start
    return total_seconds // 60


def _clock_seconds(value: str) -> int:
    hour, minute, second = (int(part) for part in value.split(":"))
    return hour * 3600 + minute * 60 + second
