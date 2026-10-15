using System.Windows;
using System.Windows.Media;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>
/// Light / dark / Windows high-contrast palettes, applied as application-level brushes (DynamicResource).
/// Light/dark text pairs are chosen for WCAG AA contrast (normal text ≥ 4.5:1, focus indicators ≥ 3:1).
/// </summary>
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    /// <summary>Raised after a palette change (windows refresh their DWM backdrop).</summary>
    public static event EventHandler? Applied;

    public static void Apply(ThemeMode mode)
    {
        var res = Application.Current.Resources;
        if (SystemParameters.HighContrast)
        {
            // Respect the user's high-contrast colours instead of our palette (accessibility).
            Set(res, "Bg", SystemColors.WindowColor);
            Set(res, "BgTop", SystemColors.WindowColor);
            Set(res, "BgBottom", SystemColors.WindowColor);
            Set(res, "Surface", SystemColors.WindowColor);
            Set(res, "SurfaceAlt", SystemColors.ControlColor);
            Set(res, "Border", SystemColors.WindowTextColor);
            Set(res, "CardBorder", SystemColors.WindowTextColor);
            Set(res, "Text", SystemColors.WindowTextColor);
            Set(res, "TextMuted", SystemColors.GrayTextColor);
            Set(res, "Accent", SystemColors.HighlightColor);
            Set(res, "AccentText", SystemColors.HighlightTextColor);
            Set(res, "AccentSoft", SystemColors.ControlColor);
            Set(res, "Link", SystemColors.HotTrackColor);
            Set(res, "Focus", SystemColors.HighlightColor);
            Set(res, "Hover", SystemColors.ControlLightColor);
            Set(res, "BarTrack", SystemColors.ControlColor);
            Set(res, "BarFill", SystemColors.HighlightColor);
            Set(res, "Success", SystemColors.WindowTextColor);
            Set(res, "Warning", SystemColors.WindowTextColor);
            Set(res, "Danger", SystemColors.WindowTextColor);
            Set(res, "IfBlock", SystemColors.HighlightColor);
            Set(res, "IfBlockText", SystemColors.HighlightTextColor);
            Set(res, "ThenBlock", SystemColors.HighlightColor);
            Set(res, "ThenBlockText", SystemColors.HighlightTextColor);
            Set(res, "IllustrationBack", SystemColors.ControlColor);
            Set(res, "IllustrationFront", SystemColors.HighlightColor);
            Set(res, "IllustrationSoft", SystemColors.WindowColor);
            SetGradients(res, SystemColors.WindowColor, SystemColors.WindowColor);
            IsDark = false;
            Applied?.Invoke(null, EventArgs.Empty);
            return;
        }

        IsDark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemPrefersDark());
        if (IsDark)
        {
            Set(res, "Bg", 0x1C2029);
            Set(res, "BgTop", 0x202633);
            Set(res, "BgBottom", 0x191D25);
            Set(res, "Surface", 0x272C36);
            Set(res, "SurfaceAlt", 0x2F3542);
            Set(res, "Border", 0x434B5B);
            Set(res, "CardBorder", 0x343A47);
            Set(res, "Text", 0xEEF1F5);       // 13.6:1 on Surface
            Set(res, "TextMuted", 0xA9B1BE);  // 7.0:1 on Surface
            Set(res, "Accent", 0x1565C0);     // primary button: white text 5.8:1
            Set(res, "AccentText", 0xFFFFFF);
            Set(res, "AccentSoft", 0x233552);
            Set(res, "Link", 0x7DB6FF);       // 7.4:1 on Surface
            Set(res, "Focus", 0x7DB6FF);
            Set(res, "Hover", 0x333A47);
            Set(res, "BarTrack", 0x3A4252);
            Set(res, "BarFill", 0x4C94EA);
            Set(res, "Success", 0x5FD38D);
            Set(res, "Warning", 0xF5B94A);
            Set(res, "Danger", 0xFF8A80);
            Set(res, "IfBlock", 0xC11115);      // "Se" badge and bar: white text 6.4:1
            Set(res, "IfBlockText", 0xFFFFFF);
            Set(res, "ThenBlock", 0x34C38F);    // "Allora": dark text 8.6:1 (white would be only 2.3:1)
            Set(res, "ThenBlockText", 0x0B1F17);
            Set(res, "IllustrationBack", 0x2E4466);
            Set(res, "IllustrationFront", 0x4C94EA);
            Set(res, "IllustrationSoft", 0x9CC7FF);
            SetGradients(res, Rgb(0x202633), Rgb(0x191D25));
        }
        else
        {
            Set(res, "Bg", 0xEEF3FA);
            Set(res, "BgTop", 0xEDF3FC);
            Set(res, "BgBottom", 0xE4ECF8);
            Set(res, "Surface", 0xFFFFFF);
            Set(res, "SurfaceAlt", 0xF5F8FC);
            Set(res, "Border", 0xC9D3E0);
            Set(res, "CardBorder", 0xE3E9F1);
            Set(res, "Text", 0x1B1F24);       // 16:1 on white
            Set(res, "TextMuted", 0x5C6470);  // 5.9:1 on white, 5.3:1 on the background
            Set(res, "Accent", 0x1565C0);     // 5.8:1 with white text
            Set(res, "AccentText", 0xFFFFFF);
            Set(res, "AccentSoft", 0xE3EEFB);
            Set(res, "Link", 0x1565C0);
            Set(res, "Focus", 0x1565C0);
            Set(res, "Hover", 0xEAF1FB);
            Set(res, "BarTrack", 0xD8E3F3);
            Set(res, "BarFill", 0x1565C0);
            Set(res, "Success", 0x1B7F3B);
            Set(res, "Warning", 0xA35600);
            Set(res, "Danger", 0xC62828);
            Set(res, "IfBlock", 0xC11115);      // white text 6.4:1
            Set(res, "IfBlockText", 0xFFFFFF);
            Set(res, "ThenBlock", 0x0F8A5F);    // white text 4.5:1
            Set(res, "ThenBlockText", 0xFFFFFF);
            Set(res, "IllustrationBack", 0xDCEAFB);
            Set(res, "IllustrationFront", 0x1565C0);
            Set(res, "IllustrationSoft", 0x7FB2EE);
            SetGradients(res, Rgb(0xEDF3FC), Rgb(0xE4ECF8));
        }
        Applied?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>"WindowBg" is opaque; "WindowBgMica" lets the Mica material show through (Windows 11).</summary>
    private static void SetGradients(ResourceDictionary res, Color top, Color bottom)
    {
        res["WindowBg"] = Gradient(top, bottom, 255);
        res["WindowBgMica"] = Gradient(top, bottom, SystemParameters.HighContrast ? (byte)255 : (byte)0xE6);
    }

    private static Brush Gradient(Color top, Color bottom, byte alpha)
    {
        var b = new LinearGradientBrush(Color.FromArgb(alpha, top.R, top.G, top.B), Color.FromArgb(alpha, bottom.R, bottom.G, bottom.B), 90);
        b.Freeze();
        return b;
    }

    private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    private static void Set(ResourceDictionary res, string key, int rgb) => Set(res, key, Rgb(rgb));

    private static void Set(ResourceDictionary res, string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        res[key] = b;
    }
}

/// <summary>Copies the string table into application resources as "S.Key" so XAML can use {DynamicResource S.Key}.</summary>
public static class LocalizationBinder
{
    public static void Apply()
    {
        var res = Application.Current.Resources;
        foreach (var key in Loc.Keys) res["S." + key] = Loc.T(key);
    }
}
