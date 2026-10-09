"""Apply numbered SQL migrations that have not been recorded yet."""

from datetime import datetime, timezone
from pathlib import Path

from atten.db.connection import connect


def migrate(db_file: Path) -> list[str]:
    applied_now: list[str] = []
    with connect(db_file) as conn:
        done = _applied_versions(conn)
        for version, script_path in _migration_files():
            if version in done:
                continue
            conn.executescript(script_path.read_text(encoding="utf-8"))
            conn.execute(
                "INSERT INTO schema_migrations (version, applied_at) VALUES (?, ?)",
                (version, datetime.now(timezone.utc).isoformat()),
            )
            applied_now.append(version)
        conn.commit()
    return applied_now


def _migration_files() -> list[tuple[str, Path]]:
    folder = Path(__file__).resolve().parent / "migrations"
    found: list[tuple[str, Path]] = []
    for script_path in sorted(folder.glob("*.sql")):
        version = script_path.name.split("_", 1)[0]
        found.append((version, script_path))
    return found


def _applied_versions(conn) -> set[str]:
    exists = conn.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'"
    ).fetchone()
    if exists is None:
        return set()
    rows = conn.execute("SELECT version FROM schema_migrations").fetchall()
    return {row["version"] for row in rows}
