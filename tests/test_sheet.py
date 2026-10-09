from datetime import date

import pytest

from atten.sheet import build_sheet, format_balance, parse_clock, required_minutes


def test_balance_text_uses_minutes_after_the_dot():
    assert format_balance(-140) == "-2.20"
    assert format_balance(-238) == "-3.58"
    assert format_balance(180) == "+3.00"
    assert format_balance(59) == "+0.59"
    assert format_balance(60) == "+1.00"
    assert format_balance(119) == "+1.59"
    assert format_balance(120) == "+2.00"
    assert format_balance(0) == "0.00"
    assert required_minutes(8) == 480
    assert required_minutes(4.5) == 270


def test_sheet_includes_every_day_and_leaves_a_blank_exit_for_an_open_entry():
    device = [
        {"date": "2026-10-07", "times": ["08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00"]},
        {"date": "2026-10-08", "times": ["09:00:00"]},
    ]
    rows = build_sheet(date(2026, 10, 7), date(2026, 10, 9), device, {}, 8)

    assert [row.date for row in rows] == ["2026-10-07", "2026-10-08", "2026-10-09"]
    assert rows[0].slots == (
        "08:00:00",
        "12:00:00",
        "13:00:00",
        "17:00:00",
        "18:00:00",
        "21:00:00",
    )
    assert rows[0].incomplete is False
    assert rows[0].balance_minutes == 180
    assert format_balance(rows[0].balance_minutes) == "+3.00"
    assert rows[0].holiday is False
    assert rows[1].slots == ("09:00:00", None)
    assert rows[1].incomplete is True
    assert rows[1].holiday is False
    assert rows[1].balance_minutes == -480
    assert rows[2].slots == (None, None)
    assert rows[2].holiday is True
    assert rows[2].incomplete is False
    assert rows[2].balance_minutes == 0


def test_holiday_presence_is_overtime_and_a_cleared_friday_uses_daily_hours():
    present = build_sheet(
        date(2026, 10, 9),
        date(2026, 10, 9),
        [{"date": "2026-10-09", "times": ["09:00:00", "13:00:00"]}],
        {},
        8,
    )
    assert present[0].holiday is True
    assert present[0].balance_minutes == 240
    assert format_balance(present[0].balance_minutes) == "+4.00"

    working_friday = build_sheet(
        date(2026, 10, 9),
        date(2026, 10, 9),
        [],
        {},
        8,
        {"2026-10-09": False},
    )
    assert working_friday[0].holiday is False
    assert working_friday[0].balance_minutes == -480
    assert working_friday[0].incomplete is True

    ticked = build_sheet(
        date(2026, 10, 7),
        date(2026, 10, 7),
        [{"date": "2026-10-07", "times": ["08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00"]}],
        {},
        8,
        {"2026-10-07": True},
    )
    assert ticked[0].holiday is True
    assert ticked[0].balance_minutes == 660
    assert format_balance(ticked[0].balance_minutes) == "+11.00"


def test_manual_exit_and_half_hour_day_change_the_balance():
    device = [{"date": "2026-10-08", "times": ["09:00:00"]}]
    overrides = {"2026-10-08": {1: "17:30:00"}}
    rows = build_sheet(date(2026, 10, 8), date(2026, 10, 8), device, overrides, 8)
    assert rows[0].slots == ("09:00:00", "17:30:00")
    assert rows[0].incomplete is False
    assert rows[0].balance_minutes == 30
    assert format_balance(rows[0].balance_minutes) == "+0.30"

    short_day = build_sheet(
        date(2026, 10, 8),
        date(2026, 10, 8),
        [{"date": "2026-10-08", "times": ["08:00:00", "12:00:00"]}],
        {},
        4.5,
    )
    assert short_day[0].balance_minutes == -30
    assert format_balance(short_day[0].balance_minutes) == "-0.30"


def test_blank_override_hides_a_device_punch():
    device = [{"date": "2026-10-08", "times": ["09:00:00", "17:00:00"]}]
    rows = build_sheet(
        date(2026, 10, 8),
        date(2026, 10, 8),
        device,
        {"2026-10-08": {1: None}},
        8,
    )
    assert rows[0].slots == ("09:00:00", None)
    assert rows[0].incomplete is True


def test_same_day_leave_cancels_the_shortfall_and_does_not_become_overtime():
    day = date(2026, 9, 30)
    punches = [{"date": "2026-09-30", "times": ["09:43:10", "13:50:33"]}]
    daily_hours = 7 + 20 / 60
    bare = build_sheet(day, day, punches, {}, daily_hours)
    assert bare[0].balance_minutes == -193
    assert format_balance(bare[0].balance_minutes) == "-3.13"

    covered = build_sheet(
        day,
        day,
        punches,
        {},
        daily_hours,
        leaves=[{"start_date": "2026-09-30", "end_date": "2026-09-30", "minutes": 193}],
    )
    assert covered[0].balance_minutes == 0
    assert covered[0].incomplete is False
    assert covered[0].on_leave is True
    assert covered[0].leave_minutes == 193
    assert format_balance(covered[0].balance_minutes) == "0.00"
    assert bare[0].on_leave is False
    assert bare[0].leave_minutes == 0

    partial = build_sheet(
        day,
        day,
        punches,
        {},
        daily_hours,
        leaves=[{"start_date": day, "end_date": day, "minutes": 60}],
    )
    assert partial[0].balance_minutes == -133

    extra = build_sheet(
        day,
        day,
        punches,
        {},
        daily_hours,
        leaves=[{"start_date": day, "end_date": day, "minutes": 300}],
    )
    assert extra[0].balance_minutes == 0

    present = build_sheet(
        day,
        day,
        [{"date": "2026-09-30", "times": ["09:00:00", "17:00:00"]}],
        {},
        daily_hours,
        leaves=[{"start_date": day, "end_date": day, "minutes": 193}],
    )
    assert present[0].balance_minutes == 40
    assert format_balance(present[0].balance_minutes) == "+0.40"


def test_multi_day_leave_covers_each_absent_working_day():
    daily_hours = 7 + 20 / 60
    day_minutes = required_minutes(daily_hours)
    rows = build_sheet(
        date(2026, 10, 5),
        date(2026, 10, 8),
        [],
        {},
        daily_hours,
        leaves=[
            {
                "start_date": "2026-10-05",
                "end_date": "2026-10-07",
                "minutes": 3 * day_minutes,
            }
        ],
    )
    assert [row.balance_minutes for row in rows] == [0, 0, 0, -day_minutes]
    assert [row.leave_minutes for row in rows] == [day_minutes, day_minutes, day_minutes, 0]
    assert [row.on_leave for row in rows] == [True, True, True, False]
    assert [row.incomplete for row in rows] == [False, False, False, True]
    assert rows[3].holiday is False

    window = build_sheet(
        date(2026, 10, 7),
        date(2026, 10, 7),
        [],
        {},
        daily_hours,
        leaves=[{"start_date": "2026-10-05", "end_date": "2026-10-07", "minutes": 300}],
    )
    assert window[0].balance_minutes == 100 - day_minutes

    across_friday = build_sheet(
        date(2026, 10, 8),
        date(2026, 10, 10),
        [],
        {},
        daily_hours,
        leaves=[
            {
                "start_date": "2026-10-08",
                "end_date": "2026-10-10",
                "minutes": 2 * day_minutes,
            }
        ],
    )
    assert [row.holiday for row in across_friday] == [False, True, False]
    assert [row.on_leave for row in across_friday] == [True, True, True]
    assert [row.leave_minutes for row in across_friday] == [day_minutes, 0, day_minutes]
    assert [row.balance_minutes for row in across_friday] == [0, 0, 0]
    assert [row.incomplete for row in across_friday] == [False, False, False]


def test_parse_clock_accepts_persian_digits_and_rejects_bad_times():
    assert parse_clock(" ۱۷:۳۰ ") == "17:30:00"
    assert parse_clock("۰۸:۰۵:۰۹") == "08:05:09"
    assert parse_clock("  ") is None
    with pytest.raises(ValueError, match="ساعت معتبر نیست"):
        parse_clock("3.60")
    with pytest.raises(ValueError, match="ساعت معتبر نیست"):
        parse_clock("24:00")
