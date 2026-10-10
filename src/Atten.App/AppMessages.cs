using Atten.Core;

namespace Atten.App;

public static class AppMessages
{
    public const string StartupFailed =
        "پایگاه داده آسیب دیده است و نسخهٔ پشتیبانی برای برگرداندن آن پیدا نشد. " +
        "فایل خراب سر جایش مانده است.";

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

    public const string ClockFileReadFailed = "خواندن فایل ممکن نشد.";

    public const string ClockPickerTitle = "فایل ورود و خروج";

    public const string ClockImportNone = "مورد جدیدی افزوده نشد.";

    public const string LeaveListFailed = "خواندن مرخصی ممکن نشد";

    public const string LeaveEmpty = "برای این پرسنل مرخصی ثبت نشده است.";

    public const string LeaveEmptyYears = "در سال‌های انتخاب‌شده مرخصی ثبت نشده است.";

    public const string LeaveChooseYear = "سال را انتخاب کنید.";

    public const string LeaveSelectOne = "یک مرخصی را از لیست انتخاب کنید.";

    public const string LeaveAdd = "ثبت مرخصی";

    public const string LeaveEdit = "ویرایش مرخصی";

    public const string LeaveSaveNew = "ثبت";

    public const string LeaveSaveEdit = "ذخیره";

    public const string LeaveMissingStart = "تاریخ شروع همکاری ثبت نشده است.";

    public const string LeaveEndBeforeStart = "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.";

    public const string LeaveInvalidNumber = "روز، ساعت و دقیقه را با عدد وارد کنید.";

    public const string LeaveBeforeTransfer = "قبل از انتقال";

    public const string LeaveAfterTransfer = "بعد از انتقال";

    public const string LeaveHint =
        "هر روز مرخصی ۷ ساعت و ۲۰ دقیقه است. هر ماه ۲٫۵ روز، انتقال حداکثر ۹ روز.";

    public const string CalculationChooseRange = "کارمند و بازه تاریخ را انتخاب کنید.";

    public const string CalculationNeedDates = "تاریخ شروع و پایان را انتخاب کنید.";

    public const string CalculationMissingRemoteId = "برای این کارمند Remote ID ثبت نشده است.";

    public const string CalculationHolidayFailed = "ذخیره روز تعطیل ممکن نشد";

    public const string CalculationResetFailed = "بازنشانی ورود و خروج ممکن نشد";

    public const string CalculationTimeFailed = "ذخیره ساعت ممکن نشد";

    public const string CalculationResetTitle = "بازنشانی ورود و خروج";

    public const string CalculationAddLeave = "افزودن مرخصی";

    public const string CalculationReset = "بازنشانی";

    public const string PayrollTitle = "حقوق";

    public const string PayrollHint =
        "محاسبه از کارکرد ماه و مانده مرخصی ساخته می‌شود. تأیید، پیش‌نویس را برای همین کاربر قفل می‌کند.";

    public const string PayrollListFailed = "خواندن حقوق ممکن نشد";

    public const string PayrollEmpty = "برای این پرسنل حقوق ثبت نشده است.";

    public const string PayrollSelectOne = "یک حقوق را از لیست انتخاب کنید.";

    public const string PayrollNeedPeriod = "سال و ماه را انتخاب کنید.";

    public const string PayrollDraft = "محاسبه پیش‌نویس";

    public const string PayrollConfirm = "تأیید";

    public const string PayrollReopen = "بازگشت به پیش‌نویس";

    public const string PayrollConfirmed = "حقوق تأیید شد.";

    public const string PayrollReopened = "حقوق به پیش‌نویس برگشت.";

    public static string CalculationResetPrompt(string shamsiDate)
    {
        return $"ورود و خروج {shamsiDate} پاک شود و دوباره از جدول ورود و خروج خوانده شود؟";
    }

    public static string ClockCount(int count)
    {
        return $"{count} مورد";
    }

    public static string LeaveCount(int count)
    {
        return $"{count} مورد";
    }

    public static string PayrollCount(int count)
    {
        return $"{count} مورد";
    }

    public static string LeavePreview(int minutes)
    {
        return $"معادل: {Leave.FormatLeaveAmount(minutes)}";
    }

    public static string LeaveRemaining(int beforeTransfer, int afterTransfer)
    {
        return $"{LeaveBeforeTransfer} {Leave.FormatSignedLeaveAmount(beforeTransfer)}\n{LeaveAfterTransfer} {Leave.FormatSignedLeaveAmount(afterTransfer)}";
    }

    public static string ClockImportNotice(int added, int skipped)
    {
        string text = added > 0 ? $"{added} مورد افزوده شد." : ClockImportNone;
        if (skipped > 0)
        {
            text = $"{text} {skipped} مورد تکراری بود.";
        }

        return text;
    }
}
