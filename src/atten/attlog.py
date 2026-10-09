"""Read a tab-separated attendance log.

Column 1 is the personnel remote id, column 2 is the name, and column 3 is a
Gregorian date and clock time separated by a space. Later columns are ignored.
"""

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class AttlogRow:
    line_number: int
    remote_id: str
    name: str
    event_date: str
    event_time: str


def load_attlog(path: Path) -> list[AttlogRow]:
    try:
        text = path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        text = path.read_text(encoding="cp1256")
    except OSError as exc:
        raise ValueError("خواندن فایل ممکن نشد.") from exc
    return parse_attlog(text)


def parse_attlog(text: str) -> list[AttlogRow]:
    rows: list[AttlogRow] = []
    for line_number, raw in enumerate(text.splitlines(), start=1):
        if not raw.strip():
            continue
        parts = raw.split("\t")
        if len(parts) < 3:
            raise ValueError(f"سطر {line_number} ناقص است.")
        event_date, separator, event_time = parts[2].strip().partition(" ")
        if separator == "" or not event_date or not event_time:
            raise ValueError(f"سطر {line_number} تاریخ یا ساعت ندارد.")
        rows.append(
            AttlogRow(
                line_number=line_number,
                remote_id=parts[0].strip(),
                name=parts[1].strip(),
                event_date=event_date,
                event_time=event_time,
            )
        )
    if not rows:
        raise ValueError("فایل ردیفی ندارد.")
    return rows
