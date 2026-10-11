using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Atten.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string target = Path.Combine(root, "app", "Atten.App.exe");
        if (!File.Exists(target))
        {
            MessageBox(
                IntPtr.Zero,
                "فایل برنامه پیدا نشد.",
                "حضور و غیاب",
                0x10);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = Path.GetDirectoryName(target),
            UseShellExecute = true,
        });
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
