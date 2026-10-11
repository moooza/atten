using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

public sealed class AppConfirmLine
{
    public AppConfirmLine(string label, string value)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
    }

    public string Label { get; }

    public string Value { get; }
}

public sealed class AppConfirmRequest
{
    public required string Title { get; init; }

    public required string Ask { get; init; }

    public IReadOnlyList<AppConfirmLine> Lines { get; init; } = [];

    public required string ConfirmText { get; init; }

    public string CancelText { get; init; } = "انصراف";

    public AppButtonKind ConfirmKind { get; init; } = AppButtonKind.Danger;
}

/// <summary>
/// Themed confirm popup used by every list delete. Screens pass the words.
/// </summary>
public static class AppConfirmDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, ElementTheme theme, AppConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(request);

        Brush accent = Theme.DictionaryFor(theme).TryGetValue(Tokens.Accent, out object? value) && value is Brush brush
            ? brush
            : new SolidColorBrush();
        var heading = StyledBlock(request.Title, "AppTitleText");
        var ask = StyledBlock(request.Ask, "AppInkText");
        var cancel = new AppButton
        {
            Content = request.CancelText,
            Kind = AppButtonKind.Primary,
        };
        var confirm = new AppButton
        {
            Content = request.ConfirmText,
            Kind = request.ConfirmKind,
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);

        var body = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FlowDirection = FlowDirection.LeftToRight,
        };
        body.Children.Add(ask);
        foreach (AppConfirmLine line in request.Lines)
        {
            body.Children.Add(HighlightedLine(line, accent));
        }

        body.Children.Add(actions);

        var dialog = new ContentDialog
        {
            Title = heading,
            Content = body,
            XamlRoot = xamlRoot,
            FlowDirection = FlowDirection.RightToLeft,
            RequestedTheme = theme,
        };
        bool confirmed = false;
        cancel.Click += (_, _) => dialog.Hide();
        confirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Hide();
        };
        await dialog.ShowAsync();
        return confirmed;
    }

    private static TextBlock HighlightedLine(AppConfirmLine line, Brush accent)
    {
        var block = StyledBlock(string.Empty, "AppInkText");
        block.TextWrapping = TextWrapping.Wrap;
        if (line.Label.Length > 0)
        {
            block.Inlines.Add(new Run { Text = line.Label });
        }

        block.Inlines.Add(new Run { Text = line.Value, Foreground = accent });
        return block;
    }

    private static TextBlock StyledBlock(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FlowDirection = FlowDirection.LeftToRight,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (Application.Current?.Resources.TryGetValue(key, out object? resource) == true
            && resource is Style style)
        {
            block.Style = style;
        }

        return block;
    }
}
