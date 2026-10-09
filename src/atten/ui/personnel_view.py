"""Personnel list. Reads rows through the repository."""

import sqlite3
import tkinter as tk
from pathlib import Path
from tkinter import ttk

from atten.dates import format_shamsi_datetime
from atten.db.repository import list_personnel
from atten.ui.fonts import UI_FONT

BACKGROUND = "#eef1f4"
PANEL = "#ffffff"
INK = "#1f2933"
MUTED = "#64748b"
LINE = "#d8dee6"

COLUMNS = (
    "updated_at",
    "created_at",
    "mobile",
    "daily_hours",
    "last_name",
    "first_name",
    "remote_id",
    "id",
)
HEADINGS = {
    "id": "شناسه",
    "remote_id": "کد پرسنلی",
    "first_name": "نام",
    "last_name": "نام خانوادگی",
    "daily_hours": "ساعت کاری",
    "mobile": "شماره همراه",
    "created_at": "تاریخ افزودن",
    "updated_at": "تاریخ به‌روزرسانی",
}
WIDTHS = {
    "id": 80,
    "remote_id": 140,
    "first_name": 140,
    "last_name": 160,
    "daily_hours": 110,
    "mobile": 140,
    "created_at": 160,
    "updated_at": 170,
}


class PersonnelView(tk.Frame):
    def __init__(self, master: tk.Misc, db_file: Path) -> None:
        super().__init__(master, bg=BACKGROUND)
        self.db_file = db_file

        self.form: tk.Toplevel | None = None
        self._rows: dict[int, dict[str, object]] = {}

        header = tk.Frame(self, bg=BACKGROUND)
        header.pack(fill="x", padx=20, pady=(18, 10))
        self.add_button = _ActionButton(
            header,
            "افزودن پرسنل",
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
        self.notice = tk.Label(
            header,
            text="",
            font=(UI_FONT, 10),
            bg=BACKGROUND,
            fg="#b42318",
        )
        self.notice.pack(side="left", padx=(12, 0))
        tk.Label(
            header,
            text="لیست پرسنل",
            font=(UI_FONT, 14, "bold"),
            bg=BACKGROUND,
            fg=INK,
        ).pack(side="right")
        self.count = tk.Label(header, text="", font=(UI_FONT, 10), bg=BACKGROUND, fg=MUTED)
        self.count.pack(side="right", padx=(0, 12))

        self.empty = tk.Label(
            self,
            text="هنوز پرسنلی ثبت نشده است.",
            font=(UI_FONT, 11),
            bg=BACKGROUND,
            fg=MUTED,
        )
        self.table = tk.Frame(self, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
        self.tree = ttk.Treeview(
            self.table,
            columns=COLUMNS,
            show="headings",
            style="Personnel.Treeview",
            selectmode="browse",
        )
        self.tree.bind("<Double-1>", self._edit_on_double_click)
        self.tree.bind("<<TreeviewSelect>>", lambda _event: self.notice.configure(text=""))
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
        style.configure("Personnel.Treeview", font=(UI_FONT, 10), rowheight=30)
        style.configure("Personnel.Treeview.Heading", font=(UI_FONT, 10, "bold"))

    def open_form(self) -> None:
        self.notice.configure(text="")
        self._open_form(None)

    def open_edit(self) -> None:
        selected = self.tree.selection()
        if not selected:
            self.notice.configure(text="یک پرسنل را از لیست انتخاب کنید.")
            return
        person = self._rows.get(int(selected[0]))
        if person is None:
            self.notice.configure(text="این پرسنل دیگر در لیست نیست.")
            return
        self.notice.configure(text="")
        self._open_form(person)

    def _edit_on_double_click(self, event: tk.Event) -> None:
        if self.tree.identify_region(event.x, event.y) != "cell":
            return
        self.open_edit()

    def _open_form(self, person: dict[str, object] | None) -> None:
        from atten.ui.personnel_form import PersonnelForm

        if self.form is not None and self.form.winfo_exists():
            self.form.destroy()
        self.form = PersonnelForm(self, self.db_file, on_saved=self.reload, person=person)

    def reload(self) -> None:
        self._rows = {}
        self.tree.delete(*self.tree.get_children())
        try:
            rows = list_personnel(self.db_file)
        except sqlite3.Error as exc:
            self.count.configure(text="")
            self.table.pack_forget()
            self.empty.configure(text=f"خواندن لیست پرسنل ممکن نشد: {exc}")
            self.empty.pack(expand=True)
            return

        self.count.configure(text=f"{len(rows)} نفر")
        if not rows:
            self.table.pack_forget()
            self.empty.configure(text="هنوز پرسنلی ثبت نشده است.")
            self.empty.pack(expand=True)
            return

        self.empty.pack_forget()
        self.table.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        for index, row in enumerate(rows):
            person_id = int(row["id"])
            self._rows[person_id] = row
            self.tree.insert(
                "",
                "end",
                iid=str(person_id),
                tags=("odd" if index % 2 else "even",),
                values=(
                    _format_stamp(row["updated_at"]),
                    _format_stamp(row["created_at"]),
                    row["mobile"] or "",
                    _format_hours(row["daily_hours"]),
                    row["last_name"],
                    row["first_name"],
                    row["remote_id"] or "",
                    row["id"],
                ),
            )


class _ActionButton(tk.Frame):
    def __init__(
        self,
        master: tk.Misc,
        text: str,
        icon: str,
        command,
        *,
        bg: str,
        fg: str,
        border: bool = False,
    ) -> None:
        super().__init__(
            master,
            bg=bg,
            cursor="hand2",
            padx=12,
            pady=6,
            highlightthickness=1 if border else 0,
            highlightbackground=LINE,
        )
        self._command = command
        self.icon = tk.Label(
            self,
            text=icon,
            font=("Segoe Fluent Icons", 12),
            bg=bg,
            fg=fg,
        )
        self.caption = tk.Label(
            self,
            text=text,
            font=(UI_FONT, 10),
            bg=bg,
            fg=fg,
        )
        self.icon.pack(side="right")
        self.caption.pack(side="right", padx=(6, 4))
        self.bind("<Button-1>", self._activate)
        self.icon.bind("<Button-1>", self._activate)
        self.caption.bind("<Button-1>", self._activate)

    def _activate(self, _event: tk.Event) -> None:
        self._command()


def _format_stamp(value: object) -> str:
    if not value:
        return ""
    return format_shamsi_datetime(str(value))


def _format_hours(value: object) -> str:
    number = float(value)
    if number.is_integer():
        return str(int(number))
    return f"{number:.2f}".rstrip("0").rstrip(".")
