# Atten

برنامهٔ دسکتاپ WinUI 3 برای حضور و غیاب. فایل پایگاه داده در پوشهٔ `data` کنار ریشهٔ پروژه (در توسعه) یا کنار exe (نسخهٔ منتشرشده) می‌ماند.

این برنامه با فایل `dat` دانلودشده از دستگاه‌های ZKTeco کار می‌کند.

## دستگاه‌های تست‌شده

- F22

## پیش‌نیاز

- Windows 10 نسخه ۱۸۰۹ یا جدیدتر
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- در صورت خطای runtime هنگام اجرا: [Windows App Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)

## ساخت

این برنامه unpackaged و self-contained است. بدون `-p:Platform=x64` ساخت `Atten.App` با خطای Windows App SDK قطع می‌شود.

از ریشهٔ مخزن:

```powershell
dotnet build Atten.sln -c Debug -p:Platform=x64
```

خروجی اجرا:

`src\Atten.App\bin\x64\Debug\net8.0-windows10.0.19041.0\Atten.App.exe`

نسخهٔ انتشار:

```powershell
dotnet build src\Atten.App\Atten.App.csproj -c Release -p:Platform=x64
```

خروجی انتشار در `src\Atten.App\bin\x64\Release\net8.0-windows10.0.19041.0\` است.

## اجرا از سورس

از ریشهٔ مخزن:

```powershell
dotnet run --project src\Atten.App\Atten.App.csproj -c Debug -p:Platform=x64
```

پایگاه داده در `data/atten.db` ریشهٔ پروژه ساخته یا باز می‌شود.

## تست

```powershell
dotnet test Atten.sln
```

## پروژه‌ها

- `Atten.Core` — مرخصی، شیت، تاریخ، اعتبارسنجی
- `Atten.Data` — مخزن، migrate، اتصال، پشتیبان
- `Atten.UI.Theme` — توکن روشن/تیره و کنترل‌های مشترک
- `Atten.App` — صفحه‌ها و پوسته
