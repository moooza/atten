using Atten.Data;

namespace Atten.App.Backup;

/// <summary>
/// How to show <see cref="DatabaseRecoveryPage"/>. Not a shell destination.
/// </summary>
public sealed class DatabaseRecoveryRequest
{
    public bool Preview { get; init; }

    /// <summary>
    /// Newest first. Null in live mode loads healthy snapshots from the backup service.
    /// An empty list hides the snapshot panel.
    /// </summary>
    public IReadOnlyList<BackupSnapshot>? Snapshots { get; init; }

    public static DatabaseRecoveryRequest Live(IReadOnlyList<BackupSnapshot>? snapshots = null)
    {
        return new DatabaseRecoveryRequest { Preview = false, Snapshots = snapshots };
    }

    public static DatabaseRecoveryRequest PreviewWithSnapshots(IReadOnlyList<BackupSnapshot> snapshots)
    {
        return new DatabaseRecoveryRequest { Preview = true, Snapshots = snapshots };
    }

    public static DatabaseRecoveryRequest PreviewWithoutSnapshots()
    {
        return new DatabaseRecoveryRequest { Preview = true, Snapshots = [] };
    }

    /// <summary>Two dated rows so the snapshot list can be laid out when none exist yet.</summary>
    public static IReadOnlyList<BackupSnapshot> SampleSnapshots()
    {
        DateTime now = DateTime.Now;
        DateTime newer = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Local);
        return
        [
            new BackupSnapshot("preview-newer.db", newer),
            new BackupSnapshot("preview-older.db", newer.AddDays(-2)),
        ];
    }
}
