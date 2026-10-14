using Atten.App.Backup;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Settings;

public sealed partial class AdvancedSettingsForm : UserControl
{
    public AdvancedSettingsForm()
    {
        InitializeComponent();
        Heading.Text = AppMessages.AdvancedSettings;
        HolidaysButton.Content = AppMessages.OfficialHolidayManage;
        KeepHeading.Text = AppMessages.SnapshotKeepTitle;
        KeepHint.Text = AppMessages.SnapshotKeepHint;
        KeepSaveButton.Content = AppMessages.SnapshotKeepSave;
        KeepBox.Text = AttenStore.SnapshotKeep.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PreviewHeading.Text = AppMessages.RecoveryPreviewSection;
        PreviewHint.Text = AppMessages.RecoveryPreviewSectionHint;
        PreviewWithSnapshotsButton.Content = AppMessages.RecoveryPreviewWithSnapshots;
        PreviewWithoutSnapshotsButton.Content = AppMessages.RecoveryPreviewWithoutSnapshots;
        HolidaysButton.Loaded += OnStretchButtonLoaded;
        PreviewWithSnapshotsButton.Loaded += OnStretchButtonLoaded;
        PreviewWithoutSnapshotsButton.Loaded += OnStretchButtonLoaded;
    }

    public event EventHandler? HolidaysRequested;

    public event EventHandler? PreviewRecoveryWithSnapshotsRequested;

    public event EventHandler? PreviewRecoveryWithoutSnapshotsRequested;

    public event EventHandler? Cancelled;

    private static void OnStretchButtonLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not AppButton button)
        {
            return;
        }

        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private void OnHolidays(object sender, RoutedEventArgs args)
    {
        HolidaysRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeepChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !PersonnelHours.AcceptsHours(args.NewText);
    }

    private void OnSaveKeep(object sender, RoutedEventArgs args)
    {
        int? parsed = PersonnelHours.WholeNumber(KeepBox.Text ?? string.Empty);
        if (parsed is null
            || parsed < DatabaseBackup.MinKeepSnapshots
            || parsed > DatabaseBackup.MaxKeepSnapshots)
        {
            ShowKeepNotice(AppMessages.SnapshotKeepInvalid, danger: true);
            return;
        }

        int keep = parsed.Value;
        IBackupService backup = App.Current.Store?.Backup ?? AttenStore.Backup();
        backup.ApplySnapshotKeep(keep);
        BackupPreferences.Save(keep);
        KeepBox.Text = keep.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ShowKeepNotice(AppMessages.SnapshotKeepSaved, danger: false);
    }

    private void OnPreviewWithSnapshots(object sender, RoutedEventArgs args)
    {
        PreviewRecoveryWithSnapshotsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewWithoutSnapshots(object sender, RoutedEventArgs args)
    {
        PreviewRecoveryWithoutSnapshotsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void ShowKeepNotice(string message, bool danger)
    {
        KeepNotice.Text = message;
        if (Application.Current.Resources.TryGetValue(danger ? "AppDangerText" : "AppSuccessText", out object? resource)
            && resource is Style style)
        {
            KeepNotice.Style = style;
        }
    }
}
