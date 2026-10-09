from pathlib import Path

from atten import paths


def test_app_dir_is_project_root_when_not_frozen(monkeypatch):
    monkeypatch.setattr(paths, "is_frozen", lambda: False)
    assert paths.app_dir() == Path(__file__).resolve().parents[1]


def test_frozen_app_dir_is_beside_the_executable(monkeypatch, tmp_path):
    exe = tmp_path / "atten.exe"
    exe.write_bytes(b"")
    monkeypatch.setattr(paths, "is_frozen", lambda: True)
    monkeypatch.setattr(paths.sys, "executable", str(exe))
    assert paths.app_dir() == tmp_path


def test_db_path_is_under_data_dir(monkeypatch, tmp_path):
    monkeypatch.setattr(paths, "app_dir", lambda: tmp_path)
    assert paths.db_path() == tmp_path / "data" / "atten.db"
    assert (tmp_path / "data").is_dir()
