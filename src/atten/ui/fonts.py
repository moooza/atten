"""Persian-capable font for Tk widgets.

Shabnam is used when the bundled file is present. Otherwise Tahoma, which can
draw Persian on Windows.
"""

import sys
import tkinter as tk
from pathlib import Path

_BUNDLED_FAMILY = "Shabnam"
_FALLBACK_FAMILY = "Tahoma"


def _font_file() -> Path:
    if getattr(sys, "frozen", False):
        return Path(getattr(sys, "_MEIPASS")) / "atten" / "assets" / "Shabnam.ttf"
    return Path(__file__).resolve().parent.parent / "assets" / "Shabnam.ttf"


UI_FONT = _BUNDLED_FAMILY if _font_file().is_file() else _FALLBACK_FAMILY


def load_ui_font() -> None:
    """Register the bundled font for this process before the window opens."""
    path = _font_file()
    if not path.is_file() or sys.platform != "win32":
        return
    import ctypes

    ctypes.windll.gdi32.AddFontResourceExW(str(path), 0x10, 0)


def apply_ui_font(master: tk.Misc) -> None:
    import tkinter.font as tkfont

    for name in ("TkDefaultFont", "TkTextFont", "TkMenuFont", "TkHeadingFont"):
        try:
            tkfont.nametofont(name, root=master).configure(family=UI_FONT, size=10)
        except tk.TclError:
            continue
