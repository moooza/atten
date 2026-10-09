"""Start logging, migrate the database, then open the window."""

import logging
import sqlite3
import tkinter as tk
from tkinter import messagebox

from atten.db.backup import prepare_database
from atten.logging_setup import setup_logging
from atten.paths import db_path
from atten.ui.fonts import load_ui_font
from atten.ui.main_window import MainWindow

log = logging.getLogger(__name__)

DAMAGED_MESSAGE = (
    "پایگاه داده آسیب دیده است و نسخهٔ پشتیبانی برای برگرداندن آن پیدا نشد. "
    "فایل خراب سر جایش مانده است."
)


def main() -> None:
    setup_logging()
    path = db_path()
    try:
        notice = prepare_database(path)
    except sqlite3.DatabaseError:
        log.exception("database at %s is damaged and has no backup", path)
        _report_damaged()
        return
    log.info("database ready at %s", path)
    load_ui_font()
    root = tk.Tk()
    MainWindow(root, notice=notice)
    root.mainloop()


def _report_damaged() -> None:
    root = tk.Tk()
    root.withdraw()
    messagebox.showerror("attendance", DAMAGED_MESSAGE)
    root.destroy()
