"""Yearly leave is counted in minutes. One leave day is 7 hours and 20 minutes.

A full month earns 2.5 days. At most 9 days carry into the next cooperation year.
The year cooperation ends carries nothing. The balance after the transfer is
the remainder minus the days that carry.
"""

from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from datetime import date, datetime, timedelta

from atten.dates import parse_shamsi_date, shamsi_month_dates, shamsi_ymd, storage_date

LEAVE_DAY_MINUTES = 7 * 60 + 20
YEARLY_LEAVE_DAYS = 30
YEARLY_LEAVE_MINUTES = YEARLY_LEAVE_DAYS * LEAVE_DAY_MINUTES
MONTHLY_LEAVE_MINUTES = LEAVE_DAY_MINUTES * 5 // 2
CARRY_LEAVE_DAYS = 9
CARRY_LEAVE_MINUTES = CARRY_LEAVE_DAYS * LEAVE_DAY_MINUTES


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


def month_leave_minutes(covered_days: int, month_length: int) -> int:
    """Minutes earned in one month. A full month is 2.5 days.

    A partial month rounds to the nearest minute:
    ``(covered_days * 1100 + month_length // 2) // month_length``.
    """
    days = int(covered_days)
    length = int(month_length)
    if days <= 0 or length <= 0:
        return 0
    if days >= length:
        return MONTHLY_LEAVE_MINUTES
    return (days * MONTHLY_LEAVE_MINUTES + length // 2) // length


def earned_leave_minutes(
    year: int,
    cooperation_start: date | datetime | str | None,
    cooperation_end: date | datetime | str | None = None,
) -> int:
    """Minutes earned in one Shamsi year inside the cooperation window.

    Both ends of the window are included. Calendar days count, including
    Fridays and holidays. An empty end runs through the last day of ``year``.
    An empty start earns nothing.
    """
    started = _optional_date(cooperation_start)
    if started is None:
        return 0
    year_start, year_end = shamsi_year_span(int(year))
    ended = _optional_date(cooperation_end)
    if ended is None:
        ended = year_end
    window_start = max(started, year_start)
    window_end = min(ended, year_end)
    if window_end < window_start:
        return 0
    total = 0
    for month in range(1, 13):
        month_days = shamsi_month_dates(int(year), month)
        overlap_start = max(window_start, month_days[0])
        overlap_end = min(window_end, month_days[-1])
        if overlap_end < overlap_start:
            continue
        covered = (overlap_end - overlap_start).days + 1
        total += month_leave_minutes(covered, len(month_days))
    return total


@dataclass(frozen=True)
class LeaveYearSettlement:
    year: int
    earned: int
    carry_in: int
    used: int
    usable: int
    remaining: int
    carry_out: int


def settle_leave_years(
    years: Iterable[tuple[int, int, int]],
    closing_year: int | None = None,
) -> tuple[LeaveYearSettlement, ...]:
    """Settle earned and used minutes from the first cooperation year forward.

    Each item is ``(shamsi_year, earned_minutes, used_minutes)``. The same
    year may appear more than once; its minutes are added together.
    ``carry_in`` is the previous year's ``carry_out`` only when that year is
    the immediately previous Shamsi year. A missing year drops the carry.
    A positive remainder carries at most 9 days. The year equal to
    ``closing_year`` carries nothing. The balance after the transfer is
    ``remaining - carry_out`` and is not stored. A zero or negative remainder
    carries nothing.
    """
    combined: dict[int, tuple[int, int]] = {}
    for year, earned, used in years:
        key = int(year)
        previous_earned, previous_used = combined.get(key, (0, 0))
        combined[key] = (previous_earned + int(earned), previous_used + int(used))

    close = None if closing_year is None else int(closing_year)
    incoming = 0
    previous_year: int | None = None
    settled: list[LeaveYearSettlement] = []
    for year in sorted(combined):
        earned, used = combined[year]
        carry_in = incoming if previous_year is not None and year == previous_year + 1 else 0
        usable = earned + carry_in
        remaining = usable - used
        if remaining > 0 and year != close:
            carry_out = min(remaining, CARRY_LEAVE_MINUTES)
        else:
            carry_out = 0
        settled.append(
            LeaveYearSettlement(
                year=year,
                earned=earned,
                carry_in=carry_in,
                used=used,
                usable=usable,
                remaining=remaining,
                carry_out=carry_out,
            )
        )
        incoming = carry_out
        previous_year = year
    return tuple(settled)


def remaining_leave_minutes(
    year: int,
    cooperation_start: date | datetime | str | None,
    cooperation_end: date | datetime | str | None,
    used_by_year: Mapping[int, int],
) -> int:
    """Minutes still available in one Shamsi year after earlier years settle.

    An empty cooperation start earns nothing. Only years that overlap the
    cooperation window are settled, so a gap drops the carry. A recorded
    cooperation end makes that Shamsi year the closing year, so it carries
    nothing forward. The returned minutes are the balance before that
    transfer. ``used_by_year`` maps a Shamsi year to minutes of leave that
    start in that year.
    """
    target = int(year)
    used_here = int(used_by_year.get(target, 0))
    started = _optional_date(cooperation_start)
    if started is None or target < shamsi_year_of(started):
        return -used_here
    ended = _optional_date(cooperation_end)
    closing_year = None if ended is None else shamsi_year_of(ended)
    entries: list[tuple[int, int, int]] = []
    for candidate in range(shamsi_year_of(started), target + 1):
        earned = earned_leave_minutes(candidate, cooperation_start, cooperation_end)
        if earned <= 0:
            continue
        entries.append((candidate, earned, int(used_by_year.get(candidate, 0))))
    for row in settle_leave_years(entries, closing_year):
        if row.year == target:
            return row.remaining
    return -used_here


def compose_leave_minutes(days: int, hours: int, minutes: int) -> int:
    return int(days) * LEAVE_DAY_MINUTES + int(hours) * 60 + int(minutes)


def split_leave_minutes(minutes: int) -> tuple[int, int, int]:
    total = max(int(minutes), 0)
    days, rest = divmod(total, LEAVE_DAY_MINUTES)
    hours, mins = divmod(rest, 60)
    return days, hours, mins


def format_leave_amount(minutes: int) -> str:
    days, hours, mins = split_leave_minutes(minutes)
    return f"{days} روز و {hours} ساعت و {mins} دقیقه"


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


def _optional_date(value: date | datetime | str | None) -> date | None:
    if value is None:
        return None
    if isinstance(value, str) and not value.strip():
        return None
    return _as_date(value)
