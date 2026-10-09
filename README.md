# Atten

اسکلت برنامه دسکتاپ با Tkinter و SQLite. فایل پایگاه داده در پوشه `data` کنار برنامه می‌ماند، نه داخل بسته موقت PyInstaller.

این برنامه با فایل `dat` دانلودشده از دستگاه‌های ZKTeco کار می‌کند.

## دستگاه‌های تست‌شده

- F22

## پیش‌نیاز

Python 3.11 یا جدیدتر، همراه Tcl/Tk. نصب رسمی پایتون روی ویندوز Tkinter را دارد. اگر دستور `python` فقط میانبر Microsoft Store است، [uv](https://docs.astral.sh/uv/) می‌تواند پایتون را نصب کند: `uv venv --python 3.12 --seed .venv`.

## اجرا از سورس

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".[dev]"
.\.venv\Scripts\python.exe -m atten
```

پایگاه داده در `data/atten.db` ریشه پروژه ساخته می‌شود.

## تست

```powershell
.\.venv\Scripts\python.exe -m pytest
```

## ساخت نسخه پرتابل

```powershell
.\scripts\build.ps1
```

خروجی در `dist/atten/` است. `atten.exe` را اجرا کنید. `data/atten.db` کنار همان فایل ساخته می‌شود. برای انتقال، کل پوشه `dist/atten` را کپی کنید.
