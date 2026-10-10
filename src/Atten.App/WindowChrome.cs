using System.Runtime.InteropServices;
using Atten.UI.Theme;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinRT.Interop;

namespace Atten.App;

/// <summary>
/// Paints the native title bar, caption buttons, and window border from theme tokens.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Color surface = Theme.TokenColor(Tokens.SurfaceColor);
        Color ink = Theme.TokenColor(Tokens.InkColor);
        Color muted = Theme.TokenColor(Tokens.MutedColor);
        Color line = Theme.TokenColor(Tokens.LineColor);
        bool dark = Theme.Current == AppTheme.Dark;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindowTitleBar bar = window.AppWindow.TitleBar;
            bar.BackgroundColor = surface;
            bar.ForegroundColor = ink;
            bar.InactiveBackgroundColor = surface;
            bar.InactiveForegroundColor = muted;
            bar.ButtonBackgroundColor = surface;
            bar.ButtonForegroundColor = ink;
            bar.ButtonHoverBackgroundColor = line;
            bar.ButtonHoverForegroundColor = ink;
            bar.ButtonPressedBackgroundColor = line;
            bar.ButtonPressedForegroundColor = ink;
            bar.ButtonInactiveBackgroundColor = surface;
            bar.ButtonInactiveForegroundColor = muted;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        int darkMode = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        int border = ToColorRef(line);
        int caption = ToColorRef(surface);
        int text = ToColorRef(ink);
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
