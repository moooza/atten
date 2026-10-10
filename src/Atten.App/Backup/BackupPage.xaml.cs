using Atten.Core;
using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace Atten.App.Backup;

public sealed partial class BackupPage : Page
{
    private readonly IBackupService? _backup;
    private string? _folder;
    private string? _backupFile;
    private string? _checkFile;
    private bool _checking;

    public BackupPage()
    {
        InitializeComponent();
        _backup = App.Current.Store?.Backup;
    }

    private async void OnChooseFolder(object sender, RoutedEventArgs args)
    {
        Window? host = App.Current.HostWindow;
        if (host is null)
        {
            return;
        }

        var picker = new FolderPicker(host.AppWindow.Id)
        {
            Title = AppMessages.BackupFolderPickerTitle,
            CommitButtonText = AppMessages.BackupChooseFolder,
        };
        PickFolderResult? picked = await picker.PickSingleFolderAsync();
        if (picked is null || string.IsNullOrWhiteSpace(picked.Path))
        {
            return;
        }

        SetFolder(picked.Path);
    }

    private void OnCreateBackup(object sender, RoutedEventArgs args)
    {
        if (_backup is null)
        {
            ShowNotice(AppMessages.BackupExportFailed, danger: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_folder))
        {
            ShowNotice(AppMessages.BackupNeedFolder, danger: true);
            return;
        }

        DateTime now = DateTime.Now;
        DateTime moment = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Local);
        try
        {
            string saved = _backup.Export(_folder, moment);
            string shown = Dates.FormatShamsiDateTime(moment, seconds: true);
            ShowNotice($"نسخهٔ پشتیبان {shown} ذخیره شد: {Path.GetFileName(saved)}", danger: false);
        }
        catch (Exception ex)
        {
            ShowNotice($"{AppMessages.BackupExportFailed}: {ex.Message}", danger: true);
        }
    }

    private async void OnChooseFile(object sender, RoutedEventArgs args)
    {
        string? path = await PickDatabaseFile(AppMessages.BackupFilePickerTitle);
        if (path is null)
        {
            return;
        }

        SetBackupFile(path);
    }

    private async void OnRestore(object sender, RoutedEventArgs args)
    {
        if (_backup is null)
        {
            ShowNotice(AppMessages.BackupNeedFile, danger: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_backupFile))
        {
            ShowNotice(AppMessages.BackupNeedFile, danger: true);
            return;
        }

        try
        {
            _backup.Validate(_backupFile);
        }
        catch (ArgumentException ex)
        {
            ShowNotice(ex.Message, danger: true);
            return;
        }

        if (!await ConfirmRestore())
        {
            return;
        }

        try
        {
            _backup.Restore(_backupFile);
        }
        catch (Exception ex)
        {
            ShowNotice(ex.Message, danger: true);
            return;
        }

        App.Current.ReloadStore();
        ShowNotice(AppMessages.BackupRestored, danger: false);
    }

    private async void OnChooseCheckFile(object sender, RoutedEventArgs args)
    {
        string? path = await PickDatabaseFile(AppMessages.BackupCheckPickerTitle);
        if (path is null)
        {
            return;
        }

        SetCheckFile(path);
    }

    private async void OnCheck(object sender, RoutedEventArgs args)
    {
        if (_checking)
        {
            return;
        }

        if (_backup is null)
        {
            ShowNotice(AppMessages.BackupCheckFailed, danger: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_checkFile))
        {
            ShowNotice(AppMessages.BackupNeedCheckFile, danger: true);
            return;
        }

        _checking = true;
        CheckButton.IsEnabled = false;
        CheckResult.Text = string.Empty;
        CheckProgress.Maximum = 1;
        CheckProgress.Value = 0;
        string path = _checkFile;
        IBackupService backup = _backup;
        try
        {
            BackupHealth result = await Task.Run(() => backup.Check(path, OnCheckProgress));
            string text = backup.Describe(result);
            CheckResult.Text = text;
            ApplyTextStyle(CheckResult, result.Ok ? "AppSuccessText" : "AppDangerText");
            ShowNotice(result.Ok ? AppMessages.BackupHealthy : AppMessages.BackupUnhealthy, danger: !result.Ok);
        }
        catch (Exception ex)
        {
            ShowNotice($"{AppMessages.BackupCheckFailed}: {ex.Message}", danger: true);
        }
        finally
        {
            _checking = false;
            CheckButton.IsEnabled = true;
        }
    }

    internal void SetFolder(string folder)
    {
        _folder = folder;
        FolderLabel.Text = folder;
        ApplyTextStyle(FolderLabel, "AppInkText");
        Notice.Text = string.Empty;
    }

    internal void SetBackupFile(string path)
    {
        _backupFile = path;
        FileLabel.Text = Path.GetFileName(path);
        ApplyTextStyle(FileLabel, "AppInkText");
        Notice.Text = string.Empty;
    }

    internal void SetCheckFile(string path)
    {
        _checkFile = path;
        CheckFileLabel.Text = Path.GetFileName(path);
        ApplyTextStyle(CheckFileLabel, "AppInkText");
        CheckStatus.Text = string.Empty;
        CheckResult.Text = string.Empty;
        CheckProgress.Value = 0;
        Notice.Text = string.Empty;
    }

    private void OnCheckProgress(int done, int total, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CheckProgress.Maximum = Math.Max(total, 1);
            CheckProgress.Value = done;
            CheckStatus.Text = message;
        });
    }

    private async Task<string?> PickDatabaseFile(string title)
    {
        Window? host = App.Current.HostWindow;
        if (host is null)
        {
            return null;
        }

        var picker = new FileOpenPicker(host.AppWindow.Id)
        {
            Title = title,
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

    private async Task<bool> ConfirmRestore()
    {
        var dialog = new ContentDialog
        {
            Title = AppMessages.BackupConfirmTitle,
            Content = AppMessages.BackupRestoreWarning,
            PrimaryButtonText = AppMessages.BackupRestoreAction,
            CloseButtonText = AppMessages.BackupCancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            FlowDirection = FlowDirection.RightToLeft,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
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
