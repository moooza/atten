using System.Runtime.InteropServices;
using Atten.Data;
using Windows.Storage;

namespace Atten.App.Backup;

/// <summary>Operator keep-count for automatic snapshots. Local settings, not domain logic.</summary>
internal static class BackupPreferences
{
    public const string SettingKey = "snapshot_keep";

    public static int Load()
    {
        try
        {
            object? stored = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (stored is int number)
            {
                return DatabaseBackup.NormalizeKeep(number);
            }

            if (stored is string text && int.TryParse(text, out int parsed))
            {
                return DatabaseBackup.NormalizeKeep(parsed);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (COMException)
        {
        }

        return DatabaseBackup.DefaultKeepSnapshots;
    }

    public static void Save(int keep)
    {
        int normalized = DatabaseBackup.NormalizeKeep(keep);
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = normalized;
        }
        catch (InvalidOperationException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (COMException)
        {
        }
    }
}
