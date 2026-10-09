from datetime import date

import pytest

from atten.dates import parse_shamsi_date
from atten.db.connection import connect
from atten.db.migrate import migrate
from atten.db.repository import (
    add_clock_event,
    add_leave,
    add_personnel,
    clear_work_punches,
    list_clock_events,
    list_daily_punches,
    list_leaves,
    list_personnel,
    list_work_balances,
    list_work_punches,
    save_work_balances,
    save_work_punch,
    sum_leave_minutes,
    ping,
    update_leave,
    update_personnel,
)
from atten.leave import compose_leave_minutes, shamsi_year_span


def test_migrate_creates_schema_and_is_idempotent(tmp_path):
    db_file = tmp_path / "atten.db"
    first = migrate(db_file)
    second = migrate(db_file)

    assert first == ["001", "002", "003", "004", "005", "006", "007", "008", "009", "010"]
    assert second == []

    with connect(db_file) as conn:
        versions = [row["version"] for row in conn.execute("SELECT version FROM schema_migrations")]
        journal = conn.execute("PRAGMA journal_mode").fetchone()[0]
        synchronous = conn.execute("PRAGMA synchronous").fetchone()[0]
        foreign_keys = conn.execute("PRAGMA foreign_keys").fetchone()[0]
        columns = [
            row["name"]
            for row in conn.execute("PRAGMA table_info(personnel)")
        ]
        clock_event_columns = [
            row["name"]
            for row in conn.execute("PRAGMA table_info(clock_events)")
        ]
        clock_event_keys = list(conn.execute("PRAGMA foreign_key_list(clock_events)"))
        calculation_day_columns = [
            row["name"]
            for row in conn.execute("PRAGMA table_info(calculation_days)")
        ]
        calculation_punch_columns = [
            row["name"]
            for row in conn.execute("PRAGMA table_info(calculation_punches)")
        ]
        leave_columns = [
            row["name"]
            for row in conn.execute("PRAGMA table_info(leaves)")
        ]

        assert versions == ["001", "002", "003", "004", "005", "006", "007", "008", "009", "010"]
    assert journal.lower() == "wal"
    assert synchronous == 2
    assert foreign_keys == 1
    assert clock_event_keys == []
    assert columns == [
        "id",
        "remote_id",
        "first_name",
        "last_name",
        "daily_hours",
        "mobile",
        "created_at",
        "updated_at",
    ]
    assert clock_event_columns == [
        "id",
        "remote_id",
        "name",
        "date",
        "time",
        "created_at",
        "updated_at",
    ]
    assert calculation_day_columns == [
        "id",
        "remote_id",
        "date",
        "balance_minutes",
        "created_at",
        "updated_at",
        "holiday",
    ]
    assert calculation_punch_columns == [
        "id",
        "remote_id",
        "date",
        "slot",
        "time",
        "created_at",
        "updated_at",
    ]
    assert leave_columns == [
        "id",
        "personnel_id",
        "start_date",
        "end_date",
        "minutes",
        "created_at",
        "updated_at",
    ]


def test_personnel_stores_daily_hours_as_a_number(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)

    with connect(db_file) as conn:
        conn.execute(
            """
            INSERT INTO personnel (remote_id, first_name, last_name, daily_hours, mobile)
            VALUES (?, ?, ?, ?, ?)
            """,
            ("dev-1", "علی", "رضایی", 4.5, "09120000000"),
        )
        conn.execute(
            """
            INSERT INTO personnel (remote_id, first_name, last_name, daily_hours, mobile)
            VALUES (?, ?, ?, ?, ?)
            """,
            ("dev-2", "مریم", "احمدی", 8, None),
        )
        conn.commit()
        rows = conn.execute(
            "SELECT remote_id, first_name, last_name, daily_hours, mobile FROM personnel ORDER BY id"
        ).fetchall()

    assert [tuple(row) for row in rows] == [
        ("dev-1", "علی", "رضایی", 4.5, "09120000000"),
        ("dev-2", "مریم", "احمدی", 8, None),
    ]


def test_list_personnel_returns_saved_rows(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    with connect(db_file) as conn:
        conn.execute(
            """
            INSERT INTO personnel (remote_id, first_name, last_name, daily_hours, mobile)
            VALUES (?, ?, ?, ?, ?)
            """,
            ("dev-1", "علی", "رضایی", 4.5, "09120000000"),
        )
        conn.commit()

    rows = list_personnel(db_file)

    assert rows == [
        {
            "id": 1,
            "remote_id": "dev-1",
            "first_name": "علی",
            "last_name": "رضایی",
            "daily_hours": 4.5,
            "mobile": "09120000000",
            "created_at": None,
            "updated_at": None,
        }
    ]


def test_add_personnel_inserts_a_row(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")

    new_id = add_personnel("علی", "رضایی", 4.5, remote_id="dev-1", mobile="09120000000", db_file=db_file)

    assert new_id == 1
    assert list_personnel(db_file) == [
        {
            "id": 1,
            "remote_id": "dev-1",
            "first_name": "علی",
            "last_name": "رضایی",
            "daily_hours": 4.5,
            "mobile": "09120000000",
            "created_at": "2026-10-07T16:30:00",
            "updated_at": "2026-10-07T16:30:00",
        }
    ]


def test_personnel_and_clock_names_are_stored_with_persian_letters(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")

    add_personnel(
        "عل\u064a",
        "\u0643اظم\u0649",
        8,
        remote_id="dev-1",
        db_file=db_file,
    )
    add_clock_event("dev-1", "عل\u064a \u0643اظمي", "2026-10-07", "08:30:00", db_file=db_file)

    person = list_personnel(db_file)[0]
    assert person["first_name"] == "علی"
    assert person["last_name"] == "کاظمی"
    assert list_clock_events(db_file)[0]["name"] == "علی کاظمی"


def test_persian_letter_migration_rewrites_existing_names(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    with connect(db_file) as conn:
        conn.execute("DELETE FROM schema_migrations WHERE version = '010'")
        conn.execute(
            """
            INSERT INTO personnel (
                remote_id, first_name, last_name, daily_hours, created_at, updated_at
            )
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                "dev-1",
                "عل\u064a",
                "\u0643اظمي",
                8,
                "2026-10-07T16:30:00",
                "2026-10-07T16:30:00",
            ),
        )
        conn.execute(
            """
            INSERT INTO clock_events (remote_id, name, date, time, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                "dev-1",
                "عل\u064a \u0643اظمي",
                "2026-10-07",
                "08:30:00",
                "2026-10-07T16:30:00",
                "2026-10-07T16:30:00",
            ),
        )
        conn.commit()

    assert migrate(db_file) == ["010"]

    person = list_personnel(db_file)[0]
    assert person["first_name"] == "علی"
    assert person["last_name"] == "کاظمی"
    assert list_clock_events(db_file)[0]["name"] == "علی کاظمی"


def test_add_personnel_rejects_blank_name_and_duplicate_remote_id(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)

    try:
        add_personnel("  ", "رضایی", 8, db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "نام را وارد کنید."
    else:
        raise AssertionError("blank first name was stored")

    try:
        add_personnel("مریم", "احمدی", 8, remote_id="dev-1", db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "این کد پرسنلی قبلاً ثبت شده است."
    else:
        raise AssertionError("duplicate remote id was stored")

    assert len(list_personnel(db_file)) == 1


def test_update_personnel_changes_the_same_row(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    first = add_personnel("علی", "رضایی", 4.5, remote_id="dev-1", mobile="09120000000", db_file=db_file)
    add_personnel("مریم", "احمدی", 8, remote_id="dev-2", db_file=db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T18:05:00")

    update_personnel(first, "علی", "کاظمی", 8, remote_id="dev-1", mobile="", db_file=db_file)

    rows = list_personnel(db_file)
    assert rows[0] == {
        "id": first,
        "remote_id": "dev-1",
        "first_name": "علی",
        "last_name": "کاظمی",
        "daily_hours": 8,
        "mobile": None,
        "created_at": "2026-10-07T16:30:00",
        "updated_at": "2026-10-07T18:05:00",
    }
    assert rows[1]["remote_id"] == "dev-2"
    assert rows[1]["created_at"] == "2026-10-07T16:30:00"
    assert rows[1]["updated_at"] == "2026-10-07T16:30:00"

    try:
        update_personnel(first, "علی", "کاظمی", 8, remote_id="dev-2", db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "این کد پرسنلی قبلاً ثبت شده است."
    else:
        raise AssertionError("duplicate remote id was stored")


def test_add_clock_event_stores_a_punch_for_a_known_remote_id(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)

    new_id = add_clock_event(
        "dev-1",
        "علی رضایی",
        date(2026, 10, 7),
        "8:30",
        db_file=db_file,
    )

    assert new_id == 1
    assert list_clock_events(db_file) == [
        {
            "id": 1,
            "remote_id": "dev-1",
            "name": "علی رضایی",
            "date": "2026-10-07",
            "time": "08:30:00",
            "created_at": "2026-10-07T16:30:00",
            "updated_at": "2026-10-07T16:30:00",
        }
    ]


def test_add_clock_event_stores_an_unknown_remote_id_and_rejects_bad_clock(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)

    new_id = add_clock_event("missing", "کسی", "2026-10-07", "08:30:00", db_file=db_file)
    assert new_id == 1
    assert list_clock_events(db_file)[0]["remote_id"] == "missing"
    assert list_personnel(db_file) == []

    try:
        add_clock_event("dev-1", "  ", "2026-10-07", "08:30:00", db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "نام را وارد کنید."
    else:
        raise AssertionError("blank name was stored")

    try:
        add_clock_event("dev-1", "علی", "1405/07/15", "08:30:00", db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "تاریخ معتبر نیست."
    else:
        raise AssertionError("shamsi date was stored")

    try:
        add_clock_event("dev-1", "علی", "2026-10-07", "25:00", db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "ساعت معتبر نیست."
    else:
        raise AssertionError("invalid time was stored")

    assert len(list_clock_events(db_file)) == 1


def test_list_daily_punches_groups_one_person_inside_the_range(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)
    add_personnel("مریم", "احمدی", 8, remote_id="dev-2", db_file=db_file)
    add_clock_event("dev-1", "علی", "2026-10-06", "07:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی", "2026-10-07", "12:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی", "2026-10-07", "08:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی", "2026-10-08", "09:15:00", db_file=db_file)
    add_clock_event("dev-2", "مریم", "2026-10-07", "10:00:00", db_file=db_file)

    assert list_daily_punches("dev-1", date(2026, 10, 7), date(2026, 10, 8), db_file=db_file) == [
        {"date": "2026-10-07", "times": ["08:00:00", "12:00:00"]},
        {"date": "2026-10-08", "times": ["09:15:00"]},
    ]
    assert list_daily_punches("dev-1", "2026-10-09", "2026-10-09", db_file=db_file) == []

    try:
        list_daily_punches("dev-1", date(2026, 10, 8), date(2026, 10, 7), db_file=db_file)
    except ValueError as exc:
        assert str(exc) == "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد."
    else:
        raise AssertionError("reversed range was accepted")


def test_work_sheet_stores_edits_without_changing_clock_events(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)
    add_clock_event("dev-1", "علی", "2026-10-08", "09:00:00", db_file=db_file)
    before = list_clock_events(db_file)

    save_work_punch("dev-1", date(2026, 10, 8), 1, "17:30", db_file=db_file)
    save_work_punch("dev-1", "2026-10-08", 0, None, db_file=db_file)
    save_work_balances(
        "dev-1",
        [("2026-10-08", -480, False), ("2026-10-09", 30, True)],
        db_file=db_file,
    )

    assert list_clock_events(db_file) == before
    assert list_work_punches("dev-1", "2026-10-08", "2026-10-09", db_file=db_file) == {
        "2026-10-08": {0: None, 1: "17:30:00"},
    }
    assert list_work_balances("dev-1", date(2026, 10, 8), date(2026, 10, 9), db_file=db_file) == [
        {"remote_id": "dev-1", "date": "2026-10-08", "balance_minutes": -480, "holiday": 0},
        {"remote_id": "dev-1", "date": "2026-10-09", "balance_minutes": 30, "holiday": 1},
    ]


def test_clear_work_punches_drops_one_day_and_leaves_clock_events(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_clock_event("dev-1", "علی", "2026-10-08", "09:00:00", db_file=db_file)
    before = list_clock_events(db_file)
    save_work_punch("dev-1", "2026-10-08", 1, "17:30", db_file=db_file)
    save_work_punch("dev-1", "2026-10-09", 0, "08:00", db_file=db_file)
    save_work_punch("dev-2", "2026-10-08", 0, "10:00", db_file=db_file)

    clear_work_punches("dev-1", "2026-10-08", db_file=db_file)

    assert list_clock_events(db_file) == before
    assert list_work_punches("dev-1", "2026-10-08", "2026-10-09", db_file=db_file) == {
        "2026-10-09": {0: "08:00:00"},
    }
    assert list_work_punches("dev-2", "2026-10-08", "2026-10-08", db_file=db_file) == {
        "2026-10-08": {0: "10:00:00"},
    }


def test_old_attendance_table_is_renamed_and_keeps_rows(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    with connect(db_file) as conn:
        conn.execute("ALTER TABLE clock_events RENAME TO attendance")
        conn.execute("DROP INDEX IF EXISTS clock_events_remote_date_time")
        conn.execute(
            """
            CREATE INDEX attendance_remote_date_time
            ON attendance (remote_id, date, time)
            """
        )
        conn.execute("DELETE FROM schema_migrations WHERE version = '005'")
        conn.execute(
            """
            INSERT INTO personnel (remote_id, first_name, last_name, daily_hours)
            VALUES (?, ?, ?, ?)
            """,
            ("dev-1", "علی", "رضایی", 8),
        )
        conn.execute(
            """
            INSERT INTO attendance (remote_id, name, date, time, created_at, updated_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            ("dev-1", "علی رضایی", "2026-10-07", "08:30:00", "2026-10-07T16:30:00", "2026-10-07T16:30:00"),
        )
        conn.commit()

    assert migrate(db_file) == ["005"]
    assert list_clock_events(db_file) == [
        {
            "id": 1,
            "remote_id": "dev-1",
            "name": "علی رضایی",
            "date": "2026-10-07",
            "time": "08:30:00",
            "created_at": "2026-10-07T16:30:00",
            "updated_at": "2026-10-07T16:30:00",
        }
    ]


def test_leave_stores_minutes_and_keeps_each_shamsi_year_separate(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    person_id = add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)
    current = parse_shamsi_date("1405/06/15")
    previous = parse_shamsi_date("1404/06/15")
    amount = compose_leave_minutes(1, 2, 15)

    leave_id = add_leave(person_id, current, current, amount, db_file=db_file)
    add_leave(person_id, previous, previous, 30, db_file=db_file)

    rows = list_leaves(person_id, db_file=db_file)
    assert leave_id == 1
    assert [row["id"] for row in rows] == [2, 1]
    assert rows[1]["minutes"] == amount
    assert rows[1]["start_date"] == current.isoformat()
    assert rows[1]["created_at"] == "2026-10-07T16:30:00"
    assert [row["minutes"] for row in rows] == [30, amount]

    year_start, year_end = shamsi_year_span(1405)
    assert sum_leave_minutes(person_id, year_start, year_end, db_file=db_file) == amount
    inside = list_leaves(person_id, current, current, db_file=db_file)
    assert [row["id"] for row in inside] == [leave_id]
    assert list_leaves(person_id, parse_shamsi_date("1405/07/01"), parse_shamsi_date("1405/07/10"), db_file=db_file) == []

    with pytest.raises(ValueError, match="بیشتر است"):
        add_leave(person_id, current, current, compose_leave_minutes(30, 0, 0), db_file=db_file)
    with pytest.raises(ValueError, match="تاریخ پایان"):
        add_leave(person_id, current, previous, 15, db_file=db_file)
    with pytest.raises(ValueError, match="وجود ندارد"):
        add_leave(99, current, current, 15, db_file=db_file)
