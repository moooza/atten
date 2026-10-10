using System.Runtime.InteropServices;
using Atten.UI.Theme;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace Atten.App;

/// <summary>
/// Hosts a feature form in its own window with the same theme and chrome as the shell.
/// </summary>
public sealed class FormWindow : Window
{
    private const int GwlpHwndParent = -8;

    public FormWindow(string title, FrameworkElement body, SizeInt32 size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(body);

        Title = title;
        body.HorizontalAlignment = HorizontalAlignment.Stretch;
        body.VerticalAlignment = VerticalAlignment.Stretch;
        var root = new Grid { FlowDirection = FlowDirection.LeftToRight };
        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        if (Application.Current.Resources.TryGetValue("PagePadding", out object? padding)
            && padding is Thickness thickness)
        {
            scroll.Padding = thickness;
        }

        scroll.SizeChanged += (_, _) =>
        {
            double inner = scroll.ActualHeight - scroll.Padding.Top - scroll.Padding.Bottom;
            body.MinHeight = Math.Max(0, inner);
        };
        root.Children.Add(scroll);
        Content = root;
        Theme.Apply(root, Theme.Preference);
        root.Background = Theme.Brush(root, Tokens.Surface);

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        AppWindow.Resize(size);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 420;
            presenter.PreferredMinimumHeight = 480;
            presenter.IsMaximizable = false;
        }

        CenterOnHost();
        OwnHost();
        WindowChrome.Apply(this);
        Theme.Changed += OnThemeChanged;
        Closed += (_, _) => Theme.Changed -= OnThemeChanged;
    }

    public void BringForward()
    {
        OwnHost();
        Activate();
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Activate);
    }

    private void OnThemeChanged(object? sender, EventArgs args)
    {
        if (Content is not Grid root)
        {
            return;
        }

        root.RequestedTheme = Theme.Current == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        root.Background = Theme.Brush(root, Tokens.Surface);
        WindowChrome.Apply(this);
    }

    private void CenterOnHost()
    {
        if (App.Current.HostWindow is not Window host)
        {
            return;
        }

        SizeInt32 size = AppWindow.Size;
        PointInt32 origin = host.AppWindow.Position;
        SizeInt32 hostSize = host.AppWindow.Size;
        AppWindow.Move(new PointInt32(
            origin.X + Math.Max(0, (hostSize.Width - size.Width) / 2),
            origin.Y + Math.Max(0, (hostSize.Height - size.Height) / 2)));
    }

    private void OwnHost()
    {
        if (App.Current.HostWindow is not Window host)
        {
            return;
        }

        SetOwner(WindowNative.GetWindowHandle(this), WindowNative.GetWindowHandle(host));
    }

    private static void SetOwner(IntPtr hwnd, IntPtr owner)
    {
        if (hwnd == IntPtr.Zero || owner == IntPtr.Zero)
        {
            return;
        }

        if (IntPtr.Size == 8)
        {
            _ = SetWindowLongPtr(hwnd, GwlpHwndParent, owner);
            return;
        }

        _ = SetWindowLong(hwnd, GwlpHwndParent, owner.ToInt32());
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
