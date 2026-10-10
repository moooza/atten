import tkinter as tk
from datetime import date

from atten.dates import (
    format_shamsi_date,
    format_shamsi_datetime,
    format_shamsi_weekday,
    parse_shamsi_date,
)
from atten.leave import compose_leave_minutes, shamsi_year_of
from atten.db.connection import connect
from atten.db.migrate import migrate
from atten.db.repository import (
    add_clock_event,
    add_leave,
    add_personnel,
    list_clock_events,
    list_leaves,
    list_personnel,
    list_work_balances,
    list_work_punches,
)
from atten import __version__
from atten.ui.main_window import MainWindow
from atten.ui.typing import on_entry_key


def test_dashboard_button_is_before_personnel_and_opens_an_empty_page(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()

        assert root.title() == f"attendance نسخه {__version__}"
        assert __version__ == "1.3"
        assert window.nav_items["dashboard"].caption.cget("text") == "داشبورد"
        buttons = window.toolbar.pack_slaves()
        assert buttons.index(window.nav_items["dashboard"]) < buttons.index(window.nav_items["personnel"])
        assert window.dashboard_view.winfo_ismapped()
        assert window.dashboard_view.winfo_children() == []
        assert not window.personnel_view.winfo_ismapped()

        window.nav_items["personnel"].event_generate("<Button-1>")
        root.update()
        assert window.personnel_view.winfo_ismapped()
        assert not window.dashboard_view.winfo_ismapped()

        window.nav_items["dashboard"].event_generate("<Button-1>")
        root.update()
        assert window.dashboard_view.winfo_ismapped()
        assert window.dashboard_view.winfo_children() == []
        assert not window.personnel_view.winfo_ismapped()
    finally:
        root.destroy()


def test_restore_notice_stays_above_the_pages(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file, notice="برگشت از نسخهٔ پشتیبان")
        root.update()
        assert window.notice_banner is not None
        assert window.notice_banner.cget("text") == "برگشت از نسخهٔ پشتیبان"
        assert window.notice_banner.winfo_ismapped()
        assert window.dashboard_view.winfo_children() == []
    finally:
        root.destroy()


def test_personnel_menu_shows_saved_people(tmp_path):
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
            (None, "مریم", "احمدی", 8, None),
        )
        conn.commit()

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()

        stacked = window.pack_slaves()
        assert stacked.index(window.toolbar) < stacked.index(window.content)
        assert not window.personnel_view.winfo_ismapped()
        assert window.nav_items["personnel"].caption.cget("text") == "پرسنل"

        window.nav_items["personnel"].event_generate("<Button-1>")
        root.update()

        assert window.personnel_view.winfo_ismapped()
        assert window.personnel_view.count.cget("text") == "2 نفر"
        values = [
            window.personnel_view.tree.item(item, "values")
            for item in window.personnel_view.tree.get_children()
        ]
        assert values == [
            ("", "", "09120000000", "4.5", "رضایی", "علی", "dev-1", "1"),
            ("", "", "", "8", "احمدی", "مریم", "", "2"),
        ]
    finally:
        root.destroy()


def test_add_personnel_form_saves_and_shows_the_new_row(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_personnel()
        root.update()

        assert window.personnel_view.add_button.caption.cget("text") == "افزودن پرسنل"
        window.personnel_view.add_button.event_generate("<Button-1>")
        root.update()
        form = window.personnel_view.form
        assert form is not None
        assert form.title() == "افزودن پرسنل"

        form.work_hours.set("0")
        form.work_minutes.set("0")
        form.first_name.set("علی")
        form.last_name.set("رضایی")
        form.save()
        assert form.winfo_exists()
        assert "ساعت کاری" in form.error.cget("text")

        form.minutes_entry.delete(0, tk.END)
        form.minutes_entry.insert(0, "59")
        assert form.work_minutes.get() == "59"
        form.minutes_entry.insert(tk.END, "9")
        assert form.work_minutes.get() == "59"

        form.work_minutes.set("60")
        form.save()
        assert form.winfo_exists()
        assert "۵۹" in form.error.cget("text")

        form.work_hours.set("۴")
        form.work_minutes.set("۳۰")
        form.remote_id.set("dev-9")
        form.mobile.set("09123334444")
        monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
        form.save()
        root.update()

        assert not form.winfo_exists()
        assert window.personnel_view.count.cget("text") == "1 نفر"
        values = [
            window.personnel_view.tree.item(item, "values")
            for item in window.personnel_view.tree.get_children()
        ]
        added = format_shamsi_datetime("2026-10-07T16:30:00")
        assert values == [(added, added, "09123334444", "4.5", "رضایی", "علی", "dev-9", "1")]
    finally:
        root.destroy()


def test_add_personnel_form_stores_cooperation_dates_as_gregorian(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_personnel()
        root.update()
        window.personnel_view.add_button.event_generate("<Button-1>")
        root.update()
        form = window.personnel_view.form
        assert form is not None
        assert form.start_field.winfo_ismapped()
        assert form.end_field.winfo_ismapped()

        form.first_name.set("علی")
        form.last_name.set("رضایی")
        form.work_hours.set("8")
        form.work_minutes.set("0")
        form.cooperation_end.set("1405/07/01")
        form.save()
        assert form.winfo_exists()
        assert "تاریخ شروع همکاری" in form.error.cget("text")

        form.cooperation_start.set("1405/07/15")
        form.cooperation_end.set("1405/07/01")
        form.save()
        assert form.winfo_exists()
        assert "تاریخ پایان همکاری" in form.error.cget("text")

        form.cooperation_start.set("1405/07/01")
        form.cooperation_end.set("1405/07/15")
        form.save()
        root.update()

        assert not form.winfo_exists()
        person = list_personnel(db_file)[0]
        assert person["cooperation_start"] == "2026-09-23"
        assert person["cooperation_end"] == "2026-10-07"

        window.personnel_view.tree.selection_set("1")
        window.personnel_view.edit_button.event_generate("<Button-1>")
        root.update()
        edited = window.personnel_view.form
        assert edited is not None
        assert edited.cooperation_start.get() == format_shamsi_date("2026-09-23")
        assert edited.cooperation_end.get() == format_shamsi_date("2026-10-07")
        edited.destroy()
        root.update()
    finally:
        root.destroy()


def test_personnel_name_entry_replaces_arabic_letters(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_personnel()
        root.update()
        window.personnel_view.add_button.event_generate("<Button-1>")
        root.update()
        form = window.personnel_view.form
        assert form is not None
        entry = form.first_name_entry
        entry.focus_set()
        root.update()

        typed = tk.Event()
        typed.widget = entry
        typed.char = "\u064a"
        assert on_entry_key(typed) == "break"
        assert entry.get() == "\u06cc"
        assert form.first_name.get() == "\u06cc"

        entry.insert(tk.END, "\u0643اظ\u0649")
        entry.event_generate("<KeyRelease>")
        root.update()
        assert entry.get() == "\u06cc\u06a9اظ\u06cc"

        form.last_name.set("رضا\u064a\u0643")
        form.work_hours.set("۸")
        form.work_minutes.set("۰")
        form.remote_id.set("۱۲۳")
        form.mobile.set("۰۹۱۲۳۳۳۴۴۴۴")
        monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
        form.save()
        root.update()

        assert not form.winfo_exists()
        person = list_personnel(db_file)[0]
        assert person["first_name"] == "\u06cc\u06a9اظ\u06cc"
        assert person["last_name"] == "رضا\u06cc\u06a9"
        assert person["remote_id"] == "123"
        assert person["mobile"] == "09123334444"
        assert person["daily_hours"] == 8
    finally:
        root.destroy()


def test_edit_button_updates_the_selected_person(tmp_path, monkeypatch):
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

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_personnel()
        root.update()
        view = window.personnel_view

        assert view.edit_button.caption.cget("text") == "ویرایش"
        view.edit_button.event_generate("<Button-1>")
        root.update()
        assert view.notice.cget("text") == "یک پرسنل را از لیست انتخاب کنید."
        assert view.form is None

        view.tree.selection_set(view.tree.get_children()[0])
        view.edit_button.event_generate("<Button-1>")
        root.update()
        form = view.form
        assert form.title() == "ویرایش پرسنل"
        assert form.person_id == 1
        assert form.first_name.get() == "علی"
        assert form.work_hours.get() == "4"
        assert form.work_minutes.get() == "30"

        form.last_name.set("کاظمی")
        form.work_hours.set("8")
        form.work_minutes.set("0")
        monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T18:05:00")
        form.save()
        root.update()

        assert not form.winfo_exists()
        values = [
            view.tree.item(item, "values")
            for item in view.tree.get_children()
        ]
        changed = format_shamsi_datetime("2026-10-07T18:05:00")
        assert values == [(changed, "", "09120000000", "8", "کاظمی", "علی", "dev-1", "1")]
    finally:
        root.destroy()


def test_clock_events_menu_shows_punches_in_shamsi(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)
    add_clock_event("dev-1", "علی رضایی", "2026-10-07", "08:30:00", db_file=db_file)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()

        buttons = window.toolbar.pack_slaves()
        assert buttons.index(window.nav_items["personnel"]) < buttons.index(window.nav_items["clock_events"])
        assert window.nav_items["clock_events"].caption.cget("text") == "ورود و خروج"
        assert not window.clock_events_view.winfo_ismapped()

        window.nav_items["clock_events"].event_generate("<Button-1>")
        root.update()

        assert window.clock_events_view.winfo_ismapped()
        assert not window.personnel_view.winfo_ismapped()
        assert window.clock_events_view.count.cget("text") == "1 مورد"
        assert window.clock_events_view.tree.heading("weekday")["text"] == "روز"
        assert window.clock_events_view.tree["columns"][3:5] == ("date", "weekday")
        values = [
            window.clock_events_view.tree.item(item, "values")
            for item in window.clock_events_view.tree.get_children()
        ]
        added = format_shamsi_datetime("2026-10-07T16:30:00", seconds=True)
        assert values == [
            (
                added,
                added,
                "08:30:00",
                format_shamsi_date("2026-10-07"),
                format_shamsi_weekday("2026-10-07"),
                "علی رضایی",
                "dev-1",
                "1",
            )
        ]

        window.nav_items["dashboard"].event_generate("<Button-1>")
        root.update()
        assert window.dashboard_view.winfo_ismapped()
        assert not window.clock_events_view.winfo_ismapped()
    finally:
        root.destroy()


def test_personnel_menu_shows_empty_state(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    root = tk.Tk()
    root.withdraw()
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_personnel()
        root.update_idletasks()
        assert window.personnel_view.empty.cget("text") == "هنوز پرسنلی ثبت نشده است."
        assert window.personnel_view.tree.get_children() == ()

        window.show_clock_events()
        root.update_idletasks()
        assert window.clock_events_view.winfo_manager() == "pack"
        assert window.personnel_view.winfo_manager() == ""
        assert window.clock_events_view.empty.cget("text") == "هنوز ورود و خروجی ثبت نشده است."
        assert window.clock_events_view.tree.get_children() == ()
    finally:
        root.destroy()


def test_clock_events_import_shows_the_loaded_file(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    log_file = tmp_path / "attlog.dat"
    log_file.write_text(
        "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n1008\tjamalian\t2026-10-06 18:03:50\t1\t1\n",
        encoding="utf-8",
    )

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_clock_events()
        root.update()
        view = window.clock_events_view

        assert view.import_button.caption.cget("text") == "بارگذاری فایل"
        view.import_file(log_file)
        root.update()

        assert view.notice.cget("text") == "2 مورد افزوده شد."
        assert view.count.cget("text") == "2 مورد"
        values = [
            view.tree.item(item, "values")
            for item in view.tree.get_children()
        ]
        added = format_shamsi_datetime("2026-10-07T16:30:00", seconds=True)
        assert values == [
            (
                added,
                added,
                "20:11:58",
                format_shamsi_date("2026-09-23"),
                format_shamsi_weekday("2026-09-23"),
                "Karimi",
                "1002",
                "1",
            ),
            (
                added,
                added,
                "18:03:50",
                format_shamsi_date("2026-10-06"),
                format_shamsi_weekday("2026-10-06"),
                "jamalian",
                "1008",
                "2",
            ),
        ]
    finally:
        root.destroy()


def test_calculation_pairs_each_days_punches_as_entry_then_exit(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_personnel("علی", "رضایی", 8, remote_id="dev-1", db_file=db_file)
    add_personnel("مریم", "احمدی", 8, remote_id="dev-2", db_file=db_file)
    add_personnel("سارا", "کریمی", 8, db_file=db_file)
    punches = ("08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00")
    for clock in punches:
        add_clock_event("dev-1", "علی رضایی", "2026-10-07", clock, db_file=db_file)
    add_clock_event("dev-1", "علی رضایی", "2026-10-08", "09:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی رضایی", "2026-10-06", "07:00:00", db_file=db_file)
    add_clock_event("dev-2", "مریم احمدی", "2026-10-07", "10:00:00", db_file=db_file)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()

        buttons = window.toolbar.pack_slaves()
        assert buttons.index(window.nav_items["clock_events"]) < buttons.index(window.nav_items["calculation"])
        assert window.nav_items["calculation"].caption.cget("text") == "محاسبه"
        assert not window.calculation_view.winfo_ismapped()

        window.nav_items["calculation"].event_generate("<Button-1>")
        root.update()
        view = window.calculation_view
        assert view.winfo_ismapped()
        assert not window.clock_events_view.winfo_ismapped()
        assert list(view.person_combo["values"]) == [
            "علی رضایی (dev-1)",
            "مریم احمدی (dev-2)",
            "سارا کریمی",
        ]

        view.end_date.set("1405/07/01")
        view.end_field.open_calendar()
        root.update()
        popup = view.end_field.popup
        assert popup is not None
        assert popup.month_label.cget("text") == "مهر 1405"
        popup.previous.invoke()
        root.update()
        assert popup.month_label.cget("text") == "شهریور 1405"
        popup.next.invoke()
        root.update()
        popup.days[16].invoke()
        root.update()
        assert view.end_date.get() == "1405/07/16"
        assert view.end_field.popup is None

        view.person_combo.current(0)
        view.start_date.set("1405/07/15")
        view.end_date.set("1405/07/17")
        view.show_button.event_generate("<Button-1>")
        root.update()

        headings = [view.tree.heading(column, "text") for column in view.tree["columns"]]
        assert headings == [
            "عملیات",
            "مرخصی",
            "کسری / اضافه",
            "خروج",
            "ورود",
            "خروج",
            "ورود",
            "خروج",
            "ورود",
            "تاریخ",
            "روز",
            "روز تعطیل",
        ]
        values = [view.tree.item(item, "values") for item in view.tree.get_children()]
        assert values == [
            (
                "",
                "",
                "+3.00",
                "21:00:00",
                "18:00:00",
                "17:00:00",
                "13:00:00",
                "12:00:00",
                "08:00:00",
                "1405/07/15",
                format_shamsi_weekday("2026-10-07"),
                "☐",
            ),
            (
                "",
                "",
                "-8.00",
                "",
                "",
                "",
                "",
                "",
                "09:00:00",
                "1405/07/16",
                format_shamsi_weekday("2026-10-08"),
                "☐",
            ),
            (
                "",
                "",
                "0.00",
                "",
                "",
                "",
                "",
                "",
                "",
                "1405/07/17",
                format_shamsi_weekday("2026-10-09"),
                "☑",
            ),
        ]
        assert "incomplete" not in view.tree.item("2026-10-07", "tags")
        assert "incomplete" in view.tree.item("2026-10-08", "tags")
        assert "incomplete" not in view.tree.item("2026-10-09", "tags")
        device_rows = list_clock_events(db_file)

        view.begin_edit("2026-10-08", "p1")
        root.update()
        assert view.editor is not None
        assert view.editor.get() == ""
        view.editor.delete(0, "end")
        view.editor.insert(0, "17:30")
        view.commit_edit()
        root.update()

        assert view.editor is None
        assert view.tree.item("2026-10-08", "values") == (
            "",
            "",
            "+0.30",
            "",
            "",
            "",
            "",
            "17:30:00",
            "09:00:00",
            "1405/07/16",
            format_shamsi_weekday("2026-10-08"),
            "☐",
        )
        assert "incomplete" not in view.tree.item("2026-10-08", "tags")
        assert list_clock_events(db_file) == device_rows
        assert list_work_punches("dev-1", "2026-10-07", "2026-10-09", db_file=db_file) == {
            "2026-10-08": {1: "17:30:00"},
        }
        assert list_work_balances("dev-1", "2026-10-07", "2026-10-09", db_file=db_file) == [
            {"remote_id": "dev-1", "date": "2026-10-07", "balance_minutes": 180, "holiday": 0},
            {"remote_id": "dev-1", "date": "2026-10-08", "balance_minutes": 30, "holiday": 0},
            {"remote_id": "dev-1", "date": "2026-10-09", "balance_minutes": 0, "holiday": 1},
        ]

        view.toggle_holiday("2026-10-08")
        root.update()
        assert view.tree.set("2026-10-08", "holiday") == "☑"
        assert view.tree.set("2026-10-08", "balance") == "+8.30"
        assert list_clock_events(db_file) == device_rows

        view.toggle_holiday("2026-10-09")
        root.update()
        assert view.tree.set("2026-10-09", "holiday") == "☐"
        assert view.tree.set("2026-10-09", "balance") == "-8.00"
        assert "incomplete" in view.tree.item("2026-10-09", "tags")

        view.save_slot("2026-10-07", 0, "25:00")
        root.update()
        assert "ساعت" in view.notice.cget("text")
        assert view.tree.item("2026-10-07", "values")[8] == "08:00:00"
        assert list_clock_events(db_file) == device_rows

        view.person_combo.current(2)
        view.show_button.event_generate("<Button-1>")
        root.update()
        assert view.empty.cget("text") == "برای این کارمند Remote ID ثبت نشده است."
        assert view.tree.get_children() == ()

        window.nav_items["dashboard"].event_generate("<Button-1>")
        root.update()
        assert window.dashboard_view.winfo_ismapped()
        assert not view.winfo_ismapped()
    finally:
        root.destroy()


def test_calculation_uses_leave_to_cover_a_short_day_and_a_multi_day_absence(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    daily_hours = 7 + 20 / 60
    person_id = add_personnel(
        "هانیه",
        "ابراهیمی",
        daily_hours,
        remote_id="1013",
        cooperation_start=parse_shamsi_date("1405/01/01"),
        db_file=db_file,
    )
    add_clock_event("1013", "هانیه ابراهیمی", "2026-09-30", "09:43:10", db_file=db_file)
    add_clock_event("1013", "هانیه ابراهیمی", "2026-09-30", "13:50:33", db_file=db_file)
    add_leave(person_id, "2026-09-30", "2026-09-30", 193, db_file=db_file)
    add_leave(
        person_id,
        "2026-10-05",
        "2026-10-07",
        compose_leave_minutes(3, 0, 0),
        db_file=db_file,
    )

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_calculation()
        root.update()
        view = window.calculation_view
        view.person_combo.current(0)
        view.start_date.set("1405/07/08")
        view.end_date.set("1405/07/08")
        view.show_button.event_generate("<Button-1>")
        root.update()

        assert view.tree.set("2026-09-30", "balance") == "0.00"
        assert view.tree.set("2026-09-30", "leave") == "\u202b3 ساعت و 13 دقیقه\u202c"
        assert view.tree.tag_configure("leave")["background"] == "#e5f6ea"
        assert "leave" in view.tree.item("2026-09-30", "tags")
        assert "incomplete" not in view.tree.item("2026-09-30", "tags")

        view.start_date.set("1405/07/13")
        view.end_date.set("1405/07/16")
        view.show_button.event_generate("<Button-1>")
        root.update()
        assert [view.tree.set(day, "balance") for day in view.tree.get_children()] == [
            "0.00",
            "0.00",
            "0.00",
            "-7.20",
        ]
        assert [view.tree.set(day, "leave") for day in view.tree.get_children()] == [
            "\u202b7 ساعت و 20 دقیقه\u202c",
            "\u202b7 ساعت و 20 دقیقه\u202c",
            "\u202b7 ساعت و 20 دقیقه\u202c",
            "",
        ]
        assert "leave" in view.tree.item("2026-10-05", "tags")
        assert "leave" in view.tree.item("2026-10-07", "tags")
        assert "leave" not in view.tree.item("2026-10-08", "tags")
        assert "incomplete" not in view.tree.item("2026-10-05", "tags")
        assert "incomplete" not in view.tree.item("2026-10-07", "tags")
        assert "incomplete" in view.tree.item("2026-10-08", "tags")
    finally:
        root.destroy()


def test_calculation_resets_a_day_and_prefills_leave_for_a_shortfall(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    person_id = add_personnel(
        "علی",
        "رضایی",
        8,
        remote_id="dev-1",
        cooperation_start=parse_shamsi_date("1405/01/01"),
        db_file=db_file,
    )
    add_clock_event("dev-1", "علی رضایی", "2026-10-08", "09:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی رضایی", "2026-10-07", "08:00:00", db_file=db_file)
    add_clock_event("dev-1", "علی رضایی", "2026-10-07", "17:00:00", db_file=db_file)
    device_rows = list_clock_events(db_file)
    answers = [False, True]
    prompts: list[tuple[str, str]] = []

    def ask(title: str, message: str, **_kwargs: object) -> bool:
        prompts.append((title, message))
        return answers.pop(0)

    monkeypatch.setattr("atten.ui.calculation_view.messagebox.askyesno", ask)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_calculation()
        root.update()
        view = window.calculation_view
        view.person_combo.current(0)
        view.start_date.set("1405/07/15")
        view.end_date.set("1405/07/17")
        view.show_button.event_generate("<Button-1>")
        root.update()

        full_day = view.actions["2026-10-07"]
        short_day = view.actions["2026-10-08"]
        holiday = view.actions["2026-10-09"]
        assert full_day.reset_button.caption.cget("text") == "بازنشانی"
        assert full_day.leave_button is None
        assert short_day.leave_button is not None
        assert short_day.leave_button.caption.cget("text") == "افزودن مرخصی"
        assert holiday.leave_button is None
        assert short_day.reset_button.winfo_ismapped()

        view.begin_edit("2026-10-08", "p1")
        root.update()
        assert view.editor is not None
        view.editor.insert(0, "17:30")
        view.commit_edit()
        root.update()
        assert view.tree.set("2026-10-08", "balance") == "+0.30"
        assert view.actions["2026-10-08"].leave_button is None

        view.actions["2026-10-08"].reset_button.event_generate("<Button-1>")
        root.update()
        assert prompts == [
            (
                "بازنشانی ورود و خروج",
                "ورود و خروج 1405/07/16 پاک شود و دوباره از جدول ورود و خروج خوانده شود؟",
            )
        ]
        assert list_work_punches("dev-1", "2026-10-08", "2026-10-08", db_file=db_file) == {
            "2026-10-08": {1: "17:30:00"},
        }

        view.actions["2026-10-08"].reset_button.event_generate("<Button-1>")
        root.update()
        assert view.tree.set("2026-10-08", "p0") == "09:00:00"
        assert view.tree.set("2026-10-08", "p1") == ""
        assert view.tree.set("2026-10-08", "balance") == "-8.00"
        assert view.tree.set("2026-10-09", "holiday") == "☑"
        assert list_work_punches("dev-1", "2026-10-07", "2026-10-09", db_file=db_file) == {}
        assert list_clock_events(db_file) == device_rows
        assert view.actions["2026-10-08"].leave_button is not None

        view.actions["2026-10-08"].leave_button.event_generate("<Button-1>")
        root.update()
        form = view.form
        assert form is not None
        assert form.title() == "ثبت مرخصی"
        assert form.person_combo.get() == "علی رضایی (dev-1)"
        assert form.start_date.get() == "1405/07/16"
        assert form.end_date.get() == "1405/07/16"
        assert form.leave_days.get() == "1"
        assert form.leave_hours.get() == "0"
        assert form.leave_minutes.get() == "40"
        assert form.preview.cget("text") == "معادل: 1 روز و 0 ساعت و 40 دقیقه"
        form.save()
        root.update()

        assert not form.winfo_exists()
        assert view.tree.set("2026-10-08", "balance") == "0.00"
        assert view.tree.set("2026-10-08", "leave") == "\u202b8 ساعت\u202c"
        assert "leave" in view.tree.item("2026-10-08", "tags")
        assert view.actions["2026-10-08"].leave_button is None
        stored = list_leaves(person_id, db_file=db_file)
        assert len(stored) == 1
        assert stored[0]["start_date"] == "2026-10-08"
        assert stored[0]["end_date"] == "2026-10-08"
        assert stored[0]["minutes"] == compose_leave_minutes(1, 0, 40)
    finally:
        root.destroy()


def _balance_text(body: tk.Misc) -> str:
    """Reading order of a balance card: the first packed run sits on the right."""
    lines = []
    for row in body.pack_slaves():
        parts = row.pack_slaves()
        assert parts
        assert {part.pack_info()["side"] for part in parts} == {"right"}
        lines.append("".join(part.cget("text") for part in parts))
    return "\n".join(lines)


def test_leaves_menu_lists_a_person_and_subtracts_the_yearly_allowance(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    year = shamsi_year_of(date.today())
    add_personnel(
        "علی",
        "رضایی",
        8,
        remote_id="dev-1",
        cooperation_start=parse_shamsi_date(f"{year:04d}/01/01"),
        db_file=db_file,
    )
    leave_day = parse_shamsi_date(f"{year:04d}/06/15")
    shown = format_shamsi_date(leave_day)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()

        buttons = window.toolbar.pack_slaves()
        assert buttons.index(window.nav_items["clock_events"]) < buttons.index(window.nav_items["leaves"])
        assert buttons.index(window.nav_items["leaves"]) < buttons.index(window.nav_items["calculation"])
        assert window.nav_items["leaves"].caption.cget("text") == "مرخصی‌ها"
        assert not window.leaves_view.winfo_ismapped()

        window.nav_items["leaves"].event_generate("<Button-1>")
        root.update()
        view = window.leaves_view
        assert view.winfo_ismapped()
        assert not window.clock_events_view.winfo_ismapped()
        assert view.empty.cget("text") == "یک پرسنل را انتخاب کنید."
        assert list(view.person_combo["values"]) == ["علی رضایی (dev-1)"]

        view.person_combo.current(0)
        view.person_combo.event_generate("<<ComboboxSelected>>")
        root.update()
        assert view.checked_years == {year}
        assert view.year_button.caption.cget("text") == f"سال {year}"
        cards = view.year_rows[year]
        assert cards.year_label.cget("text") == f"سال {year}"
        assert cards.notice is None
        assert list(cards.line.pack_slaves()) == [
            cards.carry_card,
            cards.earned_card,
            cards.used_card,
            cards.remaining_card,
            cards.transfer_card,
        ]
        assert cards.carry_card.winfo_children()[0].cget("text") == "ذخیره (از سال قبل)"
        assert cards.earned_card.winfo_children()[0].cget("text") == "استحقاق"
        assert cards.used_card.winfo_children()[0].cget("text") == "ثبت شده"
        assert cards.remaining_card.winfo_children()[0].cget("text") == "مانده"
        assert cards.transfer_card.winfo_children()[0].cget("text") == "انتقال (به سال بعد)"
        assert _balance_text(cards.carry_label) == "0 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(cards.earned_label) == "30 روز و 0 ساعت و 0 دقیقه"
        assert cards.earned_label.pack_slaves()[0].pack_slaves()[0].cget("text") == "30"
        assert _balance_text(cards.used_label) == "0 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(cards.remaining_label) == (
            "قبل از انتقال 30 روز و 0 ساعت و 0 دقیقه\n"
            "بعد از انتقال 21 روز و 0 ساعت و 0 دقیقه"
        )
        assert _balance_text(cards.transfer_label) == "9 روز و 0 ساعت و 0 دقیقه"
        assert view.empty.cget("text") == "برای این پرسنل مرخصی ثبت نشده است."

        view.add_button.event_generate("<Button-1>")
        root.update()
        form = view.form
        assert form is not None
        assert form.title() == "ثبت مرخصی"
        assert form.save_button.caption.cget("text") == "ثبت"
        assert form.person_combo.get() == "علی رضایی (dev-1)"
        assert [
            widget.cget("text")
            for widget in form.duration_row.pack_slaves()
            if isinstance(widget, tk.Label)
        ] == ["روز", "ساعت", "دقیقه"]

        form.save()
        assert form.winfo_exists()
        assert "تاریخ" in form.error.cget("text")

        form.start_date.set(shown)
        form.end_date.set(shown)
        form.leave_minutes.set("60")
        form.save()
        assert form.winfo_exists()
        assert "۵۹" in form.error.cget("text")

        form.leave_days.set("۱")
        form.leave_hours.set("2")
        form.leave_minutes.set("15")
        root.update()
        assert form.preview.cget("text") == "معادل: 1 روز و 2 ساعت و 15 دقیقه"
        form.save()
        root.update()

        assert not form.winfo_exists()
        assert view.checked_years == {year}
        assert view.count.cget("text") == "1 مورد"
        added = format_shamsi_datetime("2026-10-07T16:30:00")
        values = [view.tree.item(item, "values") for item in view.tree.get_children()]
        assert values == [(added, "1 روز و 2 ساعت و 15 دقیقه", shown, shown, "1")]
        amount_runs = view._amount_cells[1].labels
        assert amount_runs[0].cget("text") == "1"
        assert amount_runs[0].pack_info()["side"] == "right"
        cards = view.year_rows[year]
        assert _balance_text(cards.used_label) == "1 روز و 2 ساعت و 15 دقیقه"
        assert _balance_text(cards.remaining_label) == (
            "قبل از انتقال 28 روز و 5 ساعت و 5 دقیقه\n"
            "بعد از انتقال 19 روز و 5 ساعت و 5 دقیقه"
        )
        assert _balance_text(cards.transfer_label) == "9 روز و 0 ساعت و 0 دقیقه"

        view.year_button.event_generate("<Button-1>")
        root.update()
        popup = view.year_popup
        assert popup is not None
        assert list(popup.variables) == [year]
        assert popup.variables[year].get() is True
        popup.checks[year].invoke()
        root.update()
        assert view.checked_years == set()
        assert view.year_rows == {}
        assert view.tree.get_children() == ()
        assert view.empty.cget("text") == "سال را انتخاب کنید."

        popup.checks[year].invoke()
        root.update()
        assert view.checked_years == {year}
        assert view.count.cget("text") == "1 مورد"
        assert view.tree.get_children() != ()

        window.nav_items["clock_events"].event_generate("<Button-1>")
        root.update()
        assert window.clock_events_view.winfo_ismapped()
        assert not view.winfo_ismapped()
    finally:
        root.destroy()


def test_edit_leave_updates_the_selected_row(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    year = shamsi_year_of(date.today())
    person_id = add_personnel(
        "علی",
        "رضایی",
        8,
        remote_id="dev-1",
        cooperation_start=parse_shamsi_date(f"{year:04d}/01/01"),
        db_file=db_file,
    )
    leave_day = parse_shamsi_date(f"{year:04d}/06/15")
    shown = format_shamsi_date(leave_day)
    add_leave(person_id, leave_day, leave_day, compose_leave_minutes(1, 2, 15), db_file=db_file)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_leaves()
        root.update()
        view = window.leaves_view

        assert view.edit_button.caption.cget("text") == "ویرایش"
        view.edit_button.event_generate("<Button-1>")
        root.update()
        assert view.notice.cget("text") == "یک مرخصی را از لیست انتخاب کنید."
        assert view.form is None

        view.person_combo.current(0)
        view.load()
        root.update()
        view.tree.selection_set(view.tree.get_children()[0])
        view.edit_button.event_generate("<Button-1>")
        root.update()
        form = view.form
        assert form is not None
        assert form.title() == "ویرایش مرخصی"
        assert form.leave_id == 1
        assert form.save_button.caption.cget("text") == "ذخیره"
        assert form.person_combo.get() == "علی رضایی (dev-1)"
        assert form.start_date.get() == shown
        assert form.end_date.get() == shown
        assert form.leave_days.get() == "1"
        assert form.leave_hours.get() == "2"
        assert form.leave_minutes.get() == "15"
        assert form.preview.cget("text") == "معادل: 1 روز و 2 ساعت و 15 دقیقه"

        monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-08T09:15:00")
        form.leave_minutes.set("45")
        form.save()
        root.update()

        assert not form.winfo_exists()
        added = format_shamsi_datetime("2026-10-07T16:30:00")
        values = [view.tree.item(item, "values") for item in view.tree.get_children()]
        assert values == [(added, "1 روز و 2 ساعت و 45 دقیقه", shown, shown, "1")]
        assert view._amount_cells[1].labels[0].cget("text") == "1"
        assert view._amount_cells[1].labels[0].pack_info()["side"] == "right"
        cards = view.year_rows[year]
        assert _balance_text(cards.used_label) == "1 روز و 2 ساعت و 45 دقیقه"
        assert _balance_text(cards.remaining_label) == (
            "قبل از انتقال 28 روز و 4 ساعت و 35 دقیقه\n"
            "بعد از انتقال 19 روز و 4 ساعت و 35 دقیقه"
        )
        assert _balance_text(cards.transfer_label) == "9 روز و 0 ساعت و 0 دقیقه"
        stored = list_leaves(person_id, db_file=db_file)[0]
        assert stored["created_at"] == "2026-10-07T16:30:00"
        assert stored["updated_at"] == "2026-10-08T09:15:00"
        assert stored["minutes"] == compose_leave_minutes(1, 2, 45)
    finally:
        root.destroy()


def test_leave_year_filter_keeps_several_years_and_resets_with_the_person(tmp_path, monkeypatch):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    monkeypatch.setattr("atten.db.repository._timestamp", lambda: "2026-10-07T16:30:00")
    year = shamsi_year_of(date.today())
    previous = year - 1
    previous_day = parse_shamsi_date(f"{previous:04d}/06/15")
    current_day = parse_shamsi_date(f"{year:04d}/06/15")
    person_id = add_personnel(
        "علی",
        "رضایی",
        8,
        remote_id="dev-1",
        cooperation_start=parse_shamsi_date(f"{previous:04d}/01/01"),
        db_file=db_file,
    )
    add_personnel("مریم", "احمدی", 8, remote_id="dev-2", db_file=db_file)
    day = compose_leave_minutes(1, 0, 0)
    add_leave(person_id, previous_day, previous_day, day, db_file=db_file)
    add_leave(person_id, current_day, current_day, day, db_file=db_file)

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_leaves()
        root.update()
        view = window.leaves_view
        assert view.checked_years == {year}
        assert view.year_button.caption.cget("text") == f"سال {year}"

        view.person_combo.current(0)
        view.person_combo.event_generate("<<ComboboxSelected>>")
        root.update()
        assert view.checked_years == {year}
        current = view.year_rows[year]
        assert _balance_text(current.carry_label) == "9 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(current.earned_label) == "30 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(current.used_label) == "1 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(current.remaining_label) == (
            "قبل از انتقال 38 روز و 0 ساعت و 0 دقیقه\n"
            "بعد از انتقال 29 روز و 0 ساعت و 0 دقیقه"
        )
        assert _balance_text(current.transfer_label) == "9 روز و 0 ساعت و 0 دقیقه"

        view.year_button.event_generate("<Button-1>")
        root.update()
        popup = view.year_popup
        assert popup is not None
        assert list(popup.variables) == [year, previous]
        popup.checks[previous].invoke()
        root.update()
        assert view.checked_years == {year, previous}
        assert view.year_button.caption.cget("text") == f"سال {year}، {previous}"
        assert list(view.summary.pack_slaves()) == [
            view.year_rows[year].frame,
            view.year_rows[previous].frame,
        ]
        earlier = view.year_rows[previous]
        assert _balance_text(earlier.carry_label) == "0 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(earlier.earned_label) == "30 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(earlier.used_label) == "1 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(earlier.remaining_label) == (
            "قبل از انتقال 29 روز و 0 ساعت و 0 دقیقه\n"
            "بعد از انتقال 20 روز و 0 ساعت و 0 دقیقه"
        )
        assert _balance_text(earlier.transfer_label) == "9 روز و 0 ساعت و 0 دقیقه"
        assert view.count.cget("text") == "2 مورد"
        shown_previous = format_shamsi_date(previous_day)
        shown_current = format_shamsi_date(current_day)
        values = [view.tree.item(item, "values") for item in view.tree.get_children()]
        assert values[0][3] == shown_previous
        assert values[1][3] == shown_current

        popup.checks[year].invoke()
        root.update()
        assert view.checked_years == {previous}
        assert [view.tree.item(item, "values")[3] for item in view.tree.get_children()] == [
            shown_previous
        ]
        assert not view.empty.winfo_ismapped()
        popup.checks[year].invoke()
        root.update()

        view.tree.selection_set(view.tree.get_children()[0])
        view.edit_button.event_generate("<Button-1>")
        root.update()
        form = view.form
        assert form is not None
        form.save()
        root.update()
        assert view.checked_years == {year, previous}
        assert view.count.cget("text") == "2 مورد"

        view.person_combo.current(1)
        view.person_combo.event_generate("<<ComboboxSelected>>")
        root.update()
        assert view.checked_years == {year}
        assert list(view.year_popup.variables) == [year]
        missing = view.year_rows[year]
        assert missing.notice is not None
        assert missing.notice.cget("text") == "تاریخ شروع همکاری ثبت نشده است."
        assert list(missing.frame.pack_slaves())[:2] == [missing.year_label, missing.notice]
        zero = "0 روز و 0 ساعت و 0 دقیقه"
        assert _balance_text(missing.carry_label) == zero
        assert _balance_text(missing.earned_label) == zero
        assert _balance_text(missing.used_label) == zero
        assert _balance_text(missing.remaining_label) == (
            f"قبل از انتقال {zero}\n"
            f"بعد از انتقال {zero}"
        )
        assert _balance_text(missing.transfer_label) == zero
        assert view.empty.cget("text") == "برای این پرسنل مرخصی ثبت نشده است."

        view.person_combo.current(0)
        view.person_combo.event_generate("<<ComboboxSelected>>")
        root.update()
        assert view.checked_years == {year}
        assert view.count.cget("text") == "1 مورد"
        assert [view.tree.item(item, "values")[3] for item in view.tree.get_children()] == [
            shown_current
        ]

        view.year_popup.checks[previous].invoke()
        root.update()
        assert view.checked_years == {year, previous}
        assert view.year_popup is not None

        window.nav_items["clock_events"].event_generate("<Button-1>")
        root.update()
        assert view.year_popup is None
        window.nav_items["leaves"].event_generate("<Button-1>")
        root.update()
        assert view.checked_years == {year}
        assert view.year_button.caption.cget("text") == f"سال {year}"
        assert list(view.year_rows) == [year]
    finally:
        root.destroy()


def test_backup_page_is_last_and_restore_confirms_before_deleting(tmp_path, monkeypatch):
    from datetime import datetime

    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_personnel("علی", "رضایی", 8, remote_id="kept", db_file=db_file)
    folder = tmp_path / "copies"
    prompts: list[tuple[str, str]] = []
    answers = [False, True]

    def ask(title: str, message: str, **_kwargs: object) -> bool:
        prompts.append((title, message))
        return answers.pop(0)

    monkeypatch.setattr("atten.ui.backup_view.messagebox.askyesno", ask)
    monkeypatch.setattr(
        "atten.ui.backup_view.datetime",
        type("Frozen", (datetime,), {"now": staticmethod(lambda: datetime(2026, 10, 8, 21, 28, 5))}),
    )

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        root.update()
        buttons = window.toolbar.pack_slaves()
        assert buttons.index(window.nav_items["calculation"]) < buttons.index(window.nav_items["backup"])
        assert window.nav_items["backup"].caption.cget("text") == "پشتیبان"

        window.nav_items["backup"].event_generate("<Button-1>")
        root.update()
        view = window.backup_view
        assert view.winfo_ismapped()
        assert not window.calculation_view.winfo_ismapped()
        assert view.export_button.caption.cget("text") == "ایجاد نسخهٔ پشتیبان"
        assert view.restore_button.caption.cget("text") == "بازگردانی"

        view.create_backup()
        assert view.notice.cget("text") == "ابتدا مسیر ذخیره را انتخاب کنید."

        view.set_folder(folder)
        view.create_backup()
        saved = folder / "atten-backup-2026-10-08_21-28-05.db"
        assert saved.exists()
        assert "1405/07/16" in view.notice.cget("text")
        assert saved.name in view.notice.cget("text")

        add_personnel("مریم", "احمدی", 8, remote_id="gone", db_file=db_file)
        window.show_personnel()
        root.update()
        assert window.personnel_view.count.cget("text") == "2 نفر"

        window.show_backup()
        root.update()
        view.restore_backup()
        assert view.notice.cget("text") == "ابتدا فایل نسخهٔ پشتیبان را انتخاب کنید."
        assert prompts == []

        view.set_backup_file(saved)
        view.restore_backup()
        root.update()
        assert prompts[0][0] == "حذف اطلاعات"
        assert "همهٔ اطلاعات فعلی حذف خواهند شد" in prompts[0][1]
        assert window.personnel_view.count.cget("text") == "2 نفر"

        view.restore_backup()
        root.update()
        assert len(prompts) == 2
        assert view.notice.cget("text") == "نسخهٔ پشتیبان برگردانده شد. اطلاعات قبلی حذف شد."
        window.show_personnel()
        root.update()
        assert window.personnel_view.count.cget("text") == "1 نفر"
        values = [
            window.personnel_view.tree.item(item, "values")
            for item in window.personnel_view.tree.get_children()
        ]
        assert values[0][6] == "kept"
    finally:
        root.destroy()


def test_backup_health_check_shows_progress_and_a_damaged_file(tmp_path):
    db_file = tmp_path / "atten.db"
    migrate(db_file)
    add_personnel("علی", "رضایی", 8, remote_id="kept", db_file=db_file)
    folder = tmp_path / "copies"

    root = tk.Tk()
    root.attributes("-alpha", 0)
    try:
        window = MainWindow(root, db_file=db_file)
        window.show_backup()
        root.update()
        view = window.backup_view
        assert view.check_button.caption.cget("text") == "بررسی سلامت"

        view.check_selected()
        assert view.notice.cget("text") == "ابتدا فایل را برای بررسی انتخاب کنید."

        view.set_folder(folder)
        view.create_backup()
        saved = next(folder.glob("atten-backup-*.db"))
        view.set_check_file(saved)
        view.check_selected()
        root.update()

        assert float(view.progress["value"]) == float(view.progress["maximum"])
        assert view.progress["maximum"] > 1
        assert view.check_status.cget("text") == "بررسی تمام شد."
        assert view.notice.cget("text") == "فایل پشتیبان سالم است."
        assert "پرسنل: 1 رکورد" in view.check_result.cget("text")
        assert "ساختار فایل و فهرست‌ها: سالم" in view.check_result.cget("text")

        junk = tmp_path / "notes.db"
        junk.write_bytes(b"this is not a database" + b"\0" * 128)
        view.set_check_file(junk)
        view.check_selected()
        root.update()
        assert view.notice.cget("text") == "فایل پشتیبان سالم نیست."
        assert "پایگاه دادهٔ سالمی نیست" in view.check_result.cget("text")
    finally:
        root.destroy()
