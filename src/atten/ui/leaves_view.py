"""Leave list for one person, with the remaining yearly allowance."""

import sqlite3
import tkinter as tk
from datetime import date
from pathlib import Path
from tkinter import ttk

from atten.dates import format_shamsi_date, format_shamsi_datetime, parse_shamsi_date
from atten.db.repository import list_leaves, list_personnel, sum_leave_minutes
from atten.leave import YEARLY_LEAVE_MINUTES, format_leave_amount, shamsi_year_of, shamsi_year_span
from atten.ui.fonts import UI_FONT
from atten.ui.personnel_view import BACKGROUND, INK, LINE, MUTED, PANEL, _ActionButton
from atten.ui.shamsi_date import ShamsiDateEntry

COLUMNS = ("created_at", "minutes", "end_date", "start_date", "id")
HEADINGS = {
    "id": "شناسه",
    "start_date": "تاریخ شروع",
    "end_date": "تاریخ پایان",
    "minutes": "میزان",
    "created_at": "تاریخ افزودن",
}
WIDTHS = {
    "id": 80,
    "start_date": 130,
    "end_date": 130,
    "minutes": 220,
    "created_at": 160,
}


class LeavesView(tk.Frame):
    def __init__(self, master: tk.Misc, db_file: Path) -> None:
        super().__init__(master, bg=BACKGROUND)
        self.db_file = db_file
        self._people: list[dict[str, object]] = []
        self._rows: dict[int, dict[str, object]] = {}
        self.form: tk.Toplevel | None = None
        self.start_date = tk.StringVar()
        self.end_date = tk.StringVar()

        header = tk.Frame(self, bg=BACKGROUND)
        header.pack(fill="x", padx=20, pady=(18, 10))
        self.add_button = _ActionButton(
            header,
            "ثبت مرخصی",
            "\uE710",
            self.open_form,
            bg="#14508a",
            fg="#ffffff",
        )
        self.add_button.pack(side="left")
        self.edit_button = _ActionButton(
            header,
            "ویرایش",
            "\uE70F",
            self.open_edit,
            bg=PANEL,
            fg="#14508a",
            border=True,
        )
        self.edit_button.pack(side="left", padx=(8, 0))
        self.notice = tk.Label(header, text="", font=(UI_FONT, 10), bg=BACKGROUND, fg="#b42318")
        self.notice.pack(side="left", padx=(12, 0))
        tk.Label(header, text="مرخصی‌ها", font=(UI_FONT, 14, "bold"), bg=BACKGROUND, fg=INK).pack(
            side="right"
        )
        self.count = tk.Label(header, text="", font=(UI_FONT, 10), bg=BACKGROUND, fg=MUTED)
        self.count.pack(side="right", padx=(0, 12))

        filters = tk.Frame(self, bg=BACKGROUND)
        filters.pack(fill="x", padx=20, pady=(0, 12))
        tk.Label(filters, text="پرسنل", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(side="right")
        self.person_combo = ttk.Combobox(
            filters,
            state="readonly",
            justify="right",
            font=(UI_FONT, 10),
            width=28,
        )
        self.person_combo.pack(side="right", padx=(8, 6))
        self.person_combo.bind("<<ComboboxSelected>>", lambda _event: self.load())
        tk.Label(filters, text="از تاریخ", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(
            side="right", padx=(16, 0)
        )
        self.start_field = ShamsiDateEntry(filters, self.start_date)
        self.start_field.pack(side="right", padx=(8, 6))
        tk.Label(filters, text="تا تاریخ", font=(UI_FONT, 10), bg=BACKGROUND, fg=INK).pack(
            side="right", padx=(16, 0)
        )
        self.end_field = ShamsiDateEntry(filters, self.end_date)
        self.end_field.pack(side="right", padx=(8, 6))
        self.show_button = _ActionButton(
            filters,
            "نمایش",
            "\uE721",
            self.load,
            bg="#14508a",
            fg="#ffffff",
        )
        self.show_button.pack(side="left")

        self.summary = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.allowance_label = tk.Label(self.summary, text="", font=(UI_FONT, 10), bg=PANEL, fg=INK)
        self.used_label = tk.Label(self.summary, text="", font=(UI_FONT, 10), bg=PANEL, fg=INK)
        self.remaining_label = tk.Label(self.summary, text="", font=(UI_FONT, 10), bg=PANEL, fg=INK)
        self.allowance_label.pack(anchor="e", padx=16, pady=(10, 2))
        self.used_label.pack(anchor="e", padx=16, pady=2)
        self.remaining_label.pack(anchor="e", padx=16, pady=(2, 10))

        self.empty = tk.Label(
            self,
            text="یک پرسنل را انتخاب کنید.",
            font=(UI_FONT, 11),
            bg=BACKGROUND,
            fg=MUTED,
        )
        self.table = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.tree = ttk.Treeview(
            self.table,
            columns=COLUMNS,
            show="headings",
            style="Leaves.Treeview",
            selectmode="browse",
        )
        self.tree.bind("<Double-1>", lambda _event: self.open_edit())
        for name in COLUMNS:
            self.tree.heading(name, text=HEADINGS[name], anchor="e")
            self.tree.column(name, width=WIDTHS[name], minwidth=70, anchor="e", stretch=True)
        self.tree.tag_configure("odd", background="#f7f9fb")
        self.tree.tag_configure("even", background=PANEL)
        scroll = ttk.Scrollbar(self.table, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        scroll.pack(side="right", fill="y")
        self.tree.pack(side="left", fill="both", expand=True)
        style = ttk.Style(self)
        style.configure("Leaves.Treeview", font=(UI_FONT, 10), rowheight=30)
        style.configure("Leaves.Treeview.Heading", font=(UI_FONT, 10, "bold"))
        self.empty.pack(expand=True)

    def reload(self) -> None:
        previous = self._selected_person_id()
        try:
            self._people = list_personnel(self.db_file)
        except sqlite3.Error as exc:
            self._people = []
            self.notice.configure(text=f"خواندن پرسنل ممکن نشد: {exc}")
        self.person_combo.configure(values=[_person_label(person) for person in self._people])
        self.person_combo.set("")
        if previous is not None:
            for index, person in enumerate(self._people):
                if int(person["id"]) == previous:
                    self.person_combo.current(index)
                    break
        self.load()

    def load(self) -> None:
        self.notice.configure(text="")
        person = self._selected_person()
        if person is None:
            self.count.configure(text="")
            self._rows = {}
            self._layout(False, False, "یک پرسنل را انتخاب کنید.")
            return
        try:
            start, end = self._range()
        except ValueError as exc:
            self.notice.configure(text=str(exc))
            return
        person_id = int(person["id"])
        try:
            year = _summary_year(start, end)
            span_start, span_end = shamsi_year_span(year)
            used = sum_leave_minutes(person_id, span_start, span_end, db_file=self.db_file)
            rows = list_leaves(person_id, start, end, db_file=self.db_file)
        except (sqlite3.Error, ValueError) as exc:
            self.count.configure(text="")
            self._layout(False, False, f"خواندن مرخصی ممکن نشد: {exc}")
            return
        remaining = max(YEARLY_LEAVE_MINUTES - used, 0)
        self.allowance_label.configure(text=f"مجاز سال {year}: {format_leave_amount(YEARLY_LEAVE_MINUTES)}")
        self.used_label.configure(text=f"ثبت‌شده: {format_leave_amount(used)}")
        self.remaining_label.configure(text=f"باقی‌مانده: {format_leave_amount(remaining)}")
        self._rows = {}
        self.tree.delete(*self.tree.get_children())
        for index, row in enumerate(rows):
            leave_id = int(row["id"])
            self._rows[leave_id] = row
            self.tree.insert(
                "",
                "end",
                iid=str(leave_id),
                tags=("odd" if index % 2 else "even",),
                values=(
                    _format_stamp(row["created_at"]),
                    format_leave_amount(int(row["minutes"])),
                    format_shamsi_date(str(row["end_date"])),
                    format_shamsi_date(str(row["start_date"])),
                    row["id"],
                ),
            )
        if rows:
            self.count.configure(text=f"{len(rows)} مورد")
            self._layout(True, True, None)
            return
        self.count.configure(text="")
        filtered = bool(self.start_date.get().strip() or self.end_date.get().strip())
        empty_text = (
            "در این بازه مرخصی ثبت نشده است." if filtered else "برای این پرسنل مرخصی ثبت نشده است."
        )
        self._layout(True, False, empty_text)

    def open_form(self) -> None:
        self.notice.configure(text="")
        self._open_form(self._selected_person_id(), None)

    def open_edit(self) -> None:
        selected = self.tree.selection()
        if not selected:
            self.notice.configure(text="یک مرخصی را از لیست انتخاب کنید.")
            return
        leave = self._rows.get(int(selected[0]))
        if leave is None:
            self.notice.configure(text="یک مرخصی را از لیست انتخاب کنید.")
            return
        self.notice.configure(text="")
        self._open_form(int(leave["personnel_id"]), leave)

    def _open_form(self, personnel_id: int | None, leave: dict[str, object] | None) -> None:
        from atten.ui.leave_form import LeaveForm

        if self.form is not None and self.form.winfo_exists():
            self.form.destroy()
        self.form = LeaveForm(
            self,
            self.db_file,
            on_saved=self.reload,
            personnel_id=personnel_id,
            leave=leave,
        )

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

    def _range(self) -> tuple[date | None, date | None]:
        start = _optional_shamsi(self.start_date.get())
        end = _optional_shamsi(self.end_date.get())
        if start is not None and end is not None and start > end:
            raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
        return start, end

    def _layout(self, show_summary: bool, show_table: bool, empty_text: str | None) -> None:
        self.summary.pack_forget()
        self.table.pack_forget()
        self.empty.pack_forget()
        if not show_table:
            self.tree.delete(*self.tree.get_children())
        if show_summary:
            self.summary.pack(fill="x", padx=20, pady=(0, 12))
        if show_table:
            self.table.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        elif empty_text is not None:
            self.empty.configure(text=empty_text)
            self.empty.pack(expand=True)


def _summary_year(start: date | None, end: date | None) -> int:
    if start is not None:
        return shamsi_year_of(start)
    if end is not None:
        return shamsi_year_of(end)
    return shamsi_year_of(date.today())


def _person_label(person: dict[str, object]) -> str:
    name = f"{person['first_name']} {person['last_name']}"
    remote_id = person["remote_id"]
    if remote_id:
        return f"{name} ({remote_id})"
    return name


def _format_stamp(value: object) -> str:
    if not value:
        return ""
    return format_shamsi_datetime(str(value))


def _optional_shamsi(text: str) -> date | None:
    if not text.strip():
        return None
    return parse_shamsi_date(text)
