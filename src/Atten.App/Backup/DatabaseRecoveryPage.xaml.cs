using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;

namespace Atten.App.Backup;

public sealed partial class DatabaseRecoveryPage : Page
{
    private IReadOnlyList<BackupSnapshot> _snapshots = [];
    private string? _backupFile;
    private bool _busy;

    public DatabaseRecoveryPage()
    {
        InitializeComponent();
        Heading.Text = AppMessages.RecoveryTitle;
        SnapshotsTitle.Text = AppMessages.RecoverySnapshotsTitle;
        SnapshotsHint.Text = AppMessages.RecoverySnapshotsHint;
        RestoreSnapshotButton.Content = AppMessages.RecoveryRestoreSnapshot;
        FileTitle.Text = AppMessages.RecoveryFileTitle;
        FileHint.Text = AppMessages.BackupRestoreHint;
        RestoreFileButton.Content = AppMessages.BackupRestoreAction;
        ChooseFileButton.Content = AppMessages.BackupChooseFile;
        FileLabel.Text = AppMessages.BackupNoFile;
        PreviewBannerText.Text = AppMessages.RecoveryPreviewBanner;
        ClosePreviewButton.Content = AppMessages.RecoveryClosePreview;
    }

    public bool Preview { get; private set; }

    public event EventHandler? Restored;

    public event EventHandler? PreviewClosed;

    protected override void OnNavigatedTo(NavigationEventArgs args)
    {
        base.OnNavigatedTo(args);
        Apply(args.Parameter as DatabaseRecoveryRequest ?? DatabaseRecoveryRequest.Live());
    }

    public void Apply(DatabaseRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Preview = request.Preview;
        PreviewBanner.Visibility = Preview ? Visibility.Visible : Visibility.Collapsed;
        _snapshots = request.Snapshots ?? (Preview ? [] : AttenStore.Backup().ListSnapshots());
        Hint.Text = _snapshots.Count == 0 ? AppMessages.RecoveryNoSnapshots : AppMessages.RecoveryHint;
        BindSnapshots();
        Notice.Text = string.Empty;
        _backupFile = null;
        FileLabel.Text = AppMessages.BackupNoFile;
        ApplyTextStyle(FileLabel, "AppCaptionText");
    }

    private void BindSnapshots()
    {
        SnapshotCombo.Items.Clear();
        SnapshotPanel.Visibility = _snapshots.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (BackupSnapshot snapshot in _snapshots)
        {
            SnapshotCombo.Items.Add(new SnapshotOption(snapshot));
        }

        if (SnapshotCombo.Items.Count > 0)
        {
            SnapshotCombo.SelectedIndex = 0;
        }
    }

    private async void OnRestoreSnapshot(object sender, RoutedEventArgs args)
    {
        if (Preview)
        {
            ShowPreviewNotice();
            return;
        }

        if (_busy)
        {
            return;
        }

        BackupSnapshot? snapshot = SelectedSnapshot();
        if (snapshot is null)
        {
            ShowNotice(AppMessages.RecoveryNeedSnapshot, danger: true);
            return;
        }

        _busy = true;
        try
        {
            if (!await ConfirmSnapshot(snapshot))
            {
                return;
            }

            Service().RestoreSnapshot(snapshot.Id);
        }
        catch (Exception ex)
        {
            ShowNotice(ex.Message, danger: true);
            return;
        }
        finally
        {
            _busy = false;
        }

        ShowNotice(AppMessages.RecoverySnapshotRestored, danger: false);
        Restored?.Invoke(this, EventArgs.Empty);
    }

    private async void OnChooseFile(object sender, RoutedEventArgs args)
    {
        if (Preview)
        {
            ShowPreviewNotice();
            return;
        }

        string? path = await PickDatabaseFile();
        if (path is null)
        {
            return;
        }

        _backupFile = path;
        FileLabel.Text = Path.GetFileName(path);
        ApplyTextStyle(FileLabel, "AppInkText");
        Notice.Text = string.Empty;
    }

    private async void OnRestoreFile(object sender, RoutedEventArgs args)
    {
        if (Preview)
        {
            ShowPreviewNotice();
            return;
        }

        if (_busy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_backupFile))
        {
            ShowNotice(AppMessages.BackupNeedFile, danger: true);
            return;
        }

        IBackupService backup = Service();
        _busy = true;
        try
        {
            backup.Validate(_backupFile);
            if (!await ConfirmFile())
            {
                return;
            }

            backup.Restore(_backupFile);
        }
        catch (Exception ex)
        {
            ShowNotice(ex.Message, danger: true);
            return;
        }
        finally
        {
            _busy = false;
        }

        ShowNotice(AppMessages.BackupRestored, danger: false);
        Restored?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosePreview(object sender, RoutedEventArgs args)
    {
        if (!Preview)
        {
            return;
        }

        PreviewClosed?.Invoke(this, EventArgs.Empty);
    }

    private BackupSnapshot? SelectedSnapshot()
    {
        return SnapshotCombo.SelectedItem is SnapshotOption option ? option.Snapshot : null;
    }

    private sealed class SnapshotOption
    {
        public SnapshotOption(BackupSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public BackupSnapshot Snapshot { get; }

        public override string ToString()
        {
            return Dates.FormatShamsiDateTime(Snapshot.WrittenAt, seconds: true);
        }
    }

    private Task<bool> ConfirmSnapshot(BackupSnapshot snapshot)
    {
        return AppConfirmDialog.ShowAsync(
            XamlRoot,
            ActualTheme,
            new AppConfirmRequest
            {
                Title = AppMessages.RecoveryConfirmTitle,
                Ask = AppMessages.RecoverySnapshotAsk,
                Lines =
                [
                    new AppConfirmLine(
                        AppMessages.RecoverySnapshotDate,
                        Dates.FormatShamsiDateTime(snapshot.WrittenAt, seconds: true)),
                ],
                ConfirmText = AppMessages.BackupRestoreAction,
            });
    }

    private Task<bool> ConfirmFile()
    {
        return AppConfirmDialog.ShowAsync(
            XamlRoot,
            ActualTheme,
            new AppConfirmRequest
            {
                Title = AppMessages.BackupConfirmTitle,
                Ask = AppMessages.BackupRestoreWarning,
                ConfirmText = AppMessages.BackupRestoreAction,
            });
    }

    private async Task<string?> PickDatabaseFile()
    {
        Window? host = App.Current.HostWindow;
        if (host is null)
        {
            return null;
        }

        var picker = new FileOpenPicker(host.AppWindow.Id)
        {
            Title = AppMessages.BackupFilePickerTitle,
            CommitButtonText = AppMessages.BackupChooseFile,
        };
        picker.FileTypeFilter.Add(".db");
        picker.FileTypeFilter.Add("*");
        PickFileResult? picked = await picker.PickSingleFileAsync();
        if (picked is null || string.IsNullOrWhiteSpace(picked.Path))
        {
            return null;
        }

        return picked.Path;
    }

    private static IBackupService Service()
    {
        return App.Current.Store?.Backup ?? AttenStore.Backup();
    }

    private void ShowPreviewNotice()
    {
        Notice.Text = AppMessages.RecoveryPreviewNotice;
        ApplyTextStyle(Notice, "AppWarningText");
    }

    private void ShowNotice(string message, bool danger)
    {
        Notice.Text = message;
        ApplyTextStyle(Notice, danger ? "AppDangerText" : "AppSuccessText");
    }

    private static void ApplyTextStyle(TextBlock block, string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out object? resource) && resource is Style style)
        {
            block.Style = style;
        }
    }
}
