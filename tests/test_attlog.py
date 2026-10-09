from atten.attlog import parse_attlog
from atten.db.migrate import migrate
from atten.db.repository import import_clock_events, list_clock_events


LOG = (
    "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n"
    "\n"
    "1008\tjamalian\t2026-10-06 18:03:50\t1\t1\n"
    "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n"
)


def test_parse_attlog_keeps_remote_id_name_and_splits_the_clock(tmp_path):
    rows = parse_attlog(LOG)

    assert [(row.line_number, row.remote_id, row.name, row.event_date, row.event_time) for row in rows] == [
        (1, "1002", "Karimi", "2026-09-23", "20:11:58"),
        (3, "1008", "jamalian", "2026-10-06", "18:03:50"),
        (4, "1002", "Karimi", "2026-09-23", "20:11:58"),
    ]


def test_parse_attlog_rejects_a_short_line():
    try:
        parse_attlog("1002\tKarimi\n")
    except ValueError as exc:
        assert str(exc) == "سطر 1 ناقص است."
    else:
        raise AssertionError("short line was accepted")


def test_import_stores_every_row_without_a_personnel_record(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")

    first = import_clock_events(parse_attlog(LOG), db_file=db_file)
    second = import_clock_events(parse_attlog(LOG), db_file=db_file)

    assert first.added == 2
    assert first.skipped == 1
    assert second.added == 0
    assert second.skipped == 3
    assert list_clock_events(db_file) == [
        {
            "id": 1,
            "remote_id": "1002",
            "name": "Karimi",
            "date": "2026-09-23",
            "time": "20:11:58",
            "created_at": "2026-10-07T16:30:00",
            "updated_at": "2026-10-07T16:30:00",
        },
        {
            "id": 2,
            "remote_id": "1008",
            "name": "jamalian",
            "date": "2026-10-06",
            "time": "18:03:50",
            "created_at": "2026-10-07T16:30:00",
            "updated_at": "2026-10-07T16:30:00",
        },
    ]


def test_import_rejects_a_bad_clock_without_writing(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)

    try:
        import_clock_events(
            parse_attlog("1002\tKarimi\t2026-09-23 25:00:00\t1\t0\n"),
            db_file=db_file,
        )
    except ValueError as exc:
        assert str(exc) == "سطر 1: ساعت معتبر نیست."
    else:
        raise AssertionError("invalid clock was stored")

    assert list_clock_events(db_file) == []
