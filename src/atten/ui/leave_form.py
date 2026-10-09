"""Dialog for recording one leave."""

import tkinter as tk
from collections.abc import Callable
from pathlib import Path
from tkinter import ttk

from atten.dates import format_shamsi_date, parse_shamsi_date
from atten.db.repository import add_leave, list_personnel, update_leave
from atten.leave import compose_leave_minutes, format_leave_amount, split_leave_minutes
from atten.ui.fonts import UI_FONT
from atten.ui.shamsi_date import ShamsiDateEntry

BACKGROUND = "#eef1f4"
PANEL = "#ffffff"
INK = "#1f2933"
MUTED = "#64748b"
LINE = "#d8dee6"
ACCENT = "#14508a"
DANGER = "#b42318"
_DIGIT_MAP = str.maketrans("۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩", "01234567890123456789")


class LeaveForm(tk.Toplevel):
    def __init__(
        self,
        master: tk.Misc,
        db_file: Path,
        on_saved: Callable[[], None],
        personnel_id: int | None = None,
        leave: dict[str, object] | None = None,
        initial: dict[str, object] | None = None,
    ) -> None:
        super().__init__(master)
        self.db_file = db_file
        self._on_saved = on_saved
        self.leave_id = None if leave is None else int(leave["id"])
        self._people: list[dict[str, object]] = list_personnel(db_file)
        heading = "ویرایش مرخصی" if leave is not None else "ثبت مرخصی"
        self.title(heading)
        self.resizable(False, False)
        self.transient(master.winfo_toplevel())
        self.configure(bg=BACKGROUND)

        self.start_date = tk.StringVar()
        self.end_date = tk.StringVar()
        self.leave_days = tk.StringVar()
        self.leave_hours = tk.StringVar()
        self.leave_minutes = tk.StringVar()

        body = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        body.pack(fill="both", expand=True, padx=16, pady=16)

        tk.Label(
            body,
            text=heading,
            font=(UI_FONT, 14, "bold"),
            bg=PANEL,
            fg=INK,
        ).pack(anchor="e", padx=18, pady=(16, 4))
        tk.Label(
            body,
            text="هر روز مرخصی ۷ ساعت و ۲۰ دقیقه است. سقف هر سال ۳۰ روز است.",
            font=(UI_FONT, 9),
            bg=PANEL,
            fg=MUTED,
        ).pack(anchor="e", padx=18, pady=(0, 8))

        tk.Label(body, text="پرسنل", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            anchor="e", padx=18, pady=(8, 2)
        )
        self.person_combo = ttk.Combobox(
            body,
            state="readonly",
            justify="right",
            font=(UI_FONT, 11),
            width=32,
            values=[_person_label(person) for person in self._people],
        )
        self.person_combo.pack(fill="x", padx=18, ipady=4)
        chosen_id = personnel_id
        if chosen_id is None and leave is not None:
            chosen_id = int(leave["personnel_id"])
        if chosen_id is not None:
            for index, person in enumerate(self._people):
                if int(person["id"]) == chosen_id:
                    self.person_combo.current(index)
                    break

        self.start_field = self._date_field(body, "تاریخ شروع", self.start_date)
        self.end_field = self._date_field(body, "تاریخ پایان", self.end_date)
        self._duration_fields(body)

        self.preview = tk.Label(
            body,
            text="",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=INK,
            justify="right",
        )
        self.preview.pack(anchor="e", padx=18, pady=(8, 0))
        for variable in (self.leave_days, self.leave_hours, self.leave_minutes):
            variable.trace_add("write", lambda *_args: self._update_preview())

        self.error = tk.Label(
            body,
            text="",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=DANGER,
            justify="right",
            wraplength=380,
        )
        self.error.pack(anchor="e", padx=18, pady=(4, 0))

        actions = tk.Frame(body, bg=PANEL)
        actions.pack(fill="x", padx=18, pady=(8, 16))
        save_text = "ذخیره" if leave is not None else "ثبت"
        self.save_button = _DialogButton(actions, save_text, self.save, filled=True)
        self.save_button.pack(side="right")
        self.cancel_button = _DialogButton(actions, "انصراف", self.destroy, filled=False)
        self.cancel_button.pack(side="right", padx=(0, 8))

        source = leave if leave is not None else initial
        if source is not None:
            self._apply_source(source)

        self.bind("<Return>", lambda _event: self.save())
        self.bind("<Escape>", lambda _event: self.destroy())
        self.protocol("WM_DELETE_WINDOW", self.destroy)
        self.update_idletasks()
        _center_on_parent(self, master)
        self.person_combo.focus_set()
        if str(self.state()) == "normal":
            self.grab_set()

    def save(self) -> None:
        try:
            if self.person_combo.current() < 0:
                raise ValueError("یک پرسنل را انتخاب کنید.")
            person = self._people[self.person_combo.current()]
            start = parse_shamsi_date(self.start_date.get())
            end = parse_shamsi_date(self.end_date.get())
            if start > end:
                raise ValueError("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.")
            days = _optional_number(self.leave_days.get())
            hours = _optional_number(self.leave_hours.get())
            minutes = _optional_number(self.leave_minutes.get())
            if minutes > 59:
                raise ValueError("دقیقه نمی‌تواند بیشتر از ۵۹ باشد.")
            amount = compose_leave_minutes(days, hours, minutes)
            if self.leave_id is None:
                add_leave(
                    int(person["id"]),
                    start,
                    end,
                    amount,
                    db_file=self.db_file,
                )
            else:
                update_leave(
                    self.leave_id,
                    int(person["id"]),
                    start,
                    end,
                    amount,
                    db_file=self.db_file,
                )
        except ValueError as exc:
            self.error.configure(text=str(exc))
            return
        self._on_saved()
        self.destroy()

    def _apply_source(self, source: dict[str, object]) -> None:
        if source.get("start_date"):
            self.start_date.set(format_shamsi_date(source["start_date"]))
        if source.get("end_date"):
            self.end_date.set(format_shamsi_date(source["end_date"]))
        if source.get("minutes") is not None:
            days, hours, minutes = split_leave_minutes(int(source["minutes"]))
            self.leave_days.set(str(days))
            self.leave_hours.set(str(hours))
            self.leave_minutes.set(str(minutes))
        self._update_preview()

    def _update_preview(self) -> None:
        try:
            days = _optional_number(self.leave_days.get())
            hours = _optional_number(self.leave_hours.get())
            minutes = _optional_number(self.leave_minutes.get())
        except ValueError:
            self.preview.configure(text="")
            return
        amount = format_leave_amount(compose_leave_minutes(days, hours, minutes))
        self.preview.configure(text=f"معادل: {amount}")

    def _date_field(self, parent: tk.Misc, label: str, variable: tk.StringVar) -> ShamsiDateEntry:
        tk.Label(parent, text=label, font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            anchor="e", padx=18, pady=(8, 2)
        )
        field = ShamsiDateEntry(parent, variable, on_change=self._update_preview)
        field.pack(anchor="e", padx=18)
        return field

    def _duration_fields(self, parent: tk.Misc) -> None:
        tk.Label(parent, text="میزان مرخصی", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            anchor="e", padx=18, pady=(8, 2)
        )
        row = tk.Frame(parent, bg=PANEL)
        row.pack(fill="x", padx=18)
        self._days_validate = (self.register(self._accept_whole_number), "%P")
        self._hours_validate = (self.register(self._accept_whole_number), "%P")
        self._minutes_validate = (self.register(self._accept_minutes), "%P")
        self._duration_entry(row, self.leave_minutes, self._minutes_validate)
        tk.Label(row, text="دقیقه", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            side="right", padx=(0, 6)
        )
        self._duration_entry(row, self.leave_hours, self._hours_validate)
        tk.Label(row, text="ساعت", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            side="right", padx=(12, 6)
        )
        self._duration_entry(row, self.leave_days, self._days_validate)
        tk.Label(row, text="روز", font=(UI_FONT, 10), bg=PANEL, fg=INK).pack(
            side="right", padx=(12, 6)
        )

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


def _optional_number(text: str) -> int:
    raw = text.strip()
    if not raw:
        return 0
    value = _whole_number(raw)
    if value is None:
        raise ValueError("روز، ساعت و دقیقه را با عدد وارد کنید.")
    return value


def _whole_number(text: str) -> int | None:
    cleaned = text.strip().translate(_DIGIT_MAP)
    if not cleaned.isdigit():
        return None
    return int(cleaned)


def _person_label(person: dict[str, object]) -> str:
    name = f"{person['first_name']} {person['last_name']}"
    remote_id = person["remote_id"]
    if remote_id:
        return f"{name} ({remote_id})"
    return name


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
