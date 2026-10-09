"""Daily entry and exit sheet for one employee and a Shamsi date range."""

import sqlite3
import tkinter as tk
from collections.abc import Callable
from datetime import date
from pathlib import Path
from tkinter import messagebox, ttk

from atten.dates import format_shamsi_date, format_shamsi_weekday, parse_shamsi_date
from atten.db.repository import (
    clear_work_punches,
    list_daily_punches,
    list_leaves,
    list_personnel,
    list_work_balances,
    list_work_punches,
    save_work_balances,
    save_work_holiday,
    save_work_punch,
)
from atten.leave import format_leave_duration
from atten.sheet import SheetRow, build_sheet, format_balance, parse_clock
from atten.ui.fonts import UI_FONT
from atten.ui.leave_form import LeaveForm
from atten.ui.personnel_view import BACKGROUND, INK, LINE, MUTED, PANEL, _ActionButton
from atten.ui.shamsi_date import ShamsiDateEntry

DANGER = "#b42318"
INCOMPLETE = "#fde8e8"
LEAVE = "#e5f6ea"


class CalculationView(tk.Frame):
    def __init__(self, master: tk.Misc, db_file: Path) -> None:
        super().__init__(master, bg=BACKGROUND)
        self.db_file = db_file
        self._people: list[dict[str, object]] = []
        self._remote_id: str | None = None
        self._personnel_id: int | None = None
        self._daily_hours: float | None = None
        self._range: tuple[date, date] | None = None
        self._rows: dict[str, SheetRow] = {}
        self.actions: dict[str, _DayActions] = {}
        self.form: LeaveForm | None = None
        self.editor: tk.Entry | None = None
        self._edit_date: str | None = None
        self._edit_slot: int | None = None
        self.start_date = tk.StringVar()
        self.end_date = tk.StringVar()

        header = tk.Frame(self, bg=BACKGROUND)
        header.pack(fill="x", padx=20, pady=(18, 10))
        self.notice = tk.Label(header, text="", font=(UI_FONT, 10), bg=BACKGROUND, fg=DANGER)
        self.notice.pack(side="left")
        tk.Label(
            header,
            text="محاسبه",
            font=(UI_FONT, 14, "bold"),
            bg=BACKGROUND,
            fg=INK,
        ).pack(side="right")

        filters = tk.Frame(self, bg=BACKGROUND)
        filters.pack(fill="x", padx=20, pady=(0, 12))
        tk.Label(filters, text="کارمند", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(side="right")
        self.person_combo = ttk.Combobox(
            filters,
            state="readonly",
            justify="right",
            font=(UI_FONT, 10),
            width=28,
        )
        self.person_combo.pack(side="right", padx=(8, 6))
        self.person_combo.bind("<<ComboboxSelected>>", lambda _event: self._show_if_ready())
        tk.Label(filters, text="از تاریخ", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(
            side="right", padx=(16, 0)
        )
        self.start_field = ShamsiDateEntry(filters, self.start_date, on_change=self._show_if_ready)
        self.start_field.pack(side="right", padx=(8, 6))
        tk.Label(filters, text="تا تاریخ", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(
            side="right", padx=(16, 0)
        )
        self.end_field = ShamsiDateEntry(filters, self.end_date, on_change=self._show_if_ready)
        self.end_field.pack(side="right", padx=(8, 6))
        self.show_button = _ActionButton(
            filters,
            "نمایش",
            "\uE721",
            self.show_result,
            bg="#14508a",
            fg="#ffffff",
        )
        self.show_button.pack(side="left")
        self.start_field.entry.bind("<Return>", lambda _event: self.show_result())
        self.end_field.entry.bind("<Return>", lambda _event: self.show_result())

        self.empty = tk.Label(
            self,
            text="کارمند و بازه تاریخ را انتخاب کنید.",
            font=(UI_FONT, 11),
            bg=BACKGROUND,
            fg=MUTED,
        )
        self.empty.pack(expand=True)
        self.table = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.tree = ttk.Treeview(
            self.table,
            show="headings",
            style="Calculation.Treeview",
            selectmode="browse",
        )
        self.tree.tag_configure("odd", background="#f7f9fb")
        self.tree.tag_configure("even", background=PANEL)
        self.tree.tag_configure("incomplete", background=INCOMPLETE)
        self.tree.tag_configure("leave", background=LEAVE)
        self.tree.bind("<Button-1>", self._on_cell_click)
        self.tree.bind("<ButtonRelease-1>", lambda _event: self._place_actions(), add="+")
        self.tree.bind("<Configure>", lambda _event: self._place_actions(), add="+")
        self._yscroll = ttk.Scrollbar(self.table, orient="vertical", command=self.tree.yview)
        self._xscroll = ttk.Scrollbar(self.table, orient="horizontal", command=self.tree.xview)
        self.tree.configure(
            yscrollcommand=self._on_vertical_scroll,
            xscrollcommand=self._on_horizontal_scroll,
        )
        self._xscroll.pack(side="bottom", fill="x")
        self._yscroll.pack(side="right", fill="y")
        self.tree.pack(side="left", fill="both", expand=True)

        style = ttk.Style(self)
        style.configure("Calculation.Treeview", font=(UI_FONT, 10), rowheight=36)
        style.configure("Calculation.Treeview.Heading", font=(UI_FONT, 10, "bold"))

    def reload(self) -> None:
        selected = self._selected_person_id()
        try:
            people = list_personnel(self.db_file)
        except sqlite3.Error as exc:
            self._people = []
            self.person_combo["values"] = ()
            self.person_combo.set("")
            self._show_message(f"خواندن لیست پرسنل ممکن نشد: {exc}", error=True)
            return

        self._people = people
        self.person_combo["values"] = [_person_label(person) for person in people]
        if selected is None:
            return
        for index, person in enumerate(people):
            if int(person["id"]) == selected:
                self.person_combo.current(index)
                return
        self.person_combo.set("")
        self._remote_id = None
        self._personnel_id = None
        self._show_message("کارمند و بازه تاریخ را انتخاب کنید.")

    def _show_if_ready(self) -> None:
        if self._selected_person() is None:
            return
        if not self.start_date.get().strip() or not self.end_date.get().strip():
            return
        self.show_result()

    def show_result(self) -> None:
        if self.editor is not None:
            self.commit_edit()
            return
        try:
            person = self._selected_person()
            if person is None:
                raise ValueError("یک کارمند را انتخاب کنید.")
            start_text = self.start_date.get().strip()
            end_text = self.end_date.get().strip()
            if not start_text or not end_text:
                raise ValueError("تاریخ شروع و پایان را انتخاب کنید.")
            start = parse_shamsi_date(start_text)
            end = parse_shamsi_date(end_text)
            remote_id = person["remote_id"]
            if not remote_id:
                raise ValueError("برای این کارمند Remote ID ثبت نشده است.")
            personnel_id = int(person["id"])
            rows = self._load_rows(
                str(remote_id),
                float(person["daily_hours"]),
                start,
                end,
                personnel_id,
            )
        except ValueError as exc:
            self._show_message(str(exc), error=True)
            return
        except sqlite3.Error as exc:
            self._show_message(f"خواندن ورود و خروج ممکن نشد: {exc}", error=True)
            return

        self._remote_id = str(remote_id)
        self._personnel_id = personnel_id
        self._daily_hours = float(person["daily_hours"])
        self._range = (start, end)
        self.start_date.set(format_shamsi_date(start))
        self.end_date.set(format_shamsi_date(end))
        self.notice.configure(text="")
        self._render(rows)

    def toggle_holiday(self, stored_date: str) -> None:
        row = self._rows.get(stored_date)
        if (
            row is None
            or self._remote_id is None
            or self._personnel_id is None
            or self._daily_hours is None
            or self._range is None
        ):
            return
        try:
            save_work_holiday(self._remote_id, stored_date, not row.holiday, db_file=self.db_file)
            start, end = self._range
            rows = self._load_rows(self._remote_id, self._daily_hours, start, end, self._personnel_id)
        except sqlite3.Error as exc:
            self.notice.configure(text=f"ذخیره روز تعطیل ممکن نشد: {exc}", fg=DANGER)
            return
        self.notice.configure(text="")
        self._render(rows)

    def reset_day(self, stored_date: str) -> None:
        if self.editor is not None:
            self.commit_edit()
        row = self._rows.get(stored_date)
        if (
            row is None
            or self._remote_id is None
            or self._personnel_id is None
            or self._daily_hours is None
            or self._range is None
        ):
            return
        shown = format_shamsi_date(stored_date)
        confirmed = messagebox.askyesno(
            "بازنشانی ورود و خروج",
            f"ورود و خروج {shown} پاک شود و دوباره از جدول ورود و خروج خوانده شود؟",
            parent=self.winfo_toplevel(),
            default=messagebox.NO,
        )
        if not confirmed:
            return
        try:
            clear_work_punches(self._remote_id, stored_date, db_file=self.db_file)
            start, end = self._range
            rows = self._load_rows(self._remote_id, self._daily_hours, start, end, self._personnel_id)
        except sqlite3.Error as exc:
            self.notice.configure(text=f"بازنشانی ورود و خروج ممکن نشد: {exc}", fg=DANGER)
            return
        self.notice.configure(text="")
        self._render(rows)

    def open_shortfall_leave(self, stored_date: str) -> None:
        if self.editor is not None:
            self.commit_edit()
        if self.form is not None and self.form.winfo_exists():
            self.form.lift()
            self.form.focus_force()
            return
        row = self._rows.get(stored_date)
        if row is None or row.balance_minutes >= 0 or self._personnel_id is None:
            return
        self.form = LeaveForm(
            self,
            self.db_file,
            on_saved=self.show_result,
            personnel_id=self._personnel_id,
            initial={
                "start_date": stored_date,
                "end_date": stored_date,
                "minutes": abs(row.balance_minutes),
            },
        )

    def save_slot(self, stored_date: str, slot: int, text: str) -> None:
        if (
            self._remote_id is None
            or self._personnel_id is None
            or self._daily_hours is None
            or self._range is None
        ):
            self.notice.configure(text="یک کارمند را انتخاب کنید.", fg=DANGER)
            return
        try:
            parsed = parse_clock(text)
            save_work_punch(self._remote_id, stored_date, slot, parsed, db_file=self.db_file)
            start, end = self._range
            rows = self._load_rows(self._remote_id, self._daily_hours, start, end, self._personnel_id)
        except ValueError as exc:
            self.notice.configure(text=str(exc), fg=DANGER)
            return
        except sqlite3.Error as exc:
            self.notice.configure(text=f"ذخیره ساعت ممکن نشد: {exc}", fg=DANGER)
            return
        self.notice.configure(text="")
        self._render(rows)

    def begin_edit(self, stored_date: str, column: str) -> None:
        if not column.startswith("p"):
            return
        self._close_editor()
        self.tree.update_idletasks()
        bbox = self.tree.bbox(stored_date, column)
        if not bbox:
            return
        current = self.tree.set(stored_date, column)
        x, y, width, height = bbox
        self.editor = tk.Entry(self.tree, justify="right", font=(UI_FONT, 10), bg="#fffdf8")
        self.editor.insert(0, current)
        self.editor.select_range(0, "end")
        self.editor.place(x=x, y=y, width=width, height=height)
        self._edit_date = stored_date
        self._edit_slot = int(column[1:])
        self.editor.bind("<Return>", lambda _event: self.commit_edit())
        self.editor.bind("<Escape>", lambda _event: self._close_editor())
        self.editor.bind("<FocusOut>", self._edit_focus_out)
        editor = self.editor
        self.after_idle(lambda: editor.focus_set() if self.editor is editor else None)

    def commit_edit(self) -> None:
        if self.editor is None or self._edit_date is None or self._edit_slot is None:
            return
        text = self.editor.get()
        stored_date = self._edit_date
        slot = self._edit_slot
        self._close_editor()
        self.save_slot(stored_date, slot, text)

    def _load_rows(
        self,
        remote_id: str,
        daily_hours: float,
        start: date,
        end: date,
        personnel_id: int,
    ) -> tuple[SheetRow, ...]:
        device_rows = list_daily_punches(remote_id, start, end, db_file=self.db_file)
        overrides = list_work_punches(remote_id, start, end, db_file=self.db_file)
        holidays = {
            str(row["date"]): bool(row["holiday"])
            for row in list_work_balances(remote_id, start, end, db_file=self.db_file)
            if row["holiday"] is not None
        }
        leaves = list_leaves(personnel_id, start, end, db_file=self.db_file)
        rows = build_sheet(start, end, device_rows, overrides, daily_hours, holidays, leaves=leaves)
        save_work_balances(
            remote_id,
            [(row.date, row.balance_minutes, row.holiday) for row in rows],
            db_file=self.db_file,
        )
        return rows

    def _selected_person(self) -> dict[str, object] | None:
        index = self.person_combo.current()
        if index < 0 or index >= len(self._people):
            return None
        return self._people[index]

    def _selected_person_id(self) -> int | None:
        person = self._selected_person()
        if person is None:
            return None
        return int(person["id"])

    def _render(self, rows: tuple[SheetRow, ...] | list[SheetRow]) -> None:
        self._close_editor()
        self._clear_actions()
        self.tree.delete(*self.tree.get_children())
        self._rows = {row.date: row for row in rows}
        if not rows:
            self._show_message("کارمند و بازه تاریخ را انتخاب کنید.")
            return

        punch_count = max(len(row.slots) for row in rows)
        columns = _table_columns(punch_count)
        self.tree["columns"] = [name for name, _title in columns]
        for name, title in columns:
            anchor = "center" if name in ("holiday", "actions") else "e"
            self.tree.heading(name, text=title, anchor=anchor)
            if name == "date":
                width = 120
            elif name == "actions":
                width = 230
            elif name == "leave":
                width = 220
            elif name in ("balance", "holiday"):
                width = 120
            else:
                width = 100
            if name == "leave":
                minimum = 200
            elif name == "actions":
                minimum = 210
            else:
                minimum = 80
            self.tree.column(name, width=width, minwidth=minimum, anchor=anchor, stretch=name == "date")

        self.empty.pack_forget()
        self.table.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        for index, row in enumerate(rows):
            if row.on_leave:
                tag = "leave"
            elif row.incomplete:
                tag = "incomplete"
            else:
                tag = "odd" if index % 2 else "even"
            self.tree.insert(
                "",
                "end",
                iid=row.date,
                tags=(tag,),
                values=_row_values(row, punch_count),
            )
            self.actions[row.date] = _DayActions(
                self.tree,
                _row_background(tag),
                on_reset=lambda stored=row.date: self.reset_day(stored),
                on_leave=(
                    (lambda stored=row.date: self.open_shortfall_leave(stored))
                    if row.balance_minutes < 0
                    else None
                ),
            )
        self.after_idle(self._place_actions)

    def _show_message(self, text: str, *, error: bool = False) -> None:
        self._close_editor()
        self._clear_actions()
        self._rows = {}
        self.tree.delete(*self.tree.get_children())
        self.table.pack_forget()
        self.empty.configure(text=text, fg=DANGER if error else MUTED)
        self.empty.pack(expand=True)
        if error:
            self.notice.configure(text="")

    def _on_cell_click(self, event: tk.Event) -> str | None:
        cell = self._named_cell(event)
        if cell is None:
            return None
        row, name = cell
        if name == "holiday":
            if self.editor is not None:
                self.commit_edit()
            self.toggle_holiday(row)
            return "break"
        if not name.startswith("p"):
            return None
        if self.editor is not None:
            self.commit_edit()
        self.after_idle(lambda: self.begin_edit(row, name))
        return "break"

    def _named_cell(self, event: tk.Event) -> tuple[str, str] | None:
        if self.tree.identify_region(event.x, event.y) != "cell":
            return None
        row = self.tree.identify_row(event.y)
        column = self.tree.identify_column(event.x)
        if not row or not column:
            return None
        names = _column_names(self.tree)
        index = int(column.replace("#", "")) - 1
        if index < 0 or index >= len(names):
            return None
        return row, names[index]

    def _edit_focus_out(self, _event: tk.Event) -> None:
        editor = self.editor
        if editor is None:
            return
        self.after(80, lambda: self._commit_if_left(editor))

    def _commit_if_left(self, editor: tk.Entry) -> None:
        if self.editor is not editor:
            return
        try:
            if editor.focus_get() is editor:
                return
        except tk.TclError:
            return
        self.commit_edit()

    def _on_vertical_scroll(self, first: str, last: str) -> None:
        self._yscroll.set(first, last)
        self._place_actions()

    def _on_horizontal_scroll(self, first: str, last: str) -> None:
        self._xscroll.set(first, last)
        self._place_actions()

    def _place_actions(self) -> None:
        if not self.actions or not self.tree.winfo_exists():
            return
        names = _column_names(self.tree)
        if "actions" not in names:
            return
        for stored_date, frame in self.actions.items():
            if not frame.winfo_exists():
                continue
            bbox = self.tree.bbox(stored_date, "actions")
            if not bbox:
                frame.place_forget()
                continue
            x, y, width, height = bbox
            frame.place(x=x, y=y, width=width, height=height)

    def _clear_actions(self) -> None:
        for frame in self.actions.values():
            if frame.winfo_exists():
                frame.destroy()
        self.actions.clear()

    def _close_editor(self) -> None:
        editor = self.editor
        self.editor = None
        self._edit_date = None
        self._edit_slot = None
        if editor is not None:
            editor.destroy()


def _person_label(person: dict[str, object]) -> str:
    name = f"{person['first_name']} {person['last_name']}"
    remote_id = person["remote_id"]
    if remote_id:
        return f"{name} ({remote_id})"
    return name


def _column_names(tree: ttk.Treeview) -> tuple[str, ...]:
    columns = tree["columns"]
    if isinstance(columns, str):
        return tuple(columns.split())
    return tuple(columns)


def _table_columns(punch_count: int) -> tuple[tuple[str, str], ...]:
    columns = [("actions", "عملیات"), ("leave", "مرخصی"), ("balance", "کسری / اضافه")]
    for index in range(punch_count - 1, -1, -1):
        title = "ورود" if index % 2 == 0 else "خروج"
        columns.append((f"p{index}", title))
    columns.append(("date", "تاریخ"))
    columns.append(("weekday", "روز"))
    columns.append(("holiday", "روز تعطیل"))
    return tuple(columns)


def _row_values(row: SheetRow, punch_count: int) -> tuple[str, ...]:
    padded = list(row.slots) + [None] * (punch_count - len(row.slots))
    ordered = [_clock_text(padded[index]) for index in range(punch_count - 1, -1, -1)]
    ordered.append(format_shamsi_date(row.date))
    ordered.append(format_shamsi_weekday(row.date))
    ordered.append("☑" if row.holiday else "☐")
    return (
        "",
        _rtl(format_leave_duration(row.leave_minutes)),
        format_balance(row.balance_minutes),
        *ordered,
    )


def _row_background(tag: str) -> str:
    if tag == "leave":
        return LEAVE
    if tag == "incomplete":
        return INCOMPLETE
    if tag == "odd":
        return "#f7f9fb"
    return PANEL


class _DayActions(tk.Frame):
    def __init__(
        self,
        master: tk.Misc,
        row_bg: str,
        on_reset: Callable[[], None],
        on_leave: Callable[[], None] | None,
    ) -> None:
        super().__init__(master, bg=row_bg)
        self.reset_button = _CellButton(self, "بازنشانی", on_reset, filled=False, row_bg=row_bg)
        self.reset_button.pack(side="right", padx=(4, 6), pady=4)
        self.leave_button: _CellButton | None = None
        if on_leave is not None:
            self.leave_button = _CellButton(self, "افزودن مرخصی", on_leave, filled=True, row_bg=row_bg)
            self.leave_button.pack(side="right", padx=(6, 0), pady=4)


class _CellButton(tk.Frame):
    def __init__(
        self,
        master: tk.Misc,
        text: str,
        command: Callable[[], None],
        *,
        filled: bool,
        row_bg: str,
    ) -> None:
        bg = "#14508a" if filled else row_bg
        fg = "#ffffff" if filled else INK
        super().__init__(
            master,
            bg=bg,
            cursor="hand2",
            highlightthickness=0 if filled else 1,
            highlightbackground=LINE,
        )
        self._command = command
        self.caption = tk.Label(self, text=text, font=(UI_FONT, 9), bg=bg, fg=fg, padx=6, pady=1)
        self.caption.pack()
        self.bind("<Button-1>", self._activate)
        self.caption.bind("<Button-1>", self._activate)

    def _activate(self, _event: tk.Event) -> None:
        command = self._command
        self.winfo_toplevel().after_idle(command)


def _rtl(text: str) -> str:
    # A leading digit otherwise sticks to the end of the Persian phrase in the cell.
    if not text:
        return ""
    return "\u202b" + text + "\u202c"


def _clock_text(value: str | None) -> str:
    if not value:
        return ""
    return value
