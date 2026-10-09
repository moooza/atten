from datetime import date, datetime

import pytest

from atten.dates import (
    format_shamsi_date,
    format_shamsi_datetime,
    parse_shamsi_date,
    parse_shamsi_datetime,
    format_shamsi_weekday,
    shamsi_month_dates,
    shamsi_week_index,
    storage_date,
    storage_datetime,
)


def test_known_civil_dates_convert_both_ways():
    pairs = [
        (date(2026, 10, 7), "1405/07/15"),
        (date(2026, 3, 21), "1405/01/01"),
        (date(2025, 3, 21), "1404/01/01"),
        (date(2025, 3, 20), "1403/12/30"),
        (date(2024, 3, 20), "1403/01/01"),
        (date(1979, 2, 11), "1357/11/22"),
    ]
    for gregorian, shamsi in pairs:
        assert format_shamsi_date(gregorian) == shamsi
        assert format_shamsi_date(storage_date(gregorian)) == shamsi
        assert parse_shamsi_date(shamsi) == gregorian
        assert storage_date(parse_shamsi_date(shamsi)) == storage_date(gregorian)


def test_shamsi_input_accepts_persian_digits_and_stores_gregorian_datetime():
    moment = parse_shamsi_datetime("۱۴۰۵/۰۷/۱۵ ۱۶:۳۰")
    assert moment == datetime(2026, 10, 7, 16, 30, 0)
    assert storage_datetime(moment) == "2026-10-07T16:30:00"
    assert format_shamsi_datetime(storage_datetime(moment)) == "1405/07/15 16:30"
    assert format_shamsi_datetime(storage_datetime(moment), seconds=True) == "1405/07/15 16:30:00"


def test_shamsi_month_starts_on_saturday_index_and_lists_mehr_1405():
    days = shamsi_month_dates(1405, 7)
    assert len(days) == 30
    assert days[0] == date(2026, 9, 23)
    assert days[14] == date(2026, 10, 7)
    assert format_shamsi_date(days[-1]) == "1405/07/30"
    assert shamsi_week_index(date(2026, 10, 7)) == 4
    assert format_shamsi_weekday(date(2026, 10, 7)) == "چهارشنبه"
    assert format_shamsi_weekday("2026-10-09") == "جمعه"
    assert format_shamsi_weekday("2026-10-10") == "شنبه"


def test_invalid_shamsi_dates_are_rejected():
    with pytest.raises(ValueError, match="تاریخ شمسی معتبر نیست"):
        parse_shamsi_date("1404/12/30")
    with pytest.raises(ValueError, match="ساعت معتبر نیست"):
        parse_shamsi_datetime("1405/07/15")
