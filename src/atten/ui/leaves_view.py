"""Leave list for one person, with a Shamsi-year filter and yearly balance."""

import re
import sqlite3
import tkinter as tk
from datetime import date, datetime
from pathlib import Path
from tkinter import ttk

from atten.dates import format_shamsi_date, format_shamsi_datetime
from atten.db.repository import list_leaves, list_personnel
from atten.leave import (
    LeaveYearSettlement,
    earned_leave_minutes,
    format_leave_amount,
    settle_leave_years,
    shamsi_year_of,
)
from atten.ui.fonts import UI_FONT
from atten.ui.personnel_view import BACKGROUND, INK, LINE, MUTED, PANEL, _ActionButton

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
        self._amount_cells: dict[int, tk.Frame] = {}
        self.form: tk.Toplevel | None = None
        self.year_popup: _YearPopup | None = None
        self.year_rows: dict[int, _YearSummary] = {}
        self.checked_years: set[int] = {shamsi_year_of(date.today())}
        self._years_person_id: int | None = None

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
        self.person_combo.bind("<<ComboboxSelected>>", lambda _event: self.load(reset_years=True))
        self.year_button = _ActionButton(
            filters,
            _year_button_text(self.checked_years),
            "\uE70D",
            self.toggle_years,
            bg=PANEL,
            fg=INK,
            border=True,
        )
        self.year_button.pack(side="right", padx=(16, 0))

        self.summary = tk.Frame(self, bg=BACKGROUND)

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
        self.tree.configure(yscrollcommand=self._sync_amount_scroll)
        self._amount_scroll = scroll
        self.tree.bind("<Configure>", lambda _event: self._place_amounts(), add="+")
        self.tree.bind("<ButtonRelease-1>", lambda _event: self._place_amounts(), add="+")
        self.tree.bind("<<TreeviewSelect>>", lambda _event: self._paint_amounts(), add="+")
        scroll.pack(side="right", fill="y")
        self.tree.pack(side="left", fill="both", expand=True)
        style = ttk.Style(self)
        style.configure("Leaves.Treeview", font=(UI_FONT, 10), rowheight=30)
        style.configure("Leaves.Treeview.Heading", font=(UI_FONT, 10, "bold"))
        self.bind("<Unmap>", lambda _event: self._close_years())
        self.empty.pack(expand=True)

    def reload(self, *, keep_years: bool = False) -> None:
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
        self.load(reset_years=not keep_years)

    def load(self, *, reset_years: bool = False) -> None:
        self.notice.configure(text="")
        person = self._selected_person()
        person_id = None if person is None else int(person["id"])
        choices = cooperation_year_choices(person)
        self._select_years(person_id, choices, reset=reset_years)
        self._update_year_button()
        self._sync_year_popup(choices)
        if person is None:
            self.count.configure(text="")
            self._rows = {}
            self._clear_cards()
            self._layout(False, False, "یک پرسنل را انتخاب کنید.")
            return
        try:
            rows = list_leaves(person_id, db_file=self.db_file)
        except sqlite3.Error as exc:
            self.count.configure(text="")
            self._rows = {}
            self._clear_cards()
            self._layout(False, False, f"خواندن مرخصی ممکن نشد: {exc}")
            return
        self._show_rows(person, rows)

    def toggle_years(self) -> None:
        if self.year_popup is not None and self.year_popup.winfo_exists():
            self._close_years()
            return
        self.year_popup = _YearPopup(
            self.year_button,
            cooperation_year_choices(self._selected_person()),
            self.checked_years,
            self._on_year_toggled,
        )
        self.year_popup.bind("<Destroy>", self._forget_year_popup, add="+")

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
            on_saved=lambda: self.reload(keep_years=True),
            personnel_id=personnel_id,
            leave=leave,
        )

    def _on_year_toggled(self, year: int) -> None:
        popup = self.year_popup
        if popup is None or not popup.winfo_exists():
            return
        if popup.variables[year].get():
            self.checked_years.add(year)
        else:
            self.checked_years.discard(year)
        self.load()

    def _close_years(self) -> None:
        if self.year_popup is not None and self.year_popup.winfo_exists():
            self.year_popup.destroy()
        self.year_popup = None

    def _forget_year_popup(self, _event: tk.Event) -> None:
        if self.year_popup is not None and not self.year_popup.winfo_exists():
            self.year_popup = None

    def _select_years(self, person_id: int | None, choices: list[int], *, reset: bool) -> None:
        current = shamsi_year_of(date.today())
        if reset or self._years_person_id != person_id:
            self.checked_years = {current}
        else:
            self.checked_years &= set(choices)
        self._years_person_id = person_id

    def _update_year_button(self) -> None:
        self.year_button.caption.configure(text=_year_button_text(self.checked_years))

    def _sync_year_popup(self, choices: list[int]) -> None:
        popup = self.year_popup
        if popup is None or not popup.winfo_exists():
            return
        if list(popup.years) != list(choices):
            popup.rebuild(choices, self.checked_years)
            return
        for year, variable in popup.variables.items():
            selected = year in self.checked_years
            if bool(variable.get()) != selected:
                variable.set(selected)

    def _show_rows(self, person: dict[str, object], rows: list[dict[str, object]]) -> None:
        used_by_year: dict[int, int] = {}
        for row in rows:
            year = shamsi_year_of(str(row["start_date"]))
            used_by_year[year] = used_by_year.get(year, 0) + int(row["minutes"])
        settled = _settlements(person, used_by_year)
        self._clear_cards()
        for year in sorted(self.checked_years, reverse=True):
            carry, earned, used_text, remaining_text, transfer, missing_start = _card_texts(
                person, year, settled, used_by_year
            )
            summary = _YearSummary(
                self.summary,
                year,
                carry,
                earned,
                used_text,
                remaining_text,
                transfer,
                missing_start=missing_start,
            )
            summary.frame.pack(fill="x", pady=(0, 8))
            self.year_rows[year] = summary

        visible = [
            row
            for row in rows
            if shamsi_year_of(str(row["start_date"])) in self.checked_years
        ]
        self._rows = {}
        self._clear_amounts()
        self.tree.delete(*self.tree.get_children())
        for index, row in enumerate(visible):
            leave_id = int(row["id"])
            self._rows[leave_id] = row
            tag = "odd" if index % 2 else "even"
            amount = format_leave_amount(int(row["minutes"]))
            self._amount_cells[leave_id] = _AmountCell(
                self.tree, amount, tag, leave_id, self.open_edit
            )
            self.tree.insert(
                "",
                "end",
                iid=str(leave_id),
                tags=(tag,),
                values=(
                    _format_stamp(row["created_at"]),
                    amount,
                    format_shamsi_date(str(row["end_date"])),
                    format_shamsi_date(str(row["start_date"])),
                    row["id"],
                ),
            )
        if visible:
            self.count.configure(text=f"{len(visible)} مورد")
            self._layout(bool(self.year_rows), True, None)
            self.after_idle(self._place_amounts)
            return
        self.count.configure(text="")
        if not self.checked_years:
            empty_text = "سال را انتخاب کنید."
        elif rows:
            empty_text = "در سال‌های انتخاب‌شده مرخصی ثبت نشده است."
        else:
            empty_text = "برای این پرسنل مرخصی ثبت نشده است."
        self._layout(bool(self.year_rows), False, empty_text)

    def _clear_cards(self) -> None:
        for child in self.summary.winfo_children():
            child.destroy()
        self.year_rows = {}

    def _clear_amounts(self) -> None:
        for cell in self._amount_cells.values():
            cell.destroy()
        self._amount_cells = {}

    def _sync_amount_scroll(self, first: str, last: str) -> None:
        self._amount_scroll.set(first, last)
        self._place_amounts()

    def _place_amounts(self) -> None:
        if not self.tree.winfo_exists():
            return
        selected = set(self.tree.selection())
        for leave_id, cell in self._amount_cells.items():
            bbox = self.tree.bbox(str(leave_id), "minutes")
            if not bbox:
                cell.place_forget()
                continue
            x, y, width, height = bbox
            cell.place(x=x, y=y, width=width, height=height)
            cell.paint(str(leave_id) in selected)

    def _paint_amounts(self) -> None:
        if not self.tree.winfo_exists():
            return
        selected = set(self.tree.selection())
        for leave_id, cell in self._amount_cells.items():
            cell.paint(str(leave_id) in selected)

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


class _YearPopup(tk.Toplevel):
    def __init__(
        self,
        master: tk.Misc,
        years: list[int],
        checked: set[int],
        on_toggle,
    ) -> None:
        super().__init__(master)
        self.on_toggle = on_toggle
        self.years: list[int] = []
        self.variables: dict[int, tk.BooleanVar] = {}
        self.checks: dict[int, tk.Checkbutton] = {}
        self.title("سال")
        self.resizable(False, False)
        self.transient(master.winfo_toplevel())
        self.configure(bg=PANEL)
        self.body = tk.Frame(self, bg=PANEL)
        self.body.pack(fill="both", expand=True, padx=8, pady=8)
        self.rebuild(years, checked)
        self.bind("<Escape>", lambda _event: self.destroy())
        self.update_idletasks()
        x = master.winfo_rootx()
        y = master.winfo_rooty() + master.winfo_height()
        self.geometry(f"+{x}+{y}")

    def rebuild(self, years: list[int], checked: set[int]) -> None:
        for child in self.body.winfo_children():
            child.destroy()
        self.years = list(years)
        self.variables = {}
        self.checks = {}
        for year in years:
            variable = tk.BooleanVar(value=year in checked)
            check = tk.Checkbutton(
                self.body,
                text=str(year),
                variable=variable,
                command=lambda picked=year: self.on_toggle(picked),
                anchor="e",
                justify="right",
                font=(UI_FONT, 10),
                bg=PANEL,
                fg=INK,
                activebackground=PANEL,
                activeforeground=INK,
                selectcolor=PANEL,
            )
            check.pack(fill="x", anchor="e")
            self.variables[year] = variable
            self.checks[year] = check


class _YearSummary:
    def __init__(
        self,
        master: tk.Misc,
        year: int,
        carry: str,
        earned: str,
        used: str,
        remaining: str,
        transfer: str,
        *,
        missing_start: bool,
    ) -> None:
        self.year = year
        self.frame = tk.Frame(master, bg=BACKGROUND)
        self.year_label = tk.Label(
            self.frame,
            text=f"سال {year}",
            font=(UI_FONT, 10, "bold"),
            bg=BACKGROUND,
            fg=INK,
        )
        self.year_label.pack(anchor="e", pady=(0, 0 if missing_start else 4))
        self.notice: tk.Label | None = None
        if missing_start:
            self.notice = tk.Label(
                self.frame,
                text="تاریخ شروع همکاری ثبت نشده است.",
                font=(UI_FONT, 10),
                bg=BACKGROUND,
                fg=INK,
            )
            self.notice.pack(anchor="e", pady=(2, 4))
        self.line = tk.Frame(self.frame, bg=BACKGROUND)
        self.line.pack(fill="x")
        self.carry_card, self.carry_label = _summary_card(self.line, "ذخیره (از سال قبل)", carry)
        self.earned_card, self.earned_label = _summary_card(self.line, "استحقاق", earned)
        self.used_card, self.used_label = _summary_card(self.line, "ثبت شده", used)
        self.remaining_card, self.remaining_label = _summary_card(self.line, "مانده", remaining)
        self.transfer_card, self.transfer_label = _summary_card(
            self.line, "انتقال (به سال بعد)", transfer
        )


def cooperation_year_choices(
    person: dict[str, object] | None,
    today: date | None = None,
) -> list[int]:
    """Shamsi years from cooperation start through its end.

    An empty end stops at the current Shamsi year. The current year is always
    included. Newer years come first.
    """
    current = shamsi_year_of(today or date.today())
    years = {current}
    if person is None:
        return [current]
    started = _stored_day(person.get("cooperation_start"))
    if started is not None:
        start_year = shamsi_year_of(started)
        ended = _stored_day(person.get("cooperation_end"))
        end_year = current if ended is None else shamsi_year_of(ended)
        years.update(range(min(start_year, end_year), max(start_year, end_year) + 1))
    return sorted(years, reverse=True)


def _settlements(
    person: dict[str, object],
    used_by_year: dict[int, int],
) -> dict[int, LeaveYearSettlement]:
    if _stored_day(person.get("cooperation_start")) is None:
        return {}
    end = person.get("cooperation_end")
    entries: list[tuple[int, int, int]] = []
    for year in sorted(cooperation_year_choices(person)):
        earned = earned_leave_minutes(year, person.get("cooperation_start"), end)
        if earned <= 0:
            continue
        entries.append((year, earned, int(used_by_year.get(year, 0))))
    ended = _stored_day(end)
    closing_year = None if ended is None else shamsi_year_of(ended)
    return {row.year: row for row in settle_leave_years(entries, closing_year)}


def _card_texts(
    person: dict[str, object],
    year: int,
    settled: dict[int, LeaveYearSettlement],
    used_by_year: dict[int, int],
) -> tuple[str, str, str, str, str, bool]:
    """Carry, earned, used, remaining, transfer, and whether the start date is missing."""
    used = int(used_by_year.get(year, 0))
    row = settled.get(year)
    missing_start = _stored_day(person.get("cooperation_start")) is None
    if missing_start:
        carry = earned = used_amount = before = after = transfer = 0
    elif row is None:
        carry = earned = transfer = 0
        used_amount = used
        before = after = -used
    else:
        carry = row.carry_in
        earned = row.earned
        used_amount = row.used
        before = row.remaining
        transfer = row.carry_out
        after = before - transfer
    remaining_text = (
        f"قبل از انتقال {_format_minutes(before)}\n"
        f"بعد از انتقال {_format_minutes(after)}"
    )
    return (
        format_leave_amount(carry),
        format_leave_amount(earned),
        format_leave_amount(used_amount),
        remaining_text,
        format_leave_amount(transfer),
        missing_start,
    )


def _summary_card(master: tk.Misc, title: str, body: str) -> tuple[tk.Frame, tk.Frame]:
    card = tk.Frame(master, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
    card.pack(side="right", fill="both", expand=True, padx=(6, 0))
    tk.Label(
        card,
        text=title,
        font=(UI_FONT, 9),
        bg=PANEL,
        fg=MUTED,
        anchor="e",
    ).pack(anchor="e", fill="x", padx=12, pady=(8, 0))
    box = tk.Frame(card, bg=PANEL)
    box.pack(anchor="e", fill="x", padx=12, pady=(2, 10))
    for line in body.split("\n"):
        row = tk.Frame(box, bg=PANEL)
        row.pack(anchor="e")
        for run in _display_runs(line):
            tk.Label(
                row,
                text=run,
                font=(UI_FONT, 10),
                bg=PANEL,
                fg=INK,
                bd=0,
                padx=0,
                pady=0,
                highlightthickness=0,
            ).pack(side="right")
    return card, box


def _display_runs(line: str) -> list[str]:
    # A Tk label pins a leading number to the left of the Persian phrase.
    # Drawing each run alone and packing from the right keeps the day number first.
    # A space on the edge of a Persian run is drawn on the far side of that run,
    # so those spaces are their own labels and stay between the neighbors.
    runs: list[str] = []
    for part in re.findall(r"-?\d+|[^\d-]+|-", line):
        if not part:
            continue
        if part.strip(" ") == "" or re.fullmatch(r"-?\d+", part):
            runs.append(part)
            continue
        leading = len(part) - len(part.lstrip(" "))
        trailing = len(part) - len(part.rstrip(" "))
        core = part[leading : len(part) - trailing] if trailing else part[leading:]
        if leading:
            runs.append(part[:leading])
        if core:
            runs.append(core)
        if trailing:
            runs.append(part[len(part) - trailing :])
    return runs


class _AmountCell(tk.Frame):
    """Amount text drawn in runs so the day number stays on the right of the cell."""

    def __init__(
        self,
        master: tk.Misc,
        text: str,
        tag: str,
        leave_id: int,
        on_open,
    ) -> None:
        super().__init__(master, bd=0, highlightthickness=0)
        self.tag = tag
        self.leave_id = leave_id
        self._on_open = on_open
        self.row = tk.Frame(self, bd=0, highlightthickness=0)
        self.row.place(relx=1, x=-6, rely=0.5, anchor="e")
        self.labels: list[tk.Label] = []
        for run in _display_runs(text):
            label = tk.Label(
                self.row,
                text=run,
                font=(UI_FONT, 10),
                bd=0,
                padx=0,
                pady=0,
                highlightthickness=0,
            )
            label.pack(side="right")
            label.bind("<Button-1>", self._select)
            label.bind("<Double-1>", self._open)
            self.labels.append(label)
        self.bind("<Button-1>", self._select)
        self.bind("<Double-1>", self._open)
        self.row.bind("<Button-1>", self._select)
        self.row.bind("<Double-1>", self._open)
        self.paint(False)

    def paint(self, selected: bool) -> None:
        if selected:
            style = ttk.Style(self)
            bg = style.lookup("Leaves.Treeview", "background", ("selected",)) or "#0078d7"
            fg = style.lookup("Leaves.Treeview", "foreground", ("selected",)) or "#ffffff"
        else:
            bg = "#f7f9fb" if self.tag == "odd" else PANEL
            fg = INK
        self.configure(bg=bg)
        self.row.configure(bg=bg)
        for label in self.labels:
            label.configure(bg=bg, fg=fg)

    def _select(self, _event: tk.Event) -> None:
        tree = self.master
        if isinstance(tree, ttk.Treeview):
            tree.selection_set(str(self.leave_id))
            tree.focus(str(self.leave_id))

    def _open(self, _event: tk.Event) -> None:
        self._select(_event)
        self._on_open()


def _year_button_text(years: set[int]) -> str:
    if not years:
        return "سال"
    shown = "، ".join(str(year) for year in sorted(years, reverse=True))
    return f"سال {shown}"


def _format_minutes(minutes: int) -> str:
    if minutes < 0:
        return f"-{format_leave_amount(-minutes)}"
    return format_leave_amount(minutes)


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


def _stored_day(value: object) -> date | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    text = str(value).strip()
    if not text:
        return None
    return date.fromisoformat(text[:10])
