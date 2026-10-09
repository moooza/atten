"""Tkinter shell. Widgets only; SQL stays in the db package."""

import tkinter as tk
from pathlib import Path

from atten import __version__
from atten.paths import db_path
from atten.ui.backup_view import BackupView
from atten.ui.calculation_view import CalculationView
from atten.ui.clock_events_view import ClockEventsView
from atten.ui.fonts import UI_FONT, apply_ui_font
from atten.ui.leaves_view import LeavesView
from atten.ui.personnel_view import BACKGROUND, PersonnelView

BAR = "#ffffff"
INK = "#334155"
SELECTED_BG = "#e8f2fc"
SELECTED_FG = "#14508a"
HOVER_BG = "#f3f6f9"


class MainWindow(tk.Frame):
    def __init__(self, master: tk.Misc, db_file: Path | None = None, notice: str | None = None) -> None:
        apply_ui_font(master)
        super().__init__(master, bg=BACKGROUND)
        self.pack(fill="both", expand=True)
        self.db_file = db_path() if db_file is None else db_file
        if isinstance(master, tk.Tk):
            master.title(f"attendance نسخه {__version__}")
            master.minsize(760, 480)
            master.geometry("960x560")
            master.configure(bg=BACKGROUND)

        self.toolbar = tk.Frame(self, bg=BAR, height=72)
        self.toolbar.pack(side="top", fill="x")
        self.toolbar.pack_propagate(False)
        tk.Frame(self, bg="#d8dee6", height=1).pack(side="top", fill="x")
        self.notice_banner: tk.Label | None = None
        if notice:
            self.notice_banner = tk.Label(
                self,
                text=notice,
                font=(UI_FONT, 10),
                bg="#fff7ed",
                fg="#9a3412",
                anchor="e",
                justify="right",
                wraplength=900,
                padx=16,
                pady=8,
            )
            self.notice_banner.pack(side="top", fill="x")

        self.content = tk.Frame(self, bg=BACKGROUND)
        self.content.pack(side="top", fill="both", expand=True)

        self.dashboard_view = tk.Frame(self.content, bg=BACKGROUND)
        self.personnel_view = PersonnelView(self.content, self.db_file)
        self.clock_events_view = ClockEventsView(self.content, self.db_file)
        self.leaves_view = LeavesView(self.content, self.db_file)
        self.calculation_view = CalculationView(self.content, self.db_file)
        self.backup_view = BackupView(
            self.content,
            self.db_file,
            on_before_restore=self._close_editors,
            on_restored=self._reload_after_restore,
        )
        self.nav_items: dict[str, NavButton] = {}
        self.nav_items["dashboard"] = NavButton(
            self.toolbar,
            text="داشبورد",
            icon="\uE9D2",
            command=self.show_dashboard,
        )
        self.nav_items["dashboard"].pack(side="right", padx=(4, 16), pady=12)
        self.nav_items["personnel"] = NavButton(
            self.toolbar,
            text="پرسنل",
            icon="\uE716",
            command=self.show_personnel,
        )
        self.nav_items["personnel"].pack(side="right", padx=(4, 4), pady=12)
        self.nav_items["clock_events"] = NavButton(
            self.toolbar,
            text="ورود و خروج",
            icon="\uE823",
            command=self.show_clock_events,
        )
        self.nav_items["clock_events"].pack(side="right", padx=(4, 4), pady=12)
        self.nav_items["leaves"] = NavButton(
            self.toolbar,
            text="مرخصی‌ها",
            icon="\uE787",
            command=self.show_leaves,
        )
        self.nav_items["leaves"].pack(side="right", padx=(4, 4), pady=12)
        self.nav_items["calculation"] = NavButton(
            self.toolbar,
            text="محاسبه",
            icon="\uE8EF",
            command=self.show_calculation,
        )
        self.nav_items["calculation"].pack(side="right", padx=(4, 4), pady=12)
        self.nav_items["backup"] = NavButton(
            self.toolbar,
            text="پشتیبان",
            icon="\uE74E",
            command=self.show_backup,
        )
        self.nav_items["backup"].pack(side="right", padx=(4, 4), pady=12)
        self._active: str | None = None
        self.show_dashboard()

    def show_dashboard(self) -> None:
        self._show("dashboard", self.dashboard_view)

    def show_personnel(self) -> None:
        self._show("personnel", self.personnel_view, reload=True)

    def show_clock_events(self) -> None:
        self._show("clock_events", self.clock_events_view, reload=True)

    def show_leaves(self) -> None:
        self._show("leaves", self.leaves_view, reload=True)

    def show_calculation(self) -> None:
        self._show("calculation", self.calculation_view, reload=True)

    def show_backup(self) -> None:
        self._show("backup", self.backup_view)

    def _show(self, key: str, view: tk.Frame, *, reload: bool = False) -> None:
        self._activate(key)
        for page in (
            self.dashboard_view,
            self.personnel_view,
            self.clock_events_view,
            self.leaves_view,
            self.calculation_view,
            self.backup_view,
        ):
            if page is not view:
                page.pack_forget()
        view.pack(fill="both", expand=True)
        if reload:
            view.reload()

    def _close_editors(self) -> None:
        self.calculation_view._close_editor()

    def _reload_after_restore(self) -> None:
        for view in (self.personnel_view, self.leaves_view, self.calculation_view):
            form = view.form
            if form is not None and form.winfo_exists():
                form.destroy()
        had_sheet = self.calculation_view._range is not None
        self.personnel_view.reload()
        self.clock_events_view.reload()
        self.leaves_view.reload()
        self.calculation_view.reload()
        if had_sheet and self.calculation_view._selected_person() is not None:
            self.calculation_view.show_result()

    def _activate(self, key: str) -> None:
        self._active = key
        for name, button in self.nav_items.items():
            button.set_selected(name == key)


class NavButton(tk.Frame):
    def __init__(self, master: tk.Misc, text: str, icon: str, command) -> None:
        super().__init__(master, bg=BAR, cursor="hand2", padx=14, pady=6)
        self._command = command
        self._selected = False
        self.icon = tk.Label(
            self,
            text=icon,
            font=("Segoe Fluent Icons", 18),
            bg=BAR,
            fg=INK,
        )
        self.caption = tk.Label(self, text=text, font=(UI_FONT, 11), bg=BAR, fg=INK)
        self.icon.pack(side="right")
        self.caption.pack(side="right", padx=(8, 6))
        self._bind_clicks(self)

    def set_selected(self, selected: bool) -> None:
        self._selected = selected
        self._paint(SELECTED_BG if selected else BAR, SELECTED_FG if selected else INK)

    def _bind_clicks(self, widget: tk.Misc) -> None:
        widget.bind("<Button-1>", self._on_click)
        widget.bind("<Enter>", lambda _event: self._hover(True))
        widget.bind("<Leave>", lambda _event: self._hover(False))
        for child in widget.winfo_children():
            self._bind_clicks(child)

    def _on_click(self, _event: tk.Event) -> None:
        self._command()

    def _hover(self, inside: bool) -> None:
        if self._selected:
            return
        if not inside and self._pointer_inside():
            return
        self._paint(HOVER_BG if inside else BAR, INK)

    def _pointer_inside(self) -> bool:
        widget = self.winfo_containing(self.winfo_pointerx(), self.winfo_pointery())
        while widget is not None:
            if widget is self:
                return True
            widget = widget.master
        return False

    def _paint(self, bg: str, fg: str) -> None:
        self.configure(bg=bg)
        self.icon.configure(bg=bg, fg=fg)
        self.caption.configure(bg=bg, fg=fg)
