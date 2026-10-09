"""Choose a folder for a full database copy, or replace the live database from a file."""

import sqlite3
import tkinter as tk
from collections.abc import Callable
from datetime import datetime
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from atten.dates import format_shamsi_datetime
from atten.db.backup import (
    check_backup,
    describe_backup_health,
    export_database,
    restore_database,
    validate_backup,
)
from atten.paths import data_dir
from atten.ui.fonts import UI_FONT
from atten.ui.personnel_view import BACKGROUND, INK, LINE, MUTED, PANEL, _ActionButton

SUCCESS = "#067647"
DANGER = "#b42318"
RESTORE_WARNING = (
    "با بازگردانی این فایل، همهٔ اطلاعات فعلی حذف خواهند شد "
    "و جای آن‌ها را محتوای نسخهٔ پشتیبان می‌گیرد. ادامه می‌دهید؟"
)


class BackupView(tk.Frame):
    def __init__(
        self,
        master: tk.Misc,
        db_file: Path,
        *,
        on_before_restore: Callable[[], None] | None = None,
        on_restored: Callable[[], None] | None = None,
    ) -> None:
        super().__init__(master, bg=BACKGROUND)
        self.db_file = db_file
        self._on_before_restore = on_before_restore
        self._on_restored = on_restored
        self.folder: Path | None = None
        self.backup_file: Path | None = None
        self.check_file: Path | None = None
        self._checking = False

        header = tk.Frame(self, bg=BACKGROUND)
        header.pack(fill="x", padx=20, pady=(18, 4))
        tk.Label(
            header,
            text="پشتیبان",
            font=(UI_FONT, 14, "bold"),
            bg=BACKGROUND,
            fg=INK,
        ).pack(side="right")
        self.notice = tk.Label(
            self,
            text="",
            font=(UI_FONT, 10),
            bg=BACKGROUND,
            fg=DANGER,
            anchor="e",
            justify="right",
            wraplength=720,
        )
        self.notice.pack(fill="x", padx=20, pady=(0, 10))

        holder = tk.Frame(self, bg=BACKGROUND)
        holder.pack(fill="both", expand=True)
        self.scroll = ttk.Scrollbar(holder, orient="vertical")
        self.canvas = tk.Canvas(
            holder,
            bg=BACKGROUND,
            highlightthickness=0,
            yscrollcommand=self._on_canvas_scroll,
        )
        self.canvas.pack(side="left", fill="both", expand=True)
        self.body = tk.Frame(self.canvas, bg=BACKGROUND)
        self._window = self.canvas.create_window((0, 0), window=self.body, anchor="nw")
        self.body.bind("<Configure>", self._fit_scroll)
        self.canvas.bind("<Configure>", self._fit_width)
        self.canvas.bind("<MouseWheel>", self._on_mousewheel)

        export_card = _card(self.body)
        tk.Label(
            export_card,
            text="ایجاد نسخهٔ پشتیبان",
            font=(UI_FONT, 12, "bold"),
            bg=PANEL,
            fg=INK,
            anchor="e",
        ).pack(fill="x")
        tk.Label(
            export_card,
            text="یک پوشه انتخاب کنید تا نسخهٔ کامل پایگاه داده آنجا ذخیره شود. تاریخ و ساعت ساخت در نام فایل می‌آید.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
            wraplength=640,
        ).pack(fill="x", pady=(6, 0))
        export_row = tk.Frame(export_card, bg=PANEL)
        export_row.pack(fill="x", pady=(12, 0))
        self.export_button = _ActionButton(
            export_row,
            "ایجاد نسخهٔ پشتیبان",
            "\uE74E",
            self.create_backup,
            bg="#14508a",
            fg="#ffffff",
        )
        self.export_button.pack(side="left")
        self.choose_folder_button = _ActionButton(
            export_row,
            "انتخاب مسیر",
            "\uE8B7",
            self.choose_folder,
            bg=PANEL,
            fg="#14508a",
            border=True,
        )
        self.choose_folder_button.pack(side="right")
        self.folder_label = tk.Label(
            export_row,
            text="مسیری انتخاب نشده است.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
        )
        self.folder_label.pack(side="right", padx=(8, 8))

        restore_card = _card(self.body)
        tk.Label(
            restore_card,
            text="بازگردانی نسخهٔ پشتیبان",
            font=(UI_FONT, 12, "bold"),
            bg=PANEL,
            fg=INK,
            anchor="e",
        ).pack(fill="x")
        tk.Label(
            restore_card,
            text="یک فایل نسخهٔ پشتیبان انتخاب کنید. پیش از بازگردانی، تأیید گرفته می‌شود چون همهٔ اطلاعات فعلی حذف خواهند شد.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
            wraplength=640,
        ).pack(fill="x", pady=(6, 0))
        restore_row = tk.Frame(restore_card, bg=PANEL)
        restore_row.pack(fill="x", pady=(12, 0))
        self.restore_button = _ActionButton(
            restore_row,
            "بازگردانی",
            "\uE777",
            self.restore_backup,
            bg=DANGER,
            fg="#ffffff",
        )
        self.restore_button.pack(side="left")
        self.choose_file_button = _ActionButton(
            restore_row,
            "انتخاب فایل",
            "\uE8B7",
            self.choose_file,
            bg=PANEL,
            fg="#14508a",
            border=True,
        )
        self.choose_file_button.pack(side="right")
        self.file_label = tk.Label(
            restore_row,
            text="فایلی انتخاب نشده است.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
        )
        self.file_label.pack(side="right", padx=(8, 8))

        check_card = _card(self.body)
        tk.Label(
            check_card,
            text="بررسی سلامت",
            font=(UI_FONT, 12, "bold"),
            bg=PANEL,
            fg=INK,
            anchor="e",
        ).pack(fill="x")
        tk.Label(
            check_card,
            text="یک فایل نسخهٔ پشتیبان انتخاب کنید تا همهٔ جدول‌ها و رکوردها، به‌همراه ساختار فایل و ارتباط جدول‌ها، بررسی شود.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
            wraplength=640,
        ).pack(fill="x", pady=(6, 0))
        check_row = tk.Frame(check_card, bg=PANEL)
        check_row.pack(fill="x", pady=(12, 0))
        self.check_button = _ActionButton(
            check_row,
            "بررسی سلامت",
            "\uE73E",
            self.check_selected,
            bg="#14508a",
            fg="#ffffff",
        )
        self.check_button.pack(side="left")
        self.choose_check_file_button = _ActionButton(
            check_row,
            "انتخاب فایل",
            "\uE8B7",
            self.choose_check_file,
            bg=PANEL,
            fg="#14508a",
            border=True,
        )
        self.choose_check_file_button.pack(side="right")
        self.check_file_label = tk.Label(
            check_row,
            text="فایلی انتخاب نشده است.",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
        )
        self.check_file_label.pack(side="right", padx=(8, 8))
        self.progress = ttk.Progressbar(check_card, mode="determinate", maximum=1)
        self.progress.pack(fill="x", pady=(12, 0))
        self.check_status = tk.Label(
            check_card,
            text="",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=MUTED,
            anchor="e",
            justify="right",
        )
        self.check_status.pack(fill="x", pady=(8, 0))
        self.check_result = tk.Label(
            check_card,
            text="",
            font=(UI_FONT, 10),
            bg=PANEL,
            fg=INK,
            anchor="e",
            justify="right",
            wraplength=640,
        )
        self.check_result.pack(fill="x", pady=(8, 0))
        self._bind_mousewheel(self.body)

    def reload(self) -> None:
        return

    def choose_folder(self) -> None:
        selected = filedialog.askdirectory(
            parent=self.winfo_toplevel(),
            title="مسیر ذخیره نسخهٔ پشتیبان",
            initialdir=str(self.folder or data_dir()),
            mustexist=True,
        )
        if not selected:
            return
        self.set_folder(Path(selected))

    def set_folder(self, folder: Path) -> None:
        self.folder = folder
        self.folder_label.configure(text=str(folder), fg=INK)
        self.notice.configure(text="")

    def choose_file(self) -> None:
        initial = self.backup_file.parent if self.backup_file is not None else self.folder or data_dir()
        selected = filedialog.askopenfilename(
            parent=self.winfo_toplevel(),
            title="فایل نسخهٔ پشتیبان",
            initialdir=str(initial),
            filetypes=[("نسخه پشتیبان", "*.db"), ("همه فایل‌ها", "*.*")],
        )
        if not selected:
            return
        self.set_backup_file(Path(selected))

    def set_backup_file(self, path: Path) -> None:
        self.backup_file = path
        self.file_label.configure(text=path.name, fg=INK)
        self.notice.configure(text="")

    def create_backup(self) -> None:
        if self.folder is None:
            self._show_error("ابتدا مسیر ذخیره را انتخاب کنید.")
            return
        moment = datetime.now().replace(microsecond=0)
        try:
            saved = export_database(self.db_file, self.folder, moment=moment)
        except (OSError, sqlite3.Error) as exc:
            self._show_error(f"ساختن نسخهٔ پشتیبان ممکن نشد: {exc}")
            return
        shown = format_shamsi_datetime(moment, seconds=True)
        self.notice.configure(
            text=f"نسخهٔ پشتیبان {shown} ذخیره شد: {saved.name}",
            fg=SUCCESS,
        )

    def restore_backup(self) -> None:
        if self.backup_file is None:
            self._show_error("ابتدا فایل نسخهٔ پشتیبان را انتخاب کنید.")
            return
        try:
            validate_backup(self.db_file, self.backup_file)
        except ValueError as exc:
            self._show_error(str(exc))
            return
        if self._on_before_restore is not None:
            self._on_before_restore()
        confirmed = messagebox.askyesno(
            "حذف اطلاعات",
            RESTORE_WARNING,
            parent=self.winfo_toplevel(),
            default=messagebox.NO,
        )
        if not confirmed:
            return
        try:
            restore_database(self.db_file, self.backup_file)
        except (OSError, sqlite3.Error, ValueError) as exc:
            self._show_error(str(exc))
            return
        if self._on_restored is not None:
            self._on_restored()
        self.notice.configure(text="نسخهٔ پشتیبان برگردانده شد. اطلاعات قبلی حذف شد.", fg=SUCCESS)

    def choose_check_file(self) -> None:
        initial = self.check_file.parent if self.check_file is not None else self.folder or data_dir()
        selected = filedialog.askopenfilename(
            parent=self.winfo_toplevel(),
            title="فایل برای بررسی سلامت",
            initialdir=str(initial),
            filetypes=[("نسخه پشتیبان", "*.db"), ("همه فایل‌ها", "*.*")],
        )
        if not selected:
            return
        self.set_check_file(Path(selected))

    def set_check_file(self, path: Path) -> None:
        self.check_file = path
        self.check_file_label.configure(text=path.name, fg=INK)
        self.check_status.configure(text="")
        self.check_result.configure(text="")
        self.progress.configure(value=0)
        self.notice.configure(text="")

    def check_selected(self) -> None:
        if self._checking:
            return
        if self.check_file is None:
            self._show_error("ابتدا فایل را برای بررسی انتخاب کنید.")
            return
        self._checking = True
        self.check_result.configure(text="")
        self.progress.configure(value=0, maximum=1)
        try:
            result = check_backup(self.check_file, on_progress=self._show_progress)
        except (OSError, sqlite3.Error) as exc:
            self._show_error(f"بررسی فایل ممکن نشد: {exc}")
            return
        finally:
            self._checking = False
        text = describe_backup_health(result)
        self.check_result.configure(text=text, fg=SUCCESS if result.ok else DANGER)
        if result.ok:
            self.notice.configure(text="فایل پشتیبان سالم است.", fg=SUCCESS)
        else:
            self.notice.configure(text="فایل پشتیبان سالم نیست.", fg=DANGER)

    def _show_progress(self, done: int, total: int, message: str) -> None:
        self.progress.configure(maximum=max(total, 1), value=done)
        self.check_status.configure(text=message)
        self.update_idletasks()

    def _show_error(self, text: str) -> None:
        self.notice.configure(text=text, fg=DANGER)

    def _on_canvas_scroll(self, first: str, last: str) -> None:
        self.scroll.set(first, last)
        if float(first) <= 0 and float(last) >= 1:
            self.scroll.pack_forget()
        elif not self.scroll.winfo_ismapped():
            self.scroll.pack(side="right", fill="y")

    def _fit_width(self, event: tk.Event) -> None:
        self.canvas.itemconfigure(self._window, width=event.width)

    def _fit_scroll(self, _event: tk.Event) -> None:
        self.canvas.configure(scrollregion=self.canvas.bbox("all"))

    def _on_mousewheel(self, event: tk.Event) -> None:
        self.canvas.yview_scroll(int(-event.delta / 120), "units")

    def _bind_mousewheel(self, widget: tk.Misc) -> None:
        widget.bind("<MouseWheel>", self._on_mousewheel, add="+")
        for child in widget.winfo_children():
            self._bind_mousewheel(child)


def _card(parent: tk.Misc) -> tk.Frame:
    card = tk.Frame(parent, bg=PANEL, highlightthickness=1, highlightbackground=LINE)
    card.pack(fill="x", padx=20, pady=(0, 16))
    inner = tk.Frame(card, bg=PANEL)
    inner.pack(fill="x", padx=16, pady=16)
    return inner
