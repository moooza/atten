using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Atten.UI.Theme;

/// <summary>
/// Light and dark dictionaries with the same token names.
/// The user's choice is local settings, not domain logic.
/// </summary>
public static class Theme
{
    public const string SettingKey = "theme";
    public const string IconFontFamilyKey = "AppIconFontFamily";
    public const string IconFontFamilyName =
        "ms-appx:///Atten.UI.Theme/Fonts/SegoeFluentIcons.ttf#Segoe Fluent Icons";

    private const uint FrPrivate = 0x10;

    private static UISettings? _uiSettings;
    private static WeakReference<FrameworkElement>? _appliedRoot;
    private static FontFamily? _iconFontFamily;

    public static ResourceDictionary Light { get; } = new Light();

    public static ResourceDictionary Dark { get; } = new Dark();

    public static AppTheme Preference { get; private set; } = AppTheme.System;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static FontFamily IconFontFamily =>
        _iconFontFamily ??= ResolveIconFontFamily();

    public static event EventHandler? Changed;

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

    /// <summary>
    /// Loads Segoe Fluent Icons into this process. Windows 10 does not ship that font.
    /// </summary>
    internal static void RegisterIconFont()
    {
        foreach (string path in IconFontPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                if (AddFontResourceEx(path, FrPrivate, IntPtr.Zero) > 0)
                {
                    return;
                }
            }
            catch (DllNotFoundException)
            {
                return;
            }
            catch (EntryPointNotFoundException)
            {
                return;
            }
        }
    }

    public static AppTheme Load()
    {
        AppTheme preference = AppTheme.System;
        try
        {
            object? stored = ApplicationData.Current.LocalSettings.Values[SettingKey];
            if (stored is string name && Enum.TryParse(name, ignoreCase: true, out AppTheme parsed))
            {
                preference = parsed;
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

        Preference = preference;
        Current = Resolve(preference);
        return preference;
    }

    public static AppTheme Resolve(AppTheme preference)
    {
        if (preference == AppTheme.System)
        {
            return IsSystemDark() ? AppTheme.Dark : AppTheme.Light;
        }

        return preference == AppTheme.Dark ? AppTheme.Dark : AppTheme.Light;
    }

    public static void ApplySaved(FrameworkElement root)
    {
        Apply(root, Load());
    }

    public static void Apply(FrameworkElement root, AppTheme preference)
    {
        ArgumentNullException.ThrowIfNull(root);
        Preference = preference;
        Current = Resolve(preference);
        root.RequestedTheme = Current == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        WatchSystem(root);
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = preference.ToString();
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

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static Color TokenColor(string colorKey)
    {
        return TokenColor(Current == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light, colorKey);
    }

    public static Color TokenColor(ElementTheme theme, string colorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colorKey);
        ResourceDictionary dictionary = DictionaryFor(theme);
        if (dictionary.TryGetValue(colorKey, out object? value) && value is Color color)
        {
            return color;
        }

        throw new KeyNotFoundException(colorKey);
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

    public static bool IsSystemDark()
    {
        try
        {
            Color background = new UISettings().GetColorValue(UIColorType.Background);
            return background.R + background.G + background.B < 384;
        }
        catch (COMException)
        {
            return Application.Current?.RequestedTheme == ApplicationTheme.Dark;
        }
    }

    private static void WatchSystem(FrameworkElement root)
    {
        _appliedRoot = new WeakReference<FrameworkElement>(root);
        if (_uiSettings is not null)
        {
            return;
        }

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;
    }

    private static FontFamily ResolveIconFontFamily()
    {
        if (Application.Current?.Resources.TryGetValue(IconFontFamilyKey, out object? value) == true
            && value is FontFamily family)
        {
            return family;
        }

        return new FontFamily($"{IconFontFamilyName}, Segoe MDL2 Assets");
    }

    private static IEnumerable<string> IconFontPaths()
    {
        string root = AppContext.BaseDirectory;
        yield return Path.Combine(root, "Fonts", "SegoeFluentIcons.ttf");
        yield return Path.Combine(root, "Atten.UI.Theme", "Fonts", "SegoeFluentIcons.ttf");
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int AddFontResourceEx(string name, uint fl, IntPtr res);

    private static void OnSystemColorsChanged(UISettings sender, object args)
    {
        if (Preference != AppTheme.System)
        {
            return;
        }

        if (_appliedRoot is null || !_appliedRoot.TryGetTarget(out FrameworkElement? root))
        {
            return;
        }

        root.DispatcherQueue.TryEnqueue(() =>
        {
            if (Preference == AppTheme.System)
            {
                Apply(root, AppTheme.System);
            }
        });
    }
}
