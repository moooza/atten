# -*- mode: python ; coding: utf-8 -*-
"""Portable one-folder build. The database is created beside the exe, not in _MEIPASS."""

from pathlib import Path

root = Path(SPECPATH).resolve().parent
entry = root / "src" / "atten" / "__main__.py"
migrations = root / "src" / "atten" / "db" / "migrations"
font = root / "src" / "atten" / "assets" / "Shabnam.ttf"

a = Analysis(
    [str(entry)],
    pathex=[str(root / "src")],
    binaries=[],
    datas=[
        (str(migrations), "atten/db/migrations"),
        (str(font), "atten/assets"),
    ],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="atten",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="atten",
)
