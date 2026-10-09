"""Yearly leave is counted in minutes. One leave day is 7 hours and 20 minutes."""

from datetime import date, datetime, timedelta

from atten.dates import parse_shamsi_date, shamsi_ymd, storage_date

LEAVE_DAY_MINUTES = 7 * 60 + 20
YEARLY_LEAVE_DAYS = 30
YEARLY_LEAVE_MINUTES = YEARLY_LEAVE_DAYS * LEAVE_DAY_MINUTES


def shamsi_year_of(value: date | datetime | str) -> int:
    year, _month, _day = shamsi_ymd(value)
    return year


def shamsi_year_span(year: int) -> tuple[date, date]:
    start = parse_shamsi_date(f"{year:04d}/01/01")
    try:
        end = parse_shamsi_date(f"{year:04d}/12/30")
    except ValueError:
        end = parse_shamsi_date(f"{year:04d}/12/29")
    return start, end


def compose_leave_minutes(days: int, hours: int, minutes: int) -> int:
    return int(days) * LEAVE_DAY_MINUTES + int(hours) * 60 + int(minutes)


def split_leave_minutes(minutes: int) -> tuple[int, int, int]:
    total = max(int(minutes), 0)
    days, rest = divmod(total, LEAVE_DAY_MINUTES)
    hours, mins = divmod(rest, 60)
    return days, hours, mins


def format_leave_amount(minutes: int) -> str:
    days, hours, mins = split_leave_minutes(minutes)
    return f"{days} روز، {hours} ساعت و {mins} دقیقه"


def format_leave_duration(minutes: int) -> str:
    total = int(minutes)
    if total <= 0:
        return ""
    hours, mins = divmod(total, 60)
    if hours and mins:
        return f"{hours} ساعت و {mins} دقیقه"
    if hours:
        return f"{hours} ساعت"
    return f"{mins} دقیقه"


def spread_leave_minutes(
    leaves: list[dict[str, object]],
    is_holiday,
) -> dict[str, int]:
    """Split each leave across the working days it covers."""
    credit: dict[str, int] = {}
    for leave in leaves:
        start = _as_date(leave["start_date"])
        end = _as_date(leave["end_date"])
        amount = int(leave["minutes"])
        if amount <= 0 or end < start:
            continue
        working: list[str] = []
        current = start
        while current <= end:
            if not is_holiday(current):
                working.append(storage_date(current))
            current += timedelta(days=1)
        if not working:
            continue
        share, extra = divmod(amount, len(working))
        for index, key in enumerate(working):
            credit[key] = credit.get(key, 0) + share + (1 if index < extra else 0)
    return credit


def _as_date(value: object) -> date:
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    return date.fromisoformat(str(value)[:10])
