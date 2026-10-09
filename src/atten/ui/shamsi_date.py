"""Shamsi date field. The calendar asks dates.py for month days and weekdays."""

import tkinter as tk
from collections.abc import Callable
from datetime import date

from atten.dates import (
    SHAMSI_MONTH_NAMES,
    format_shamsi_date,
    parse_shamsi_date,
    shamsi_month_dates,
    shamsi_week_index,
    shamsi_ymd,
)
from atten.ui.fonts import UI_FONT

PANEL = "#ffffff"
INK = "#1f2933"
MUTED = "#64748b"
LINE = "#d8dee6"
ACCENT = "#14508a"
WEEKDAYS = ("ش", "ی", "د", "س", "چ", "پ", "ج")


class ShamsiDateEntry(tk.Frame):
    def __init__(
        self,
        master: tk.Misc,
        variable: tk.StringVar | None = None,
        on_change: Callable[[], None] | None = None,
    ) -> None:
        super().__init__(master, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.variable = variable if variable is not None else tk.StringVar()
        self._on_change = on_change
        self.popup: ShamsiCalendar | None = None
        self.entry = tk.Entry(
            self,
            textvariable=self.variable,
            width=12,
            font=(UI_FONT, 10),
            justify="right",
            relief="flat",
            bg=PANEL,
            fg=INK,
            highlightthickness=0,
        )
        self.entry.pack(side="right", ipady=4, padx=(0, 6))
        self.calendar_button = tk.Label(
            self,
            text="\uE787",
            font=("Segoe Fluent Icons", 12),
            bg=PANEL,
            fg=ACCENT,
            cursor="hand2",
        )
        self.calendar_button.pack(side="left", padx=(6, 4))
        self.calendar_button.bind("<Button-1>", lambda _event: self.open_calendar())

    def open_calendar(self) -> None:
        if self.popup is not None and self.popup.winfo_exists():
            self.popup.destroy()
            self.popup = None
            return
        self.popup = ShamsiCalendar(self, self._shown_date(), self._picked)
        self.popup.bind("<Destroy>", self._forget_popup, add="+")

    def _shown_date(self) -> date:
        text = self.variable.get().strip()
        if text:
            try:
                return parse_shamsi_date(text)
            except ValueError:
                pass
        return date.today()

    def _picked(self, text: str) -> None:
        self.variable.set(text)
        if self._on_change is not None:
            self._on_change()

    def _forget_popup(self, _event: tk.Event) -> None:
        if self.popup is not None and not self.popup.winfo_exists():
            self.popup = None


class ShamsiCalendar(tk.Toplevel):
    def __init__(self, master: tk.Misc, selected: date, on_pick) -> None:
        super().__init__(master)
        self.on_pick = on_pick
        self.selected = shamsi_ymd(selected)
        self.year, self.month, _day = self.selected
        self.days: dict[int, tk.Button] = {}
        self.title("انتخاب تاریخ")
        self.resizable(False, False)
        self.transient(master.winfo_toplevel())
        self.configure(bg=PANEL)

        header = tk.Frame(self, bg=PANEL)
        header.pack(fill="x", padx=10, pady=(10, 4))
        self.previous = tk.Button(
            header,
            text="ماه قبل",
            command=lambda: self._shift(-1),
            relief="flat",
            bg=PANEL,
            fg=INK,
            font=(UI_FONT, 9),
        )
        self.previous.pack(side="right")
        self.next = tk.Button(
            header,
            text="ماه بعد",
            command=lambda: self._shift(1),
            relief="flat",
            bg=PANEL,
            fg=INK,
            font=(UI_FONT, 9),
        )
        self.next.pack(side="left")
        self.month_label = tk.Label(header, text="", font=(UI_FONT, 11, "bold"), bg=PANEL, fg=INK)
        self.month_label.pack(side="right", expand=True)

        self.grid_frame = tk.Frame(self, bg=PANEL)
        self.grid_frame.pack(padx=10, pady=(0, 10))
        self._render()
        self.bind("<Escape>", lambda _event: self.destroy())
        self.update_idletasks()
        x = master.winfo_rootx()
        y = master.winfo_rooty() + master.winfo_height()
        self.geometry(f"+{x}+{y}")

    def _shift(self, delta: int) -> None:
        index = self.year * 12 + (self.month - 1) + delta
        if index < 12:
            return
        self.year, month_index = divmod(index, 12)
        self.month = month_index + 1
        self._render()

    def _render(self) -> None:
        for child in self.grid_frame.winfo_children():
            child.destroy()
        self.days = {}
        self.month_label.configure(text=f"{SHAMSI_MONTH_NAMES[self.month - 1]} {self.year}")
        for index, name in enumerate(WEEKDAYS):
            tk.Label(
                self.grid_frame,
                text=name,
                font=(UI_FONT, 9),
                bg=PANEL,
                fg=MUTED,
                width=3,
            ).grid(row=0, column=6 - index, padx=1, pady=1)

        days = shamsi_month_dates(self.year, self.month)
        offset = shamsi_week_index(days[0])
        for index, current in enumerate(days):
            _year, _month, day = shamsi_ymd(current)
            chosen = (_year, _month, day) == self.selected
            button = tk.Button(
                self.grid_frame,
                text=str(day),
                width=3,
                relief="flat",
                bg=ACCENT if chosen else PANEL,
                fg="#ffffff" if chosen else INK,
                font=(UI_FONT, 9),
                command=lambda picked=current: self._choose(picked),
            )
            column = 6 - ((offset + index) % 7)
            row = 1 + (offset + index) // 7
            button.grid(row=row, column=column, padx=1, pady=1)
            self.days[day] = button

    def _choose(self, picked: date) -> None:
        self.on_pick(format_shamsi_date(picked))
        self.destroy()
