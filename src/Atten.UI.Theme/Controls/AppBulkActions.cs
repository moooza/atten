using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

public enum AppBulkKind
{
    Custom,
    Delete,
    Deactivate,
}

/// <summary>
/// One command in the bulk-actions popup. Delete and deactivate are shared;
/// a list can also pass its own commands.
/// </summary>
public sealed class AppBulkCommand
{
    public const string DeleteKey = "delete";
    public const string DeactivateKey = "deactivate";

    private AppBulkCommand(string key, string text, AppButtonKind kind, AppBulkKind role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Key = key;
        Text = text;
        Kind = kind;
        Role = role;
    }

    public string Key { get; }

    public string Text { get; }

    public AppButtonKind Kind { get; }

    public AppBulkKind Role { get; }

    public static AppBulkCommand Delete(string? text = null)
    {
        return new AppBulkCommand(DeleteKey, text ?? "حذف انتخاب‌شده‌ها", AppButtonKind.Danger, AppBulkKind.Delete);
    }

    public static AppBulkCommand Deactivate(string? text = null)
    {
        return new AppBulkCommand(
            DeactivateKey,
            text ?? "غیرفعال کردن انتخاب‌شده‌ها",
            AppButtonKind.Secondary,
            AppBulkKind.Deactivate);
    }

    public static AppBulkCommand Custom(string key, string text, AppButtonKind kind = AppButtonKind.Secondary)
    {
        return new AppBulkCommand(key, text, kind, AppBulkKind.Custom);
    }
}

/// <summary>
/// Shared bulk-action dropdown for any list. Screens bind commands and handle the chosen key.
/// </summary>
public sealed class AppBulkActions : UserControl
{
    public const string DefaultCaption = "اقدامات دسته جمعی";

    private readonly AppDropdownButton _dropdown = new() { Caption = DefaultCaption };
    private readonly StackPanel _list = new();
    private IReadOnlyList<AppBulkCommand> _commands = [];

    public AppBulkActions()
    {
        FlowDirection = FlowDirection.RightToLeft;
        _list.Spacing = 8;
        _dropdown.Menu = _list;
        _dropdown.Opening += (_, _) => Rebuild();
        Content = _dropdown;
    }

    public event EventHandler<AppBulkCommand>? CommandChosen;

    public string Caption
    {
        get => _dropdown.Caption;
        set => _dropdown.Caption = string.IsNullOrWhiteSpace(value) ? DefaultCaption : value;
    }

    public void Bind(IEnumerable<AppBulkCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands.ToList();
        Rebuild();
    }

    private void Rebuild()
    {
        _list.Children.Clear();
        foreach (AppBulkCommand command in _commands)
        {
            var button = new AppButton
            {
                Content = command.Text,
                Kind = command.Kind,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Tag = command,
            };
            button.Click += OnCommand;
            _list.Children.Add(button);
        }
    }

    private void OnCommand(object sender, RoutedEventArgs args)
    {
        if (sender is not AppButton { Tag: AppBulkCommand command })
        {
            return;
        }

        _dropdown.Hide();
        CommandChosen?.Invoke(this, command);
    }
}
