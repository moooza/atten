using Atten.Core;
using Atten.UI.Theme;

namespace Atten.App;

public static class AppMessages
{
    public const string StartupFailed =
        "پایگاه داده آسیب دیده است و نسخهٔ پشتیبانی برای برگرداندن آن پیدا نشد. " +
        "فایل خراب سر جایش مانده است.";

    public const string RecoveryTitle = "بازیابی پایگاه داده";

    public const string RecoveryHint =
        "فایل اصلی پایگاه داده سالم نیست. یک نسخهٔ پشتیبان سالم یا یک فایل پشتیبان را برگردانید. " +
        "بستن پنجره تنها راه خروج بدون بازگردانی است.";

    public const string RecoveryNoSnapshots =
        "پایگاه داده آسیب دیده است و نسخهٔ پشتیبانی برای برگرداندن آن پیدا نشد. " +
        "یک فایل پشتیبان انتخاب کنید. بستن پنجره تنها راه خروج بدون بازگردانی است.";

    public const string RecoverySnapshotsTitle = "نسخه‌های پشتیبان سالم";

    public const string RecoverySnapshotsHint =
        "جدیدترین نسخه پیش‌انتخاب شده است. با تأیید، فایل خراب کنار گذاشته می‌شود و این نسخه جایگزین آن می‌شود.";

    public const string RecoveryRestoreSnapshot = "بازگردانی این نسخه";

    public const string RecoveryNeedSnapshot = "یک نسخهٔ پشتیبان را از فهرست انتخاب کنید.";

    public const string RecoveryConfirmTitle = "بازگردانی";

    public const string RecoverySnapshotAsk =
        "با بازگردانی این نسخه، فایل خراب کنار گذاشته می‌شود و جای آن را این پشتیبان می‌گیرد. ادامه می‌دهید؟";

    public const string RecoverySnapshotDate = "تاریخ: ";

    public const string RecoveryFileTitle = "بازگردانی از فایل پشتیبان";

    public const string RecoverySnapshotRestored = "نسخهٔ پشتیبان برگردانده شد.";

    public const string RecoveryPreviewBanner =
        "پایگاه فعلی سالم است. این فقط نمایش صفحه بازیابی است و داده‌ای عوض نمی‌شود.";

    public const string RecoveryPreviewNotice = "در نمایش آزمایشی بازگردانی انجام نمی‌شود.";

    public const string RecoveryClosePreview = "بستن نمایش آزمایشی";

    public const string BackupTitle = "پشتیبان";

    public const string BackupExportTitle = "ایجاد نسخهٔ پشتیبان";

    public const string BackupExportHint =
        "یک پوشه انتخاب کنید تا نسخهٔ کامل پایگاه داده آنجا ذخیره شود. تاریخ و ساعت ساخت در نام فایل می‌آید.";

    public const string BackupExportAction = "ایجاد نسخهٔ پشتیبان";

    public const string BackupChooseFolder = "انتخاب مسیر";

    public const string BackupFolderPickerTitle = "مسیر ذخیره نسخهٔ پشتیبان";

    public const string BackupNoFolder = "مسیری انتخاب نشده است.";

    public const string BackupNeedFolder = "ابتدا مسیر ذخیره را انتخاب کنید.";

    public const string BackupRestoreTitle = "بازگردانی نسخهٔ پشتیبان";

    public const string BackupRestoreHint =
        "یک فایل نسخهٔ پشتیبان انتخاب کنید. پیش از بازگردانی، تأیید گرفته می‌شود چون همهٔ اطلاعات فعلی حذف خواهند شد.";

    public const string BackupRestoreAction = "بازگردانی";

    public const string BackupChooseFile = "انتخاب فایل";

    public const string BackupFilePickerTitle = "فایل نسخهٔ پشتیبان";

    public const string BackupNoFile = "فایلی انتخاب نشده است.";

    public const string BackupNeedFile = "ابتدا فایل نسخهٔ پشتیبان را انتخاب کنید.";

    public const string BackupConfirmTitle = "حذف اطلاعات";

    public const string BackupRestoreWarning =
        "با بازگردانی این فایل، همهٔ اطلاعات فعلی حذف خواهند شد " +
        "و جای آن‌ها را محتوای نسخهٔ پشتیبان می‌گیرد. ادامه می‌دهید؟";

    public const string BackupCancel = "انصراف";

    public const string BackupRestored = "نسخهٔ پشتیبان برگردانده شد. اطلاعات قبلی حذف شد.";

    public const string BackupCheckTitle = "بررسی سلامت";

    public const string BackupCheckHint =
        "یک فایل نسخهٔ پشتیبان انتخاب کنید تا همهٔ جدول‌ها و رکوردها، به‌همراه ساختار فایل و ارتباط جدول‌ها، بررسی شود.";

    public const string BackupCheckAction = "بررسی سلامت";

    public const string BackupCheckPickerTitle = "فایل برای بررسی سلامت";

    public const string BackupNeedCheckFile = "ابتدا فایل را برای بررسی انتخاب کنید.";

    public const string BackupHealthy = "فایل پشتیبان سالم است.";

    public const string BackupUnhealthy = "فایل پشتیبان سالم نیست.";

    public const string BackupExportFailed = "ساختن نسخهٔ پشتیبان ممکن نشد";

    public const string BackupCheckFailed = "بررسی فایل ممکن نشد";

    public const string ChoosePersonnel = "یک پرسنل را انتخاب کنید.";

    public const string PersonnelLoadFailed = "خواندن پرسنل ممکن نشد";

    public const string PersonnelListFailed = "خواندن لیست پرسنل ممکن نشد";

    public const string PersonnelEmpty = "هنوز پرسنلی ثبت نشده است.";

    public const string PersonnelSelectOne = "یک پرسنل را از لیست انتخاب کنید.";

    public const string PersonnelAdd = "افزودن پرسنل";

    public const string PersonnelEdit = "ویرایش پرسنل";

    public const string ClockListFailed = "خواندن ورود و خروج ممکن نشد";

    public const string ClockEmpty = "هنوز ورود و خروجی ثبت نشده است.";

    public const string ClockEmptyMonth = "در این ماه ورود و خروجی ثبت نشده است.";

    public const string ClockFileReadFailed = "خواندن فایل ممکن نشد.";

    public const string ClockPickerTitle = "فایل ورود و خروج";

    public const string ClockImportNone = "مورد جدیدی افزوده نشد.";

    public const string ClockImportTitle = "نتیجه بارگذاری";

    public const string LeaveListFailed = "خواندن مرخصی ممکن نشد";

    public const string LeaveEmpty = "برای این پرسنل مرخصی ثبت نشده است.";

    public const string LeaveEmptyYears = "در سال‌های انتخاب‌شده مرخصی ثبت نشده است.";

    public const string LeaveChooseYear = "سال را انتخاب کنید.";

    public const string LeaveSelectOne = "یک مرخصی را از لیست انتخاب کنید.";

    public const string LeaveDelete = "حذف";

    public const string LeaveDeleteTitle = "حذف مرخصی";

    public const string LeaveDeleteFailed = "حذف مرخصی ممکن نشد";

    public const string LeaveDeleteAsk = "این مرخصی حذف شود؟";

    public const string LeaveAdd = "ثبت مرخصی";

    public const string LeaveEdit = "ویرایش مرخصی";

    public const string LeaveSaveNew = "ثبت";

    public const string LeaveSaveEdit = "ذخیره";

    public const string LeaveMissingStart = "تاریخ شروع همکاری ثبت نشده است.";

    public const string LeaveEndBeforeStart = "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.";

    public const string LeaveInvalidNumber = "روز، ساعت و دقیقه را با عدد وارد کنید.";

    public const string LeaveHint =
        "هر روز مرخصی ۷ ساعت و ۲۰ دقیقه است. هر ماه ۲٫۵ روز. جمعه و تعطیل رسمی شمرده نمی‌شود. " +
        "پیش‌فرض انتقال ۹ روز است؛ مانده را می‌توان تسویه یا اضافه به سال بعد منتقل کرد.";

    public const string LeaveLeftoverHint =
        "میزان انتقال و تسویه را جدا وارد کنید. جمع آن‌ها از مانده بیشتر نشود. " +
        "می‌توان بیشتر از ۹ روز منتقل کرد، یا بخشی را تسویه و بخشی را منتقل کرد. " +
        "سال قطع همکاری منتقل نمی‌شود و می‌توان کل مانده را تسویه کرد.";

    public const string LeaveLeftoverFailed = "ثبت مانده ممکن نشد";

    public const string LeaveKindLeave = "مرخصی";

    public const string LeaveKindSettlement = "تسویه";

    public const string LeaveSettlementNoEdit = "تسویه را نمی‌توان ویرایش کرد.";

    public const string CalculationChooseRange = "کارمند و بازه تاریخ را انتخاب کنید.";

    public const string CalculationNeedDates = "تاریخ شروع و پایان را انتخاب کنید.";

    public const string CalculationMissingRemoteId = "برای این کارمند Remote ID ثبت نشده است.";

    public const string CalculationHolidayFailed = "ذخیره روز تعطیل ممکن نشد";

    public const string CalculationResetFailed = "بازنشانی ورود و خروج ممکن نشد";

    public const string CalculationTimeFailed = "ذخیره ساعت ممکن نشد";

    public const string CalculationResetTitle = "بازنشانی ورود و خروج";

    public const string CalculationResetAsk = "ورود و خروج این روز پاک شود و دوباره از جدول ورود و خروج خوانده شود؟";

    public const string CalculationAddLeave = "افزودن مرخصی";

    public const string CalculationReset = "بازنشانی";

    public const string AdvancedSettings = "تنظیمات پیشرفته";

    public const string SnapshotKeepTitle = "تعداد نسخه‌های پشتیبان خودکار";

    public const string SnapshotKeepHint =
        "بعد از هر ذخیره، نسخه‌های قدیمی‌تر از این تعداد حذف می‌شوند. پیش‌فرض ۶۰ است.";

    public const string SnapshotKeepSave = "ثبت";

    public const string SnapshotKeepSaved = "تعداد نسخه‌های پشتیبان ذخیره شد.";

    public const string SnapshotKeepInvalid = "تعداد باید یک عدد بین ۱ و ۳۶۵ باشد.";

    public const string RecoveryPreviewSection = "نمایش آزمایشی بازیابی";

    public const string RecoveryPreviewSectionHint =
        "پایگاه فعلی عوض نمی‌شود. صفحه بازیابی را همان‌طور که در استارت دیده می‌شود نشان می‌دهد.";

    public const string RecoveryPreviewWithSnapshots = "نمایش بازیابی با نسخهٔ پشتیبان";

    public const string RecoveryPreviewWithoutSnapshots = "نمایش بازیابی بدون نسخهٔ پشتیبان";

    public const string DashboardHolidayMissing = "تعطیلات رسمی امسال اضافه نشده است.";

    public const string DashboardHolidayMissingHint =
        "از دکمه تنظیمات در نوار بالا، تنظیمات پیشرفته را باز کنید و مدیریت تعطیلات رسمی را بزنید.";

    public const string DashboardHolidayReady = "تعطیلات رسمی امسال ثبت شده است.";

    public const string OfficialHolidays = "تعطیلات رسمی";

    public const string OfficialHolidayManage = "مدیریت تعطیلات رسمی";

    public const string OfficialHolidayHint =
        "تعطیلات رسمی هر سال جداگانه ثبت می‌شود. جمعه در این فهرست نیست. " +
        "تاریخ جابه‌جاشونده را می‌توان ویرایش کرد و تعطیل اعلامی را دستی افزود.";

    public const string OfficialHolidayAdd = "افزودن تعطیلات رسمی";

    public const string OfficialHolidayPack = "بارگذاری بسته سال";

    public const string OfficialHolidayPackAsk =
        "روزهای تعطیل رسمی این سال افزوده شوند؟ تاریخ جابه‌جاشونده را بعداً می‌توان ویرایش کرد.";

    public const string OfficialHolidayPackMissing = "برای این سال بسته رسمی آماده نیست.";

    public const string OfficialHolidayPackNoneNew = "همهٔ روزهای این بسته از قبل ثبت شده‌اند.";

    public const string OfficialHolidayPackFailed = "بارگذاری بسته سال ممکن نشد";

    public const string OfficialHolidayEmptyPack =
        "برای این سال تعطیل رسمی ثبت نشده است. می‌توانید بسته سال را بارگذاری کنید.";

    public const string OfficialHolidayEdit = "ویرایش تعطیل رسمی";

    public const string OfficialHolidayDelete = "حذف";

    public const string OfficialHolidayDeleteTitle = "حذف تعطیل رسمی";

    public const string OfficialHolidayDeleteAsk = "این تعطیل رسمی حذف شود؟";

    public const string OfficialHolidayDeleteFailed = "حذف تعطیل رسمی ممکن نشد";

    public const string OfficialHolidayListFailed = "خواندن تعطیلات رسمی ممکن نشد";

    public const string OfficialHolidayEmpty = "برای این سال تعطیل رسمی ثبت نشده است.";

    public const string OfficialHolidayChooseYear = "سال را انتخاب کنید.";

    public const string OfficialHolidaySelectOne = "یک تعطیل رسمی را از لیست انتخاب کنید.";

    public const string OfficialHolidayNeedDate = "تاریخ را انتخاب کنید.";

    public const string OfficialHolidayNeedStart = "تاریخ شروع را انتخاب کنید.";

    public const string OfficialHolidayNeedEnd = "تاریخ پایان را انتخاب کنید.";

    public const string OfficialHolidayEndBeforeStart = "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.";

    public const string OfficialHolidaySelectNone = "حداقل یک تعطیل رسمی را انتخاب کنید.";

    public const string OfficialHolidayDeleteManyTitle = "حذف تعطیل‌های رسمی";

    public const string OfficialHolidayDeleteManyAsk = "تعطیل‌های رسمی انتخاب‌شده حذف شوند؟";

    public static string OfficialHolidayDays(int count)
    {
        return $"معادل: {count} روز";
    }

    public static string ClockCount(int count)
    {
        return $"{count} مورد";
    }

    public static string LeaveCount(int count)
    {
        return $"{count} مورد";
    }

    public static string OfficialHolidayCount(int count)
    {
        return $"{count} مورد";
    }

    public static string OfficialHolidayPackTitle(int year)
    {
        return $"بارگذاری بسته سال {year}";
    }

    public static string OfficialHolidayPackNotice(int added, int skipped)
    {
        string text = added > 0 ? $"{added} مورد افزوده شد." : OfficialHolidayPackNoneNew;
        if (skipped > 0)
        {
            text = $"{text} {skipped} مورد از قبل ثبت بود.";
        }

        return text;
    }

    public static string OfficialHolidayPackSkipped(int count)
    {
        return $"{count} روز از قبل ثبت شده و دوباره افزوده نمی‌شود.";
    }

    public static string LeavePreview(int minutes)
    {
        return $"معادل: {Leave.FormatLeaveAmount(minutes)}";
    }

    public static string LeaveSpan(string startDate, string endDate, string amount)
    {
        string range = startDate == endDate ? startDate : $"{startDate} تا {endDate}";
        return $"{range} — {amount}";
    }

    public static string LeaveBalanceNow(int year, int minutes)
    {
        return $"مانده مرخصی سال {year}: {Leave.FormatSignedLeaveAmount(minutes)}";
    }

    public static string LeaveLeftoverTitle(int year)
    {
        return $"مانده سال {year}";
    }

    public static string LeaveLeftoverRemaining(int minutes)
    {
        return $"مانده کل: {Leave.FormatSignedLeaveAmount(minutes)}";
    }

    public static string LeaveLeftoverUnallocated(int minutes)
    {
        return $"باقی‌مانده تخصیص‌نداده: {Leave.FormatSignedLeaveAmount(minutes)}";
    }

    public static IReadOnlyList<AppNoticeLine> ClockImportLines(
        int added,
        int skipped,
        IReadOnlyList<AttlogIssue> errors)
    {
        var lines = new List<AppNoticeLine>();
        if (skipped > 0)
        {
            lines.Add(new AppNoticeLine($"{skipped} مورد تکراری بود.", "AppWarningText"));
        }

        lines.Add(new AppNoticeLine(
            added > 0 ? $"{added} مورد افزوده شد." : ClockImportNone,
            "AppSuccessText"));
        if (errors.Count == 0)
        {
            return lines;
        }

        lines.Add(new AppNoticeLine($"{errors.Count} مورد خطا داشت.", "AppDangerText"));
        foreach (AttlogIssue error in errors.OrderBy(item => item.LineNumber))
        {
            lines.Add(new AppNoticeLine(error.Message, "AppDangerText"));
        }

        return lines;
    }
}
