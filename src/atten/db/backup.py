"""Crash-safe copies of the database, and restore when the live file is damaged."""

import logging
import os
import shutil
import sqlite3
from collections.abc import Callable
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

log = logging.getLogger(__name__)

KEEP_SNAPSHOTS = 60
_SNAPSHOT_GLOB = "atten-[0-9]*.db"
_TABLE_LABELS = {
    "schema_migrations": "نسخه‌های پایگاه",
    "personnel": "پرسنل",
    "clock_events": "ورود و خروج",
    "leaves": "مرخصی‌ها",
    "calculation_days": "روزهای محاسبه",
    "calculation_punches": "ساعت‌های محاسبه",
}
_TABLE_ORDER = tuple(_TABLE_LABELS)
BackupProgress = Callable[[int, int, str], None]

RESTORED_NOTICE = (
    "پایگاه داده آسیب دیده بود و از آخرین نسخهٔ پشتیبان سالم برگردانده شد. "
    "اگر درست قبل از قطع برق چیزی ذخیره شده باشد، ممکن است همان ثبت آخر برنگشته باشد."
)


def prepare_database(db_file: Path) -> str | None:
    """Migrate a healthy file, or replace a damaged one with the newest good snapshot."""
    notice = None
    if db_file.exists() and not healthy(db_file):
        restored = restore_newest(db_file)
        if restored is None:
            raise sqlite3.DatabaseError("database is damaged and no backup is available")
        log.info("restored database %s from %s", db_file, restored.name)
        notice = RESTORED_NOTICE
    from atten.db.migrate import migrate

    migrate(db_file)
    if not snapshot_paths(db_file):
        snapshot(db_file)
    return notice


def snapshot(db_file: Path, keep: int = KEEP_SNAPSHOTS) -> Path | None:
    """Write a consistent single-file copy beside the database."""
    if not db_file.exists():
        return None
    folder = db_file.parent / "backups"
    folder.mkdir(parents=True, exist_ok=True)
    target = _snapshot_path(folder)
    if not _consistent_copy(db_file, target):
        return None
    _prune(folder, keep)
    return target


def restore_newest(db_file: Path) -> Path | None:
    """Replace a damaged database with the newest snapshot that still opens."""
    candidates = sorted(snapshot_paths(db_file), key=lambda path: path.stat().st_mtime, reverse=True)
    for backup in candidates:
        if not healthy(backup):
            continue
        _quarantine(db_file)
        shutil.copy2(backup, db_file)
        _fsync(db_file)
        if healthy(db_file):
            return backup
    return None


def healthy(db_file: Path) -> bool:
    if not db_file.exists() or db_file.stat().st_size < 100:
        return False
    try:
        conn = sqlite3.connect(db_file, timeout=5)
    except sqlite3.Error:
        return False
    try:
        rows = conn.execute("PRAGMA quick_check").fetchall()
    except sqlite3.Error:
        return False
    finally:
        conn.close()
    return len(rows) == 1 and rows[0][0] == "ok"


def export_database(db_file: Path, folder: Path, *, moment: datetime | None = None) -> Path:
    """Write a consistent copy of the whole database into the chosen folder."""
    if not db_file.exists():
        raise FileNotFoundError(db_file)
    folder.mkdir(parents=True, exist_ok=True)
    target = _export_path(folder, moment or datetime.now())
    if not _consistent_copy(db_file, target):
        raise sqlite3.DatabaseError("ساختن نسخهٔ پشتیبان ممکن نشد.")
    return target


def validate_backup(db_file: Path, source: Path) -> None:
    """Reject a file that cannot replace the live database."""
    source = source.resolve()
    db_file = db_file.resolve()
    if source == db_file:
        raise ValueError("این فایل همان پایگاه دادهٔ جاری است.")
    if not _is_app_database(source):
        raise ValueError("فایل انتخاب‌شده یک نسخهٔ پشتیبان سالم نیست.")


def restore_database(db_file: Path, source: Path) -> None:
    """Replace the live database with a consistent copy of a backup file."""
    source = source.resolve()
    db_file = db_file.resolve()
    validate_backup(db_file, source)
    db_file.parent.mkdir(parents=True, exist_ok=True)
    temporary = db_file.with_name(db_file.name + ".restore")
    try:
        if not _consistent_copy(source, temporary, checkpoint_source=False):
            raise sqlite3.DatabaseError("بازگردانی نسخهٔ پشتیبان ممکن نشد.")
        _discard_sidecars(db_file)
        os.replace(temporary, db_file)
        _remove_sidecars(db_file)
        _fsync(db_file)
    finally:
        temporary.unlink(missing_ok=True)
        _remove_sidecars(temporary)
    from atten.db.migrate import migrate

    migrate(db_file)


@dataclass(frozen=True)
class CheckedTable:
    name: str
    rows: int
    error: str | None = None


@dataclass(frozen=True)
class BackupHealth:
    readable: bool
    tables: tuple[CheckedTable, ...] = ()
    integrity_errors: tuple[str, ...] = ()
    foreign_key_errors: tuple[str, ...] = ()
    file_error: str | None = None

    @property
    def ok(self) -> bool:
        names = {table.name for table in self.tables}
        return (
            self.readable
            and self.file_error is None
            and "schema_migrations" in names
            and all(table.error is None for table in self.tables)
            and not self.integrity_errors
            and not self.foreign_key_errors
        )


def table_label(name: str) -> str:
    return _TABLE_LABELS.get(name, name)


def check_backup(path: Path, on_progress: BackupProgress | None = None) -> BackupHealth:
    """Read every table and record, then check pages, indexes, and foreign keys."""

    def report(done: int, total: int, message: str) -> None:
        if on_progress is not None:
            on_progress(done, total, message)

    if not path.is_file():
        report(1, 1, "فایل پیدا نشد.")
        return BackupHealth(readable=False, file_error="فایل پیدا نشد.")
    try:
        conn = _open_readonly(path)
    except sqlite3.Error as exc:
        report(1, 1, "فایل باز نشد.")
        return BackupHealth(readable=False, file_error=_file_error(exc))

    try:
        try:
            names = [
                str(row[0])
                for row in conn.execute(
                    """
                    SELECT name FROM sqlite_master
                    WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
                    ORDER BY name
                    """
                )
            ]
        except sqlite3.Error as exc:
            report(1, 1, "فایل پایگاه داده نیست.")
            return BackupHealth(readable=False, file_error=_file_error(exc))

        ordered = _ordered_tables(names)
        total = len(ordered) + 2
        tables: list[CheckedTable] = []
        for index, name in enumerate(ordered, start=1):
            label = table_label(name)
            report(index - 1, total, f"جدول {label} بررسی می‌شود...")
            rows, error = _scan_table(
                conn,
                name,
                lambda count, label=label, index=index: report(
                    index - 1,
                    total,
                    f"جدول {label}: {count} رکورد خوانده شد...",
                ),
            )
            tables.append(CheckedTable(name, rows, error))
            if error:
                report(index, total, f"جدول {label} خراب است.")
            else:
                report(index, total, f"جدول {label}: {rows} رکورد")

        report(len(ordered), total, "ساختار فایل و فهرست‌ها بررسی می‌شود...")
        integrity_errors = _integrity_errors(conn)
        report(len(ordered) + 1, total, "ارتباط جدول‌ها بررسی می‌شود...")
        foreign_key_errors = _foreign_key_errors(conn)
        report(total, total, "بررسی تمام شد.")
        return BackupHealth(
            readable=True,
            tables=tuple(tables),
            integrity_errors=integrity_errors,
            foreign_key_errors=foreign_key_errors,
        )
    finally:
        conn.close()


def describe_backup_health(result: BackupHealth) -> str:
    if not result.readable:
        return result.file_error or "فایل قابل بررسی نیست."
    lines: list[str] = []
    total_rows = 0
    for table in result.tables:
        label = table_label(table.name)
        if table.error:
            lines.append(f"{label}: {table.error}")
        else:
            lines.append(f"{label}: {table.rows} رکورد")
            total_rows += table.rows
    if "schema_migrations" not in {table.name for table in result.tables}:
        lines.append("این فایل نسخهٔ پشتیبان این برنامه نیست.")
    if result.integrity_errors:
        lines.append("ساختار فایل: " + _join_findings(result.integrity_errors))
    else:
        lines.append("ساختار فایل و فهرست‌ها: سالم")
    if result.foreign_key_errors:
        lines.append("ارتباط جدول‌ها: " + _join_findings(result.foreign_key_errors))
    else:
        lines.append("ارتباط جدول‌ها: سالم")
    if result.ok:
        lines.insert(0, f"فایل سالم است. {len(result.tables)} جدول و {total_rows} رکورد بررسی شد.")
    else:
        lines.insert(0, "فایل سالم نیست.")
    return "\n".join(lines)


def snapshot_paths(db_file: Path) -> list[Path]:
    folder = db_file.parent / "backups"
    if not folder.exists():
        return []
    return sorted(folder.glob(_SNAPSHOT_GLOB))


def _consistent_copy(source_file: Path, target: Path, *, checkpoint_source: bool = True) -> bool:
    temporary = Path(str(target) + ".tmp")
    source = sqlite3.connect(source_file, timeout=5)
    destination = sqlite3.connect(temporary, timeout=5)
    failed = False
    try:
        destination.execute("PRAGMA synchronous = FULL")
        source.backup(destination)
        destination.execute("PRAGMA journal_mode = DELETE").fetchone()
        destination.commit()
    except sqlite3.Error:
        log.exception("database copy failed for %s", source_file)
        failed = True
    finally:
        destination.close()
        _remove_sidecars(temporary)
        if checkpoint_source:
            try:
                source.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchall()
            except sqlite3.Error:
                log.exception("checkpoint failed for %s", source_file)
        source.close()
    if failed or not temporary.exists() or not healthy(temporary):
        temporary.unlink(missing_ok=True)
        _remove_sidecars(temporary)
        if not failed:
            log.error("copy for %s was not a healthy database", source_file)
        return False
    _remove_sidecars(temporary)
    _fsync(temporary)
    os.replace(temporary, target)
    return True


def _is_app_database(db_file: Path) -> bool:
    if not healthy(db_file):
        return False
    try:
        conn = sqlite3.connect(db_file, timeout=5)
    except sqlite3.Error:
        return False
    try:
        row = conn.execute(
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'"
        ).fetchone()
    except sqlite3.Error:
        return False
    finally:
        conn.close()
    return row is not None


def _open_readonly(path: Path) -> sqlite3.Connection:
    uri = path.resolve().as_uri() + "?mode=ro"
    conn = sqlite3.connect(uri, uri=True, timeout=5)
    conn.execute("PRAGMA busy_timeout = 5000")
    return conn


def _ordered_tables(names: list[str]) -> list[str]:
    ranked = {name: index for index, name in enumerate(_TABLE_ORDER)}
    return sorted(names, key=lambda name: (ranked.get(name, len(ranked)), name))


def _scan_table(conn: sqlite3.Connection, name: str, on_batch: Callable[[int], None]) -> tuple[int, str | None]:
    quoted = '"' + name.replace('"', '""') + '"'
    try:
        cursor = conn.execute(f"SELECT * FROM {quoted}")
    except sqlite3.Error:
        return 0, "خواندن رکوردها ممکن نشد."
    count = 0
    announced = 0
    try:
        while True:
            batch = cursor.fetchmany(500)
            if not batch:
                break
            count += len(batch)
            if count - announced >= 1000:
                announced = count
                on_batch(count)
    except sqlite3.Error:
        return count, "خواندن رکوردها ممکن نشد."
    return count, None


def _integrity_errors(conn: sqlite3.Connection) -> tuple[str, ...]:
    try:
        rows = conn.execute("PRAGMA integrity_check").fetchall()
    except sqlite3.Error:
        return ("ساختار فایل خراب است.",)
    messages = [str(row[0]) for row in rows if str(row[0]) != "ok"]
    return tuple(messages)


def _foreign_key_errors(conn: sqlite3.Connection) -> tuple[str, ...]:
    try:
        rows = conn.execute("PRAGMA foreign_key_check").fetchall()
    except sqlite3.Error:
        return ("ارتباط جدول‌ها بررسی نشد.",)
    findings: list[str] = []
    for row in rows:
        table = table_label(str(row[0]))
        parent = table_label(str(row[2]))
        findings.append(f"جدول {table} به {parent} وصل نیست (ردیف {row[1]}).")
    return tuple(findings)


def _file_error(exc: sqlite3.Error) -> str:
    text = str(exc).lower()
    if "not a database" in text or "malformed" in text or "disk image" in text:
        return "این فایل پایگاه دادهٔ سالمی نیست."
    if "unable to open" in text:
        return "فایل باز نشد."
    return "این فایل پایگاه دادهٔ سالمی نیست."


def _join_findings(findings: tuple[str, ...]) -> str:
    shown = list(findings[:8])
    extra = len(findings) - len(shown)
    text = "؛ ".join(shown)
    if extra > 0:
        text += f"؛ و {extra} مورد دیگر"
    return text


def _export_path(folder: Path, moment: datetime) -> Path:
    stamp = moment.strftime("%Y-%m-%d_%H-%M-%S")
    path = folder / f"atten-backup-{stamp}.db"
    counter = 2
    while path.exists():
        path = folder / f"atten-backup-{stamp}-{counter}.db"
        counter += 1
    return path


def _discard_sidecars(db_file: Path) -> None:
    if db_file.exists():
        try:
            conn = sqlite3.connect(db_file, timeout=5)
            try:
                conn.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchall()
            finally:
                conn.close()
        except sqlite3.Error:
            log.exception("checkpoint before restore failed for %s", db_file)
    _remove_sidecars(db_file)


def _snapshot_path(folder: Path) -> Path:
    stamp = datetime.now().strftime("%Y-%m-%dT%H%M%S%f")
    path = folder / f"atten-{stamp}.db"
    counter = 2
    while path.exists():
        path = folder / f"atten-{stamp}-{counter}.db"
        counter += 1
    return path


def _prune(folder: Path, keep: int) -> None:
    files = sorted(folder.glob(_SNAPSHOT_GLOB), key=lambda path: path.stat().st_mtime)
    extras = files[:-keep] if keep > 0 else files
    for old in extras:
        old.unlink(missing_ok=True)


def _quarantine(db_file: Path) -> None:
    folder = db_file.parent / "corrupt"
    folder.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y-%m-%dT%H%M%S%f")
    for path in (db_file, *_sidecars(db_file)):
        if path.exists():
            path.replace(folder / f"{path.name}.{stamp}")


def _sidecars(db_file: Path) -> tuple[Path, Path]:
    name = str(db_file)
    return Path(name + "-wal"), Path(name + "-shm")


def _remove_sidecars(db_file: Path) -> None:
    for path in _sidecars(db_file):
        path.unlink(missing_ok=True)


def _fsync(path: Path) -> None:
    with path.open("r+b") as handle:
        os.fsync(handle.fileno())
