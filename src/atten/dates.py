"""Shamsi on screen, Gregorian in the database and on the wire.

Stored dates are text: ``YYYY-MM-DD``.
Stored datetimes are text: ``YYYY-MM-DDTHH:MM:SS``.
Both are Gregorian. Time is the civil clock time, with no timezone shift.
"""

from datetime import date, datetime, timedelta

from atten.persian import english_digits

_GREGORIAN_MONTH_DAYS = (31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)
_JALALI_MONTH_DAYS = (31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29)
_INVALID_DATE = "تاریخ شمسی معتبر نیست."
_INVALID_TIME = "ساعت معتبر نیست."


def storage_date(value: date) -> str:
    return value.strftime("%Y-%m-%d")


def storage_datetime(value: datetime) -> str:
    return value.strftime("%Y-%m-%dT%H:%M:%S")


def format_shamsi_date(value: date | datetime | str) -> str:
    gregorian = _as_date(value)
    year, month, day = _gregorian_to_jalali(gregorian.year, gregorian.month, gregorian.day)
    return f"{year:04d}/{month:02d}/{day:02d}"


def format_shamsi_datetime(value: datetime | str, *, seconds: bool = False) -> str:
    moment = _as_datetime(value)
    clock = moment.strftime("%H:%M:%S") if seconds else _format_clock(moment)
    return f"{format_shamsi_date(moment.date())} {clock}"


def parse_shamsi_date(text: str) -> date:
    year, month, day = _shamsi_parts(text)
    return _jalali_to_date(year, month, day)


def parse_shamsi_datetime(text: str) -> datetime:
    cleaned = _normalize(text)
    if not cleaned:
        raise ValueError(_INVALID_DATE)
    date_text, separator, time_text = cleaned.partition(" ")
    if separator == "" or not time_text:
        raise ValueError(_INVALID_TIME)
    gregorian = parse_shamsi_date(date_text)
    hour, minute, second = _clock_parts(time_text)
    return datetime(gregorian.year, gregorian.month, gregorian.day, hour, minute, second)


def _gregorian_to_jalali(year: int, month: int, day: int) -> tuple[int, int, int]:
    gy = year - 1600
    gm = month - 1
    day_number = 365 * gy + (gy + 3) // 4 - (gy + 99) // 100 + (gy + 399) // 400
    day_number += sum(_GREGORIAN_MONTH_DAYS[:gm]) + day - 80
    if gm > 1 and _gregorian_leap(year):
        day_number += 1

    cycles, day_number = divmod(day_number, 12053)
    jy = 979 + 33 * cycles + 4 * (day_number // 1461)
    day_number %= 1461
    if day_number >= 366:
        day_number -= 1
        jy += day_number // 365
        day_number %= 365

    for index, length in enumerate(_JALALI_MONTH_DAYS[:-1]):
        if day_number < length:
            return jy, index + 1, day_number + 1
        day_number -= length
    return jy, 12, day_number + 1


def _jalali_to_date(year: int, month: int, day: int) -> date:
    if month < 1 or month > 12 or day < 1 or day > 31:
        raise ValueError(_INVALID_DATE)
    gy = year - 979
    day_number = 365 * gy + (gy // 33) * 8 + (gy % 33 + 3) // 4 + 78 + day
    day_number += sum(_JALALI_MONTH_DAYS[: month - 1])

    gy = 1600 + 400 * (day_number // 146097)
    day_number %= 146097
    leap = True
    if day_number >= 36525:
        day_number -= 1
        gy += 100 * (day_number // 36524)
        day_number %= 36524
        if day_number >= 365:
            day_number += 1
        else:
            leap = False

    gy += 4 * (day_number // 1461)
    day_number %= 1461
    if day_number >= 366:
        leap = False
        day_number -= 1
        gy += day_number // 365
        day_number %= 365

    month_index = 0
    while month_index < 12:
        length = _GREGORIAN_MONTH_DAYS[month_index] + (month_index == 1 and leap)
        if day_number < length:
            break
        day_number -= length
        month_index += 1
    else:
        raise ValueError(_INVALID_DATE)

    gregorian = date(gy, month_index + 1, day_number + 1)
    if _gregorian_to_jalali(gregorian.year, gregorian.month, gregorian.day) != (year, month, day):
        raise ValueError(_INVALID_DATE)
    return gregorian


def _gregorian_leap(year: int) -> bool:
    return year % 4 == 0 and (year % 100 != 0 or year % 400 == 0)


def _as_date(value: date | datetime | str) -> date:
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    text = _normalize(str(value)).split(" ")[0].replace("/", "-")
    try:
        return date.fromisoformat(text[:10])
    except ValueError as exc:
        raise ValueError(_INVALID_DATE) from exc


def _as_datetime(value: datetime | str) -> datetime:
    if isinstance(value, datetime):
        return value
    text = _normalize(str(value)).replace("/", "-")
    date_text, separator, time_text = text.partition("T")
    if separator == "":
        date_text, separator, time_text = text.partition(" ")
    if separator == "" or not time_text:
        raise ValueError(_INVALID_TIME)
    try:
        gregorian = date.fromisoformat(date_text)
    except ValueError as exc:
        raise ValueError(_INVALID_DATE) from exc
    hour, minute, second = _clock_parts(time_text)
    return datetime(gregorian.year, gregorian.month, gregorian.day, hour, minute, second)


def _shamsi_parts(text: str) -> tuple[int, int, int]:
    cleaned = _normalize(text).split(" ")[0].replace("-", "/").replace(".", "/")
    pieces = cleaned.split("/")
    if len(pieces) != 3 or not all(piece.isdigit() for piece in pieces):
        raise ValueError(_INVALID_DATE)
    year, month, day = (int(piece) for piece in pieces)
    if year < 1:
        raise ValueError(_INVALID_DATE)
    return year, month, day


def _clock_parts(text: str) -> tuple[int, int, int]:
    pieces = text.split(":")
    if len(pieces) not in (2, 3) or not all(piece.isdigit() for piece in pieces):
        raise ValueError(_INVALID_TIME)
    hour = int(pieces[0])
    minute = int(pieces[1])
    second = int(pieces[2]) if len(pieces) == 3 else 0
    if hour > 23 or minute > 59 or second > 59:
        raise ValueError(_INVALID_TIME)
    return hour, minute, second


SHAMSI_MONTH_NAMES = (
    "فروردین",
    "اردیبهشت",
    "خرداد",
    "تیر",
    "مرداد",
    "شهریور",
    "مهر",
    "آبان",
    "آذر",
    "دی",
    "بهمن",
    "اسفند",
)


def shamsi_ymd(value: date | datetime | str) -> tuple[int, int, int]:
    year, month, day = (int(part) for part in format_shamsi_date(value).split("/"))
    return year, month, day


def shamsi_week_index(value: date | datetime | str) -> int:
    """Saturday is 0 and Friday is 6."""
    return (_as_date(value).weekday() + 2) % 7


def shamsi_month_dates(year: int, month: int) -> tuple[date, ...]:
    if year < 1 or month < 1 or month > 12:
        raise ValueError(_INVALID_DATE)
    current = parse_shamsi_date(f"{year:04d}/{month:02d}/01")
    days: list[date] = []
    while True:
        shown_year, shown_month, _day = shamsi_ymd(current)
        if (shown_year, shown_month) != (year, month):
            break
        days.append(current)
        current += timedelta(days=1)
        if len(days) > 31:
            raise ValueError(_INVALID_DATE)
    return tuple(days)


def _format_clock(value: datetime) -> str:
    if value.second:
        return value.strftime("%H:%M:%S")
    return value.strftime("%H:%M")


def _normalize(text: str) -> str:
    return " ".join(english_digits(text.strip()).split())
