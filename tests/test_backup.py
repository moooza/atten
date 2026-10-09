import sqlite3
from datetime import datetime
from pathlib import Path

import pytest

from atten.db.backup import (
    check_backup,
    describe_backup_health,
    export_database,
    healthy,
    prepare_database,
    restore_database,
    snapshot,
    snapshot_paths,
)
from atten.db.connection import connect
from atten.db.migrate import migrate


def _insert_person(db_file: Path, remote_id: str) -> None:
    with connect(db_file) as conn:
        conn.execute(
            """
            INSERT INTO personnel (remote_id, first_name, last_name, daily_hours, mobile)
            VALUES (?, ?, ?, ?, ?)
            """,
            (remote_id, "علی", "رضایی", 8, None),
        )
        conn.commit()


def _person_ids(db_file: Path) -> list[str]:
    with connect(db_file) as conn:
        rows = conn.execute("SELECT remote_id FROM personnel ORDER BY id").fetchall()
    return [row["remote_id"] for row in rows]


def test_committed_write_creates_a_standalone_snapshot(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    before = len(snapshot_paths(db_file))
    _insert_person(db_file, "dev-1")

    copies = snapshot_paths(db_file)
    assert len(copies) == before + 1
    backup = copies[-1]
    assert not Path(str(backup) + "-wal").exists()
    conn = sqlite3.connect(backup)
    try:
        assert conn.execute("SELECT remote_id FROM personnel").fetchone()[0] == "dev-1"
        assert conn.execute("PRAGMA quick_check").fetchone()[0] == "ok"
    finally:
        conn.close()

    _person_ids(db_file)
    assert len(snapshot_paths(db_file)) == before + 1


def test_damaged_database_is_restored_from_the_newest_healthy_snapshot(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    _insert_person(db_file, "kept")
    junk = db_file.parent / "backups" / "atten-2999-01-01T000000000000.db"
    junk.write_bytes(b"this is not a database" + b"\0" * 128)
    db_file.write_bytes(b"torn write")
    Path(str(db_file) + "-wal").write_bytes(b"stale wal")

    notice = prepare_database(db_file)

    assert notice is not None
    assert healthy(db_file)
    assert _person_ids(db_file) == ["kept"]
    quarantined = list((db_file.parent / "corrupt").iterdir())
    assert any(path.name.startswith("atten.db.") for path in quarantined)
    wal_copies = [path for path in quarantined if path.name.startswith("atten.db-wal.")]
    assert [path.read_bytes() for path in wal_copies] == [b"stale wal"]


def test_damaged_database_without_a_snapshot_is_left_in_place(tmp_path):
    db_file = tmp_path / "atten.db"
    original = b"not a database"
    db_file.write_bytes(original)

    with pytest.raises(sqlite3.DatabaseError):
        prepare_database(db_file)

    assert db_file.read_bytes() == original
    assert not (db_file.parent / "corrupt").exists()


def test_snapshots_are_pruned_to_the_requested_count(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    for _ in range(4):
        assert snapshot(db_file, keep=2) is not None

    assert len(snapshot_paths(db_file)) == 2


def test_export_names_the_file_with_the_creation_time_and_keeps_every_row(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    _insert_person(db_file, "dev-1")
    folder = tmp_path / "chosen"
    moment = datetime(2026, 10, 8, 21, 28, 5)

    exported = export_database(db_file, folder, moment=moment)
    again = export_database(db_file, folder, moment=moment)

    assert exported.name == "atten-backup-2026-10-08_21-28-05.db"
    assert again.name == "atten-backup-2026-10-08_21-28-05-2.db"
    assert exported.parent == folder
    copy = sqlite3.connect(exported)
    try:
        assert copy.execute("SELECT remote_id FROM personnel").fetchone()[0] == "dev-1"
        assert copy.execute("SELECT 1 FROM schema_migrations").fetchone() is not None
        assert copy.execute("PRAGMA quick_check").fetchone()[0] == "ok"
    finally:
        copy.close()


def test_restore_replaces_the_live_database_and_drops_the_wal(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    _insert_person(db_file, "kept")
    exported = export_database(db_file, tmp_path / "out", moment=datetime(2026, 10, 8, 21, 28, 5))
    _insert_person(db_file, "gone")
    Path(str(db_file) + "-wal").write_bytes(b"stale wal")

    restore_database(db_file, exported)

    assert _person_ids(db_file) == ["kept"]
    assert healthy(db_file)
    assert not Path(str(db_file) + "-wal").exists()
    assert not Path(str(db_file) + ".restore").exists()


def test_restore_rejects_a_file_that_is_not_a_backup(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    _insert_person(db_file, "kept")
    junk = tmp_path / "notes.db"
    junk.write_bytes(b"this is not a database" + b"\0" * 128)

    with pytest.raises(ValueError, match="نسخهٔ پشتیبان سالم"):
        restore_database(db_file, junk)

    assert _person_ids(db_file) == ["kept"]


def test_user_exports_are_not_pruned_with_automatic_snapshots(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    exported = export_database(
        db_file,
        db_file.parent / "backups",
        moment=datetime(2026, 10, 8, 21, 28, 5),
    )
    for _ in range(3):
        assert snapshot(db_file, keep=1) is not None

    assert exported.exists()
    assert len(snapshot_paths(db_file)) == 1
    assert exported not in snapshot_paths(db_file)


def test_check_backup_reads_every_table_and_reports_a_broken_link(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    _insert_person(db_file, "dev-1")
    exported = export_database(db_file, tmp_path / "out", moment=datetime(2026, 10, 8, 21, 28, 5))
    original = exported.read_bytes()
    steps: list[tuple[int, int, str]] = []

    result = check_backup(exported, on_progress=lambda done, total, message: steps.append((done, total, message)))

    assert result.ok
    counts = {table.name: table.rows for table in result.tables}
    assert counts["personnel"] == 1
    assert counts["clock_events"] == 0
    assert counts["leaves"] == 0
    assert "schema_migrations" in counts
    assert steps[-1] == (steps[-1][1], steps[-1][1], "بررسی تمام شد.")
    assert any("پرسنل" in message for _done, _total, message in steps)
    assert "فایل سالم است" in describe_backup_health(result)
    assert "پرسنل: 1 رکورد" in describe_backup_health(result)
    assert exported.read_bytes() == original
    assert not Path(str(exported) + "-wal").exists()

    with sqlite3.connect(exported) as conn:
        conn.execute(
            """
            INSERT INTO leaves (personnel_id, start_date, end_date, minutes, created_at, updated_at)
            VALUES (999, '2026-10-08', '2026-10-08', 60, '2026-10-08T10:00:00', '2026-10-08T10:00:00')
            """
        )
        conn.commit()

    broken = check_backup(exported)
    assert not broken.ok
    text = describe_backup_health(broken)
    assert "فایل سالم نیست." in text
    assert "مرخصی‌ها" in text
    assert "پرسنل" in text


def test_check_backup_rejects_a_file_that_is_not_a_database(tmp_path):
    junk = tmp_path / "notes.db"
    junk.write_bytes(b"this is not a database" + b"\0" * 128)
    missing = tmp_path / "missing.db"

    junk_result = check_backup(junk)
    missing_result = check_backup(missing)

    assert not junk_result.ok
    assert not junk_result.readable
    assert junk_result.file_error == "این فایل پایگاه دادهٔ سالمی نیست."
    assert not missing_result.readable
    assert missing_result.file_error == "فایل پیدا نشد."

    db_file = tmp_path / "atten.db"
    migrate(db_file)
    exported = export_database(db_file, tmp_path / "out", moment=datetime(2026, 10, 8, 21, 28, 5))
    truncated = tmp_path / "truncated.db"
    truncated.write_bytes(exported.read_bytes()[:120])
    damaged = check_backup(truncated)
    assert not damaged.ok
    assert "فایل سالم است" not in describe_backup_health(damaged)
