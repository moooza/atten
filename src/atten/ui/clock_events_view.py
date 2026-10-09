"""Clock-event list. Reads rows through the repository."""

import sqlite3
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, ttk

from atten.attlog import load_attlog
from atten.dates import format_shamsi_date, format_shamsi_datetime
from atten.db.repository import ClockEventImport, import_clock_events, list_clock_events
from atten.paths import data_dir
from atten.ui.fonts import UI_FONT
from atten.ui.personnel_view import BACKGROUND, INK, LINE, MUTED, PANEL, _ActionButton

COLUMNS = (
    "updated_at",
    "created_at",
    "time",
    "date",
    "name",
    "remote_id",
    "id",
)
HEADINGS = {
    "id": "شناسه",
    "remote_id": "کد پرسنلی",
    "name": "نام",
    "date": "تاریخ",
    "time": "ساعت",
    "created_at": "تاریخ افزودن",
    "updated_at": "تاریخ به‌روزرسانی",
}
WIDTHS = {
    "id": 80,
    "remote_id": 140,
    "name": 180,
    "date": 120,
    "time": 100,
    "created_at": 160,
    "updated_at": 170,
}


class ClockEventsView(tk.Frame):
    def __init__(self, master: tk.Misc, db_file: Path) -> None:
        super().__init__(master, bg=BACKGROUND)
        self.db_file = db_file

        header = tk.Frame(self, bg=BACKGROUND)
        header.pack(fill="x", padx=20, pady=(18, 10))
        self.import_button = _ActionButton(
            header,
            "بارگذاری فایل",
            "\uE898",
            self.open_file,
            bg="#14508a",
            fg="#ffffff",
        )
        self.import_button.pack(side="left")
        self.notice = tk.Label(
            header,
            text="",
            font=(UI_FONT, 10),
            bg=BACKGROUND,
            fg=INK,
        )
        self.notice.pack(side="left", padx=(12, 0))
        tk.Label(
            header,
            text="ورود و خروج",
            font=(UI_FONT, 14, "bold"),
            bg=BACKGROUND,
            fg=INK,
        ).pack(side="right")
        self.count = tk.Label(header, text="", font=(UI_FONT, 10), bg=BACKGROUND, fg=MUTED)
        self.count.pack(side="right", padx=(0, 12))

        self.empty = tk.Label(
            self,
            text="هنوز ورود و خروجی ثبت نشده است.",
            font=(UI_FONT, 11),
            bg=BACKGROUND,
            fg=MUTED,
        )
        self.table = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.tree = ttk.Treeview(
            self.table,
            columns=COLUMNS,
            show="headings",
            style="ClockEvents.Treeview",
            selectmode="browse",
        )
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
        style.configure("ClockEvents.Treeview", font=(UI_FONT, 10), rowheight=30)
        style.configure("ClockEvents.Treeview.Heading", font=(UI_FONT, 10, "bold"))

    def open_file(self) -> None:
        folder = data_dir() / "import"
        if not folder.is_dir():
            folder = data_dir()
        selected = filedialog.askopenfilename(
            parent=self.winfo_toplevel(),
            title="فایل ورود و خروج",
            initialdir=str(folder),
            filetypes=[("Attendance log", "*.dat"), ("All files", "*.*")],
        )
        if not selected:
            return
        self.import_file(Path(selected))

    def import_file(self, path: Path) -> None:
        try:
            result = import_clock_events(load_attlog(path), db_file=self.db_file)
        except ValueError as exc:
            self.notice.configure(text=str(exc), fg="#b42318")
            return
        self.notice.configure(text=_import_notice(result), fg=INK)
        self.reload()

    def reload(self) -> None:
        self.tree.delete(*self.tree.get_children())
        try:
            rows = list_clock_events(self.db_file)
        except sqlite3.Error as exc:
            self.count.configure(text="")
            self.table.pack_forget()
            self.empty.configure(text=f"خواندن ورود و خروج ممکن نشد: {exc}")
            self.empty.pack(expand=True)
            return

        self.count.configure(text=f"{len(rows)} مورد")
        if not rows:
            self.table.pack_forget()
            self.empty.configure(text="هنوز ورود و خروجی ثبت نشده است.")
            self.empty.pack(expand=True)
            return

        self.empty.pack_forget()
        self.table.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        for index, row in enumerate(rows):
            self.tree.insert(
                "",
                "end",
                iid=str(row["id"]),
                tags=("odd" if index % 2 else "even",),
                values=(
                    _format_stamp(row["updated_at"]),
                    _format_stamp(row["created_at"]),
                    row["time"],
                    format_shamsi_date(str(row["date"])),
                    row["name"],
                    row["remote_id"],
                    row["id"],
                ),
            )


def _import_notice(result: ClockEventImport) -> str:
    if result.added:
        text = f"{result.added} مورد افزوده شد."
    else:
        text = "مورد جدیدی افزوده نشد."
    if result.skipped:
        text = f"{text} {result.skipped} مورد تکراری بود."
    return text


def _format_stamp(value: object) -> str:
    if not value:
        return ""
    return format_shamsi_datetime(str(value), seconds=True)
