using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

internal static class ThemeLooks
{
    public static void Apply(FrameworkElement scope, string key)
    {
        if (TryFindStyle(scope, key, out Style? style) && style is not null)
        {
            scope.Style = style;
        }
    }

    public static bool TryFindStyle(FrameworkElement scope, string key, out Style? style)
    {
        if (scope.Resources.TryGetValue(key, out object? local) && local is Style localStyle)
        {
            style = localStyle;
            return true;
        }

        if (Application.Current?.Resources.TryGetValue(key, out object? app) == true && app is Style appStyle)
        {
            style = appStyle;
            return true;
        }

        style = null;
        return false;
    }
}
