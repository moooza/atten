"""Data access. The UI calls this module and does not write SQL."""

import math
import sqlite3
from dataclasses import dataclass
from datetime import date, datetime
from pathlib import Path

from atten.attlog import AttlogRow
from atten.dates import storage_date, storage_datetime
from atten.db.connection import connect
from atten.leave import YEARLY_LEAVE_MINUTES, format_leave_amount, shamsi_year_of, shamsi_year_span
from atten.paths import db_path
from atten.persian import english_digits, normalize_text


@dataclass(frozen=True)
class ClockEventImport:
    added: int
    skipped: int


def add_personnel(
    first_name: str,
    last_name: str,
    daily_hours: float,
    remote_id: str | None = None,
    mobile: str | None = None,
    db_file: Path | None = None,
) -> int:
    path = db_path() if db_file is None else db_file
    fields = _validated_fields(first_name, last_name, daily_hours, remote_id, mobile)
    created_at = _timestamp()
    try:
        with connect(path) as conn:
            cursor = conn.execute(
                """
                INSERT INTO personnel (
                    remote_id, first_name, last_name, daily_hours, mobile, created_at, updated_at
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (*fields, created_at, created_at),
            )
            conn.commit()
            return int(cursor.lastrowid)
    except sqlite3.IntegrityError as exc:
        raise _save_error(exc) from exc


def update_personnel(
    person_id: int,
    first_name: str,
    last_name: str,
    daily_hours: float,
    remote_id: str | None = None,
    mobile: str | None = None,
    db_file: Path | None = None,
) -> None:
    path = db_path() if db_file is None else db_file
    fields = _validated_fields(first_name, last_name, daily_hours, remote_id, mobile)
    try:
        with connect(path) as conn:
            found = conn.execute(
                "SELECT 1 FROM personnel WHERE id = ?",
                (person_id,),
            ).fetchone()
            if found is None:
                raise ValueError("این پرسنل دیگر وجود ندارد.")
            conn.execute(
                """
                UPDATE personnel
                SET remote_id = ?, first_name = ?, last_name = ?, daily_hours = ?, mobile = ?,
                    updated_at = ?
                WHERE id = ?
                """,
                (*fields, _timestamp(), person_id),
            )
            conn.commit()
    except sqlite3.IntegrityError as exc:
        raise _save_error(exc) from exc


def _validated_fields(
    first_name: str,
    last_name: str,
    daily_hours: float,
    remote_id: str | None,
    mobile: str | None,
) -> tuple[str | None, str, str, float, str | None]:
    first = normalize_text(first_name.strip())
    last = normalize_text(last_name.strip())
    if not first:
        raise ValueError("نام را وارد کنید.")
    if not last:
        raise ValueError("نام خانوادگی را وارد کنید.")
    try:
        hours = float(daily_hours)
    except (TypeError, ValueError) as exc:
        raise ValueError("ساعت کاری باید یک عدد بزرگ‌تر از صفر باشد.") from exc
    if not math.isfinite(hours) or hours <= 0:
        raise ValueError("ساعت کاری باید یک عدد بزرگ‌تر از صفر باشد.")
    return (_blank_to_none(remote_id), first, last, hours, _blank_to_none(mobile))


def _save_error(exc: sqlite3.IntegrityError) -> ValueError:
    message = str(exc).lower()
    if "unique" in message and "remote_id" in message:
        return ValueError("این کد پرسنلی قبلاً ثبت شده است.")
    return ValueError("ذخیره پرسنل ممکن نشد.")


def _timestamp() -> str:
    return storage_datetime(datetime.now())


def _blank_to_none(value: str | None) -> str | None:
    if value is None:
        return None
    text = normalize_text(value.strip())
    return text or None


def list_personnel(db_file: Path | None = None) -> list[dict[str, object]]:
    path = db_path() if db_file is None else db_file
    with connect(path) as conn:
        rows = conn.execute(
            """
            SELECT id, remote_id, first_name, last_name, daily_hours, mobile, created_at, updated_at
            FROM personnel
            ORDER BY id
            """
        ).fetchall()
    return [dict(row) for row in rows]


def add_leave(
    personnel_id: int,
    start_date: date | str,
    end_date: date | str,
    minutes: int,
    db_file: Path | None = None,
) -> int:
    path = db_path() if db_file is None else db_file
    person_id = _person_id(personnel_id)
    start = _storage_day(start_date)
    end = _storage_day(end_date)
    if start > end:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    amount = _leave_minutes(minutes)
    created_at = _timestamp()
    try:
        with connect(path) as conn:
            _require_personnel(conn, person_id)
            _reject_if_leave_exceeds(conn, person_id, start, amount, exclude_id=None)
            cursor = conn.execute(
                """
                INSERT INTO leaves (
                    personnel_id, start_date, end_date, minutes, created_at, updated_at
                )
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (person_id, start, end, amount, created_at, created_at),
            )
            conn.commit()
            return int(cursor.lastrowid)
    except sqlite3.IntegrityError as exc:
        raise ValueError("ثبت مرخصی ممکن نشد.") from exc


def update_leave(
    leave_id: int,
    personnel_id: int,
    start_date: date | str,
    end_date: date | str,
    minutes: int,
    db_file: Path | None = None,
) -> None:
    path = db_path() if db_file is None else db_file
    leave_key = _leave_id(leave_id)
    person_id = _person_id(personnel_id)
    start = _storage_day(start_date)
    end = _storage_day(end_date)
    if start > end:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    amount = _leave_minutes(minutes)
    try:
        with connect(path) as conn:
            found = conn.execute(
                "SELECT 1 FROM leaves WHERE id = ?",
                (leave_key,),
            ).fetchone()
            if found is None:
                raise ValueError("این مرخصی دیگر وجود ندارد.")
            _require_personnel(conn, person_id)
            _reject_if_leave_exceeds(conn, person_id, start, amount, exclude_id=leave_key)
            conn.execute(
                """
                UPDATE leaves
                SET personnel_id = ?, start_date = ?, end_date = ?, minutes = ?, updated_at = ?
                WHERE id = ?
                """,
                (person_id, start, end, amount, _timestamp(), leave_key),
            )
            conn.commit()
    except sqlite3.IntegrityError as exc:
        raise ValueError("ثبت مرخصی ممکن نشد.") from exc


def list_leaves(
    personnel_id: int,
    start_date: date | str | None = None,
    end_date: date | str | None = None,
    db_file: Path | None = None,
) -> list[dict[str, object]]:
    path = db_path() if db_file is None else db_file
    person_id = _person_id(personnel_id)
    start = _optional_day(start_date)
    end = _optional_day(end_date)
    if start is not None and end is not None and start > end:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    clauses = ["personnel_id = ?"]
    params: list[object] = [person_id]
    if start is not None:
        clauses.append("end_date >= ?")
        params.append(start)
    if end is not None:
        clauses.append("start_date <= ?")
        params.append(end)
    where = " AND ".join(clauses)
    with connect(path) as conn:
        rows = conn.execute(
            f"""
            SELECT id, personnel_id, start_date, end_date, minutes, created_at, updated_at
            FROM leaves
            WHERE {where}
            ORDER BY start_date, id
            """,
            params,
        ).fetchall()
    return [dict(row) for row in rows]


def sum_leave_minutes(
    personnel_id: int,
    start_date: date | str,
    end_date: date | str,
    db_file: Path | None = None,
) -> int:
    """Minutes of leaves whose start date falls inside the span."""
    path = db_path() if db_file is None else db_file
    person_id = _person_id(personnel_id)
    start, end = _date_span(start_date, end_date)
    with connect(path) as conn:
        row = conn.execute(
            """
            SELECT COALESCE(SUM(minutes), 0) AS total
            FROM leaves
            WHERE personnel_id = ? AND start_date >= ? AND start_date <= ?
            """,
            (person_id, start, end),
        ).fetchone()
    return int(row["total"])


def _require_personnel(conn, person_id: int) -> None:
    found = conn.execute(
        "SELECT 1 FROM personnel WHERE id = ?",
        (person_id,),
    ).fetchone()
    if found is None:
        raise ValueError("این پرسنل دیگر وجود ندارد.")


def _reject_if_leave_exceeds(
    conn,
    person_id: int,
    start: str,
    amount: int,
    exclude_id: int | None,
) -> None:
    year = shamsi_year_of(date.fromisoformat(start))
    span_start, span_end = shamsi_year_span(year)
    params: list[object] = [person_id, storage_date(span_start), storage_date(span_end)]
    exclude = ""
    if exclude_id is not None:
        exclude = " AND id != ?"
        params.append(exclude_id)
    used = conn.execute(
        f"""
        SELECT COALESCE(SUM(minutes), 0) AS total
        FROM leaves
        WHERE personnel_id = ? AND start_date >= ? AND start_date <= ?{exclude}
        """,
        params,
    ).fetchone()
    remaining = YEARLY_LEAVE_MINUTES - int(used["total"])
    if amount > remaining:
        left = format_leave_amount(max(remaining, 0))
        raise ValueError(f"این مرخصی از مانده سال {year} بیشتر است. باقی‌مانده: {left}")


def _leave_id(value: int) -> int:
    if isinstance(value, bool):
        raise ValueError("یک مرخصی را انتخاب کنید.")
    try:
        leave_id = int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("یک مرخصی را انتخاب کنید.") from exc
    if leave_id <= 0:
        raise ValueError("یک مرخصی را انتخاب کنید.")
    return leave_id


def _person_id(value: int) -> int:
    if isinstance(value, bool):
        raise ValueError("یک پرسنل را انتخاب کنید.")
    try:
        person_id = int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError("یک پرسنل را انتخاب کنید.") from exc
    if person_id <= 0:
        raise ValueError("یک پرسنل را انتخاب کنید.")
    return person_id


def _leave_minutes(value: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError("میزان مرخصی را وارد کنید.")
    return value


def _optional_day(value: date | str | None) -> str | None:
    if value is None:
        return None
    if isinstance(value, str) and not value.strip():
        return None
    return _storage_day(value)


def add_clock_event(
    remote_id: str,
    name: str,
    event_date: date | str,
    event_time: str,
    db_file: Path | None = None,
) -> int:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    person_name = _required_text(name, "نام را وارد کنید.")
    stored_date = _storage_day(event_date)
    stored_time = _storage_clock(event_time)
    created_at = _timestamp()
    try:
        with connect(path) as conn:
            cursor = conn.execute(
                """
                INSERT INTO clock_events (
                    remote_id, name, date, time, created_at, updated_at
                )
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (person_key, person_name, stored_date, stored_time, created_at, created_at),
            )
            conn.commit()
            return int(cursor.lastrowid)
    except sqlite3.IntegrityError as exc:
        raise ValueError("ثبت ورود و خروج ممکن نشد.") from exc


def import_clock_events(
    rows: list[AttlogRow],
    db_file: Path | None = None,
) -> ClockEventImport:
    path = db_path() if db_file is None else db_file
    prepared = [_prepared_clock_event(row) for row in rows]
    created_at = _timestamp()
    added = 0
    skipped = 0
    try:
        with connect(path) as conn:
            existing = {
                (row["remote_id"], row["date"], row["time"])
                for row in conn.execute("SELECT remote_id, date, time FROM clock_events")
            }
            seen: set[tuple[str, str, str]] = set()
            for remote_id, name, stored_date, stored_time in prepared:
                key = (remote_id, stored_date, stored_time)
                if key in existing or key in seen:
                    skipped += 1
                    continue
                conn.execute(
                    """
                    INSERT INTO clock_events (
                        remote_id, name, date, time, created_at, updated_at
                    )
                    VALUES (?, ?, ?, ?, ?, ?)
                    """,
                    (remote_id, name, stored_date, stored_time, created_at, created_at),
                )
                seen.add(key)
                added += 1
            conn.commit()
    except sqlite3.IntegrityError as exc:
        raise ValueError("ثبت ورود و خروج ممکن نشد.") from exc
    return ClockEventImport(added, skipped)


def _prepared_clock_event(row: AttlogRow) -> tuple[str, str, str, str]:
    try:
        return (
            _required_text(row.remote_id, "Remote ID را وارد کنید."),
            _required_text(row.name, "نام را وارد کنید."),
            _storage_day(row.event_date),
            _storage_clock(row.event_time),
        )
    except ValueError as exc:
        raise ValueError(f"سطر {row.line_number}: {exc}") from exc


def list_daily_punches(
    remote_id: str,
    start_date: date | str,
    end_date: date | str,
    db_file: Path | None = None,
) -> list[dict[str, object]]:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    start = _storage_day(start_date)
    end = _storage_day(end_date)
    if start > end:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    with connect(path) as conn:
        rows = conn.execute(
            """
            SELECT date, time
            FROM clock_events
            WHERE remote_id = ? AND date >= ? AND date <= ?
            ORDER BY date, time, id
            """,
            (person_key, start, end),
        ).fetchall()
    days: list[dict[str, object]] = []
    current_date = ""
    times: list[str] = []
    for row in rows:
        if row["date"] != current_date:
            if current_date:
                days.append({"date": current_date, "times": times})
            current_date = str(row["date"])
            times = []
        times.append(str(row["time"]))
    if current_date:
        days.append({"date": current_date, "times": times})
    return days


def list_work_punches(
    remote_id: str,
    start_date: date | str,
    end_date: date | str,
    db_file: Path | None = None,
) -> dict[str, dict[int, str | None]]:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    start, end = _date_span(start_date, end_date)
    with connect(path) as conn:
        rows = conn.execute(
            """
            SELECT date, slot, time
            FROM calculation_punches
            WHERE remote_id = ? AND date >= ? AND date <= ?
            ORDER BY date, slot
            """,
            (person_key, start, end),
        ).fetchall()
    grouped: dict[str, dict[int, str | None]] = {}
    for row in rows:
        grouped.setdefault(str(row["date"]), {})[int(row["slot"])] = (
            None if row["time"] is None else str(row["time"])
        )
    return grouped


def save_work_punch(
    remote_id: str,
    event_date: date | str,
    slot: int,
    event_time: str | None,
    db_file: Path | None = None,
) -> None:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    stored_date = _storage_day(event_date)
    if slot < 0:
        raise ValueError("ساعت معتبر نیست.")
    stored_time = None if event_time is None or not event_time.strip() else _storage_clock(event_time)
    stamp = _timestamp()
    with connect(path) as conn:
        conn.execute(
            """
            INSERT INTO calculation_punches (
                remote_id, date, slot, time, created_at, updated_at
            )
            VALUES (?, ?, ?, ?, ?, ?)
            ON CONFLICT (remote_id, date, slot) DO UPDATE SET
                time = excluded.time,
                updated_at = excluded.updated_at
            """,
            (person_key, stored_date, slot, stored_time, stamp, stamp),
        )
        conn.commit()


def clear_work_punches(
    remote_id: str,
    event_date: date | str,
    db_file: Path | None = None,
) -> None:
    """Drop manual entry and exit times so that day is read from clock events again."""
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    stored_date = _storage_day(event_date)
    with connect(path) as conn:
        conn.execute(
            """
            DELETE FROM calculation_punches
            WHERE remote_id = ? AND date = ?
            """,
            (person_key, stored_date),
        )
        conn.commit()


def list_work_balances(
    remote_id: str,
    start_date: date | str,
    end_date: date | str,
    db_file: Path | None = None,
) -> list[dict[str, object]]:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    start, end = _date_span(start_date, end_date)
    with connect(path) as conn:
        rows = conn.execute(
            """
            SELECT remote_id, date, balance_minutes, holiday
            FROM calculation_days
            WHERE remote_id = ? AND date >= ? AND date <= ?
            ORDER BY date
            """,
            (person_key, start, end),
        ).fetchall()
    return [dict(row) for row in rows]




def save_work_balances(
    remote_id: str,
    balances: list[tuple[str, int, bool]],
    db_file: Path | None = None,
) -> None:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    stamp = _timestamp()
    prepared = [
        (_storage_day(day), int(minutes), 1 if holiday else 0)
        for day, minutes, holiday in balances
    ]
    with connect(path) as conn:
        conn.executemany(
            """
            INSERT INTO calculation_days (
                remote_id, date, balance_minutes, holiday, created_at, updated_at
            )
            VALUES (?, ?, ?, ?, ?, ?)
            ON CONFLICT (remote_id, date) DO UPDATE SET
                balance_minutes = excluded.balance_minutes,
                holiday = excluded.holiday,
                updated_at = excluded.updated_at
            """,
            [
                (person_key, day, minutes, holiday, stamp, stamp)
                for day, minutes, holiday in prepared
            ],
        )
        conn.commit()


def save_work_holiday(
    remote_id: str,
    event_date: date | str,
    holiday: bool,
    db_file: Path | None = None,
) -> None:
    path = db_path() if db_file is None else db_file
    person_key = _required_text(remote_id, "Remote ID را وارد کنید.")
    stored_date = _storage_day(event_date)
    stamp = _timestamp()
    flag = 1 if holiday else 0
    with connect(path) as conn:
        conn.execute(
            """
            INSERT INTO calculation_days (
                remote_id, date, balance_minutes, holiday, created_at, updated_at
            )
            VALUES (?, ?, 0, ?, ?, ?)
            ON CONFLICT (remote_id, date) DO UPDATE SET
                holiday = excluded.holiday,
                updated_at = excluded.updated_at
            """,
            (person_key, stored_date, flag, stamp, stamp),
        )
        conn.commit()


def ping(db_file: Path | None = None) -> str:
    path = db_path() if db_file is None else db_file
    with connect(path) as conn:
        conn.execute("SELECT 1")
    return "ok"


def list_clock_events(db_file: Path | None = None) -> list[dict[str, object]]:
    path = db_path() if db_file is None else db_file
    with connect(path) as conn:
        rows = conn.execute(
            """
            SELECT id, remote_id, name, date, time, created_at, updated_at
            FROM clock_events
            ORDER BY date, time, id
            """
        ).fetchall()
    return [dict(row) for row in rows]


def _required_text(value: object, message: str) -> str:
    text = "" if value is None else normalize_text(str(value).strip())
    if not text:
        raise ValueError(message)
    return text


def _storage_day(value: date | datetime | str) -> str:
    if isinstance(value, datetime):
        return storage_date(value.date())
    if isinstance(value, date):
        return storage_date(value)
    text = str(value).strip()
    try:
        return storage_date(date.fromisoformat(text[:10]))
    except ValueError as exc:
        raise ValueError("تاریخ معتبر نیست.") from exc


def _storage_clock(value: str) -> str:
    text = english_digits(str(value).strip())
    pieces = text.split(":")
    if len(pieces) not in (2, 3) or not all(piece.isdigit() for piece in pieces):
        raise ValueError("ساعت معتبر نیست.")
    hour = int(pieces[0])
    minute = int(pieces[1])
    second = int(pieces[2]) if len(pieces) == 3 else 0
    if hour > 23 or minute > 59 or second > 59:
        raise ValueError("ساعت معتبر نیست.")
    return f"{hour:02d}:{minute:02d}:{second:02d}"


def _date_span(start_date: date | str, end_date: date | str) -> tuple[str, str]:
    start = _storage_day(start_date)
    end = _storage_day(end_date)
    if start > end:
        raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
    return start, end
