"""SQLite connection settings for the portable database file.

WAL with synchronous=FULL keeps a finished save in a side file that SQLite
replays after power loss. Each committed change is also copied to data/backups.
"""

import logging
import sqlite3
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path

log = logging.getLogger(__name__)


@contextmanager
def connect(db_file: Path) -> Iterator[sqlite3.Connection]:
    db_file.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(db_file, timeout=5)
    try:
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA busy_timeout = 5000")
        conn.execute("PRAGMA journal_mode = WAL")
        conn.execute("PRAGMA synchronous = FULL")
        conn.execute("PRAGMA foreign_keys = ON")
        before = conn.total_changes
        yield conn
    finally:
        changed = conn.total_changes != before
        conn.close()
        if changed:
            from atten.db.backup import snapshot

            try:
                snapshot(db_file)
            except Exception:
                log.exception("database snapshot failed for %s", db_file)
