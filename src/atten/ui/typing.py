"""Keep typed text on Persian yeh and kaf.

The Windows Persian layout emits Arabic yeh (ي), alef maksura (ى), and kaf (ك).
Entry widgets rewrite those keystrokes and pastes to ی and ک.
"""

import tkinter as tk

from atten.persian import persian_letters

_KEY_LETTERS = {
    "\u064a": "\u06cc",  # ي → ی
    "\u0649": "\u06cc",  # ى → ی
    "\u0643": "\u06a9",  # ك → ک
}
_installed = False


def install_persian_typing() -> None:
    global _installed
    if _installed:
        return
    _installed = True
    original = tk.Entry.__init__

    def init(self: tk.Entry, *args: object, **kwargs: object) -> None:
        original(self, *args, **kwargs)
        self.bind("<KeyPress>", on_entry_key, add="+")
        self.bind("<KeyRelease>", _rewrite_event, add="+")
        self.bind("<FocusOut>", _rewrite_event, add="+")
        self.bind("<<Paste>>", _rewrite_after_paste, add="+")

    tk.Entry.__init__ = init  # type: ignore[method-assign]


def on_entry_key(event: tk.Event) -> str | None:
    replacement = _KEY_LETTERS.get(getattr(event, "char", ""))
    if replacement is None:
        return None
    widget: tk.Entry = event.widget
    try:
        if widget.selection_present():
            widget.delete("sel.first", "sel.last")
        widget.insert("insert", replacement)
    except tk.TclError:
        return "break"
    return "break"


def rewrite_entry(widget: tk.Entry) -> None:
    try:
        if not int(widget.winfo_exists()):
            return
        current = widget.get()
    except tk.TclError:
        return
    fixed = persian_letters(current)
    if fixed == current:
        return
    try:
        cursor = widget.index("insert")
    except tk.TclError:
        cursor = "end"
    validate = str(widget.cget("validate"))
    if validate != "none":
        widget.configure(validate="none")
    widget.delete(0, "end")
    widget.insert(0, fixed)
    if validate != "none":
        widget.configure(validate=validate)
    try:
        widget.icursor(cursor)
    except tk.TclError:
        return


def _rewrite_event(event: tk.Event) -> None:
    rewrite_entry(event.widget)


def _rewrite_after_paste(event: tk.Event) -> None:
    widget: tk.Entry = event.widget
    widget.after_idle(lambda: rewrite_entry(widget))
