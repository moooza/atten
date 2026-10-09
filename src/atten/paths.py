"""Resolve writable paths beside the executable, or at the repo root in development."""

import sys
from pathlib import Path


def is_frozen() -> bool:
    return bool(getattr(sys, "frozen", False))


def app_dir() -> Path:
    if is_frozen():
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[2]


def data_dir() -> Path:
    folder = app_dir() / "data"
    folder.mkdir(parents=True, exist_ok=True)
    return folder


def db_path() -> Path:
    return data_dir() / "atten.db"
