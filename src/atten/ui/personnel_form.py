"""Dialog for adding or editing one personnel row."""

import tkinter as tk
from collections.abc import Callable
from datetime import date
from pathlib import Path

from atten.dates import format_shamsi_date, parse_shamsi_date
from atten.db.repository import add_personnel, update_personnel
from atten.persian import english_digits
from atten.ui.fonts import UI_FONT
from atten.ui.shamsi_date import ShamsiDateEntry

BACKGROUND = "#eef1f4"
PANEL = "#ffffff"
INK = "#1f2933"
MUTED = "#64748b"
LINE = "#d8dee6"

ACCENT = "#14508a"
DANGER = "#b42318"


class PersonnelForm(tk.Toplevel):
    def __init__(
        self,
        master: tk.Misc,
        db_file: Path,
        on_saved: Callable[[], None],
        person: dict[str, object] | None = None,
    ) -> None:
        super().__init__(master)
        self.db_file = db_file
        self._on_saved = on_saved
        self.person_id = None if person is None else int(person["id"])
        heading = "ویرایش پرسنل" if person is not None else "افزودن پرسنل"
        self.title(heading)
        self.resizable(False, False)
        self.transient(master.winfo_toplevel())
        self.configure(bg=BACKGROUND)

        self.first_name = tk.StringVar()
        self.last_name = tk.StringVar()
        self.work_hours = tk.StringVar()
        self.work_minutes = tk.StringVar()
        self.remote_id = tk.StringVar()
        self.mobile = tk.StringVar()
        self.cooperation_start = tk.StringVar()
        self.cooperation_end = tk.StringVar()
        if person is not None:
            hours, minutes = split_daily_hours(person["daily_hours"])
            self.first_name.set(str(person["first_name"]))
            self.last_name.set(str(person["last_name"]))
            self.work_hours.set(str(hours))
            self.work_minutes.set(str(minutes))
            self.remote_id.set(str(person["remote_id"] or ""))
            self.mobile.set(str(person["mobile"] or ""))
            if person.get("cooperation_start"):
                self.cooperation_start.set(format_shamsi_date(str(person["cooperation_start"])))
            if person.get("cooperation_end"):
                self.cooperation_end.set(format_shamsi_date(str(person["cooperation_end"])))

        body = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        body.pack(fill="both", expand=True, padx=16, pady=16)

        tk.Label(
            body,
            text=heading,
            font=(UI_FONT, 14, "bold"),
            bg=PANEL,
            fg=INK,
        ).pack(anchor="e", padx=18, pady=(16, 8))

        self.first_name_entry = self._field(body, "نام", self.first_name)
        self._field(body, "نام خانوادگی", self.last_name)
        self._duration_fields(body)
        self._field(body, "کد پرسنلی (اختیاری)", self.remote_id)
        self._field(body, "شماره همراه (اختیاری)", self.mobile)
        self.start_field = self._date_field(body, "تاریخ شروع همکاری", self.cooperation_start)
        self.end_field = self._date_field(body, "تاریخ پایان همکاری", self.cooperation_end)

        self.error = tk.Label(
            body,
            text="",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=DANGER,
            justify="right",
            wraplength=360,
        )
        self.error.pack(anchor="e", padx=18, pady=(4, 0))

        actions = tk.Frame(body, bg=PANEL)
        actions.pack(fill="x", padx=18, pady=(8, 16))
        self.save_button = _DialogButton(actions, "ذخیره", self.save, filled=True)
        self.save_button.pack(side="right")
        self.cancel_button = _DialogButton(actions, "انصراف", self.destroy, filled=False)
        self.cancel_button.pack(side="right", padx=(0, 8))

        self.bind("<Return>", lambda _event: self.save())
        self.bind("<Escape>", lambda _event: self.destroy())
        self.protocol("WM_DELETE_WINDOW", self.destroy)
        self.update_idletasks()
        _center_on_parent(self, master)
        self.first_name_entry.focus_set()
        if str(self.state()) == "normal":
            self.grab_set()

    def save(self) -> None:
        try:
            hours = parse_work_duration(self.work_hours.get(), self.work_minutes.get())
            started = _optional_shamsi_date(self.cooperation_start.get())
            ended = _optional_shamsi_date(self.cooperation_end.get())
            if self.person_id is None:
                add_personnel(
                    self.first_name.get(),
                    self.last_name.get(),
                    hours,
                    remote_id=self.remote_id.get(),
                    mobile=self.mobile.get(),
                    cooperation_start=started,
                    cooperation_end=ended,
                    db_file=self.db_file,
                )
            else:
                update_personnel(
                    self.person_id,
                    self.first_name.get(),
                    self.last_name.get(),
                    hours,
                    remote_id=self.remote_id.get(),
                    mobile=self.mobile.get(),
                    cooperation_start=started,
                    cooperation_end=ended,
                    db_file=self.db_file,
                )
        except ValueError as exc:
            self.error.configure(text=str(exc))
            return
        self._on_saved()
        self.destroy()

    def _field(self, parent: tk.Misc, label: str, variable: tk.StringVar) -> tk.Entry:
        tk.Label(parent, text=label, font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            anchor="e", padx=18, pady=(8, 2)
        )
        entry = tk.Entry(
            parent,
            textvariable=variable,
            font=(UI_FONT, 11),
            justify="right",
            relief="flat",
            bg=PANEL,
            fg=INK,
            highlightthickness=1,
            highlightbackground=LINE,
            highlightcolor=ACCENT,
        )
        entry.pack(fill="x", padx=18, ipady=6)
        return entry

    def _date_field(self, parent: tk.Misc, label: str, variable: tk.StringVar) -> ShamsiDateEntry:
        tk.Label(parent, text=label, font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            anchor="e", padx=18, pady=(8, 2)
        )
        field = ShamsiDateEntry(parent, variable)
        field.pack(anchor="e", padx=18)
        return field

    def _duration_fields(self, parent: tk.Misc) -> None:
        tk.Label(
            parent,
            text="میزان ساعت کاری",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=INK,
        ).pack(anchor="e", padx=18, pady=(8, 2))

        row = tk.Frame(parent, bg=PANEL)
        row.pack(fill="x", padx=18)
        self._hours_validate = (self.register(self._accept_whole_number), "%P")
        self._minutes_validate = (self.register(self._accept_minutes), "%P")
        self.hours_entry = self._duration_entry(row, self.work_hours, self._hours_validate)
        tk.Label(row, text="ساعت", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            side="right", padx=(18, 6)
        )
        self.minutes_entry = self._duration_entry(row, self.work_minutes, self._minutes_validate)
        tk.Label(row, text="دقیقه", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            side="right", padx=(0, 6)
        )
        tk.Label(
            parent,
            text="دقیقه را از ۰ تا ۵۹ وارد کنید.",
            font=(UI_FONT, 9),
            bg=PANEL,
            fg=MUTED,
        ).pack(anchor="e", padx=18, pady=(4, 8))

    def _duration_entry(
        self,
        parent: tk.Misc,
        variable: tk.StringVar,
        validate: tuple[str, str],
    ) -> tk.Entry:
        entry = tk.Entry(
            parent,
            textvariable=variable,
            width=6,
            font=(UI_FONT, 11),
            justify="center",
            relief="flat",
            bg=PANEL,
            fg=INK,
            highlightthickness=1,
            highlightbackground=LINE,
            highlightcolor=ACCENT,
            validate="key",
            validatecommand=validate,
        )
        entry.pack(side="right", ipady=6)
        return entry

    def _accept_whole_number(self, proposed: str) -> bool:
        return proposed == "" or _whole_number(proposed) is not None

    def _accept_minutes(self, proposed: str) -> bool:
        if proposed == "":
            return True
        value = _whole_number(proposed)
        return value is not None and value <= 59


def split_daily_hours(value: object) -> tuple[int, int]:
    total_minutes = int(round(float(value) * 60))
    if total_minutes < 0:
        total_minutes = 0
    return divmod(total_minutes, 60)


def parse_work_duration(hours_text: str, minutes_text: str) -> float:
    hours_raw = hours_text.strip()
    minutes_raw = minutes_text.strip()
    if not hours_raw and not minutes_raw:
        raise ValueError("میزان ساعت کاری را وارد کنید.")
    hours = _whole_number(hours_raw) if hours_raw else 0
    minutes = _whole_number(minutes_raw) if minutes_raw else 0
    if hours is None or minutes is None:
        raise ValueError("ساعت و دقیقه را با عدد وارد کنید.")
    if minutes > 59:
        raise ValueError("دقیقه نمی‌تواند بیشتر از ۵۹ باشد.")
    total = hours + minutes / 60
    if total <= 0:
        raise ValueError("میزان ساعت کاری باید بیشتر از صفر باشد.")
    return total


def _optional_shamsi_date(text: str) -> date | None:
    cleaned = text.strip()
    if not cleaned:
        return None
    return parse_shamsi_date(cleaned)


def _whole_number(text: str) -> int | None:
    cleaned = english_digits(text.strip())
    if not cleaned.isdigit():
        return None
    return int(cleaned)


def _center_on_parent(window: tk.Toplevel, master: tk.Misc) -> None:
    parent = master.winfo_toplevel()
    width = int(window.winfo_reqwidth() * 1.5)
    height = window.winfo_reqheight()
    x = parent.winfo_rootx() + max((parent.winfo_width() - width) // 2, 0)
    y = parent.winfo_rooty() + max((parent.winfo_height() - height) // 2, 0)
    window.geometry(f"{width}x{height}+{x}+{y}")
    window.minsize(width, height)


class _DialogButton(tk.Frame):
    def __init__(self, master: tk.Misc, text: str, command: Callable[[], None], filled: bool) -> None:
        bg = ACCENT if filled else PANEL
        fg = "#ffffff" if filled else INK
        super().__init__(
            master,
            bg=bg,
            cursor="hand2",
            padx=14,
            pady=6,
            highlightthickness=0 if filled else 1,
            highlightbackground=LINE,
        )
        self._command = command
        self.caption = tk.Label(self, text=text, font=(UI_FONT, 10), bg=bg, fg=fg)
        self.caption.pack()
        self.bind("<Button-1>", self._activate)
        self.caption.bind("<Button-1>", self._activate)

    def _activate(self, _event: tk.Event) -> None:
        self._command()
