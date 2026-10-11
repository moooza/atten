using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;

namespace Atten.App;

/// <summary>
/// Last main-window position. Local settings, not domain data.
/// A missing monitor falls back to the primary display.
/// </summary>
public static class WindowPlacement
{
    public const int DefaultWidth = 960;
    public const int DefaultHeight = 560;
    public const int MinWidth = 760;
    public const int MinHeight = 480;

    private const string XKey = "window.x";
    private const string YKey = "window.y";
    private const string WidthKey = "window.width";
    private const string HeightKey = "window.height";

    public static void Apply(AppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (TryRead(out PointInt32 origin, out SizeInt32 size)
            && DisplayFor(origin) is DisplayArea display)
        {
            Place(window, display.WorkArea, origin, size);
            return;
        }

        if (TryRead(out _, out _) && DisplayArea.Primary is DisplayArea primary)
        {
            Place(window, primary.WorkArea, origin: null, new SizeInt32(DefaultWidth, DefaultHeight));
            return;
        }

        window.Resize(new SizeInt32(DefaultWidth, DefaultHeight));
    }

    public static void Save(AppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        try
        {
            ApplicationData.Current.LocalSettings.Values[XKey] = window.Position.X;
            ApplicationData.Current.LocalSettings.Values[YKey] = window.Position.Y;
            ApplicationData.Current.LocalSettings.Values[WidthKey] = window.Size.Width;
            ApplicationData.Current.LocalSettings.Values[HeightKey] = window.Size.Height;
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

    private static void Place(AppWindow window, RectInt32 work, PointInt32? origin, SizeInt32 size)
    {
        int width = Clamp(size.Width, MinWidth, Math.Max(MinWidth, work.Width));
        int height = Clamp(size.Height, MinHeight, Math.Max(MinHeight, work.Height));
        int x = origin is { } point
            ? Clamp(point.X, work.X, work.X + Math.Max(0, work.Width - width))
            : work.X + Math.Max(0, (work.Width - width) / 2);
        int y = origin is { } placed
            ? Clamp(placed.Y, work.Y, work.Y + Math.Max(0, work.Height - height))
            : work.Y + Math.Max(0, (work.Height - height) / 2);
        window.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private static DisplayArea? DisplayFor(PointInt32 point)
    {
        foreach (DisplayArea area in DisplayArea.FindAll())
        {
            if (Contains(area.WorkArea, point) || Contains(area.OuterBounds, point))
            {
                return area;
            }
        }

        return null;
    }

    private static bool Contains(RectInt32 area, PointInt32 point)
    {
        return point.X >= area.X
            && point.Y >= area.Y
            && point.X < area.X + area.Width
            && point.Y < area.Y + area.Height;
    }

    private static bool TryRead(out PointInt32 origin, out SizeInt32 size)
    {
        origin = default;
        size = default;
        try
        {
            IPropertySet values = ApplicationData.Current.LocalSettings.Values;
            if (ReadInt(values, XKey) is int x
                && ReadInt(values, YKey) is int y
                && ReadInt(values, WidthKey) is int width
                && ReadInt(values, HeightKey) is int height
                && width > 0
                && height > 0)
            {
                origin = new PointInt32(x, y);
                size = new SizeInt32(width, height);
                return true;
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

        return false;
    }

    private static int? ReadInt(IPropertySet values, string key)
    {
        return values[key] switch
        {
            int number => number,
            long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
            _ => null,
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(max, Math.Max(min, value));
    }
}
