using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>
/// Windows 11 window material: rounded corners, Mica backdrop and dark title-bar hint through DWM.
/// On older systems (or high contrast) nothing changes and the opaque gradient background is used.
/// </summary>
public static class Backdrop
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Mica needs Windows 11 22H2 (build 22621) or later.</summary>
    public static bool MicaSupported => Environment.OSVersion.Version.Build >= 22621 && !SystemParameters.HighContrast;

    public static bool RoundCornersSupported => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Call from SourceInitialized. Returns true when Mica is active (the window background must stay translucent).</summary>
    public static bool Apply(Window window, FrameworkElement root)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            if (RoundCornersSupported)
            {
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }
            int dark = ThemeManager.IsDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            bool mica = MicaSupported;
            var chrome = WindowChrome.GetWindowChrome(window);
            if (chrome != null) chrome.GlassFrameThickness = mica ? new Thickness(-1) : new Thickness(0);
            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
                target.BackgroundColor = mica ? Colors.Transparent : SystemColors.WindowColor;
            int type = mica ? DWMSBT_MAINWINDOW : 0;
            if (Environment.OSVersion.Version.Build >= 22621)
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int));
            window.Background = mica ? Brushes.Transparent : (Brush)window.FindResource("WindowBg");
            root.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, mica ? "WindowBgMica" : "WindowBg");
            return mica;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            return false;
        }
    }
}
