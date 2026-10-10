using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;

namespace Atten.UI.Theme;

/// <summary>
/// Light and dark dictionaries with the same token names.
/// The user's choice is local settings, not domain logic.
/// </summary>
public static class Theme
{
    public const string SettingKey = "theme";

    public static ResourceDictionary Light { get; } = new Light();

    public static ResourceDictionary Dark { get; } = new Dark();

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Install(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);
        foreach (ResourceDictionary dictionary in app.Resources.MergedDictionaries)
        {
            if (dictionary is ThemeResources)
            {
                return;
            }
        }

        app.Resources.MergedDictionaries.Add(new ThemeResources());
    }

    public static AppTheme Load()
    {
        AppTheme theme = AppTheme.Light;
        try
        {
            object? stored = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (stored is string name && Enum.TryParse(name, ignoreCase: true, out AppTheme parsed))
            {
                theme = parsed;
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

        Current = theme;
        return theme;
    }

    public static void ApplySaved(FrameworkElement root)
    {
        Apply(root, Load());
    }

    public static void Apply(FrameworkElement root, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        Current = theme;
        root.RequestedTheme = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = theme.ToString();
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

    public static Brush Brush(FrameworkElement scope, string token)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ResourceDictionary dictionary = DictionaryFor(scope.ActualTheme);
        if (dictionary.TryGetValue(token, out object? value) && value is Brush brush)
        {
            return brush;
        }

        throw new KeyNotFoundException(token);
    }

    public static ResourceDictionary DictionaryFor(ElementTheme theme)
    {
        if (theme == ElementTheme.Default)
        {
            theme = Application.Current?.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        return theme == ElementTheme.Dark ? Dark : Light;
    }
}
