using ExplorerOrganizer.Core.Localization;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>Per-user Explorer context menu and "start with Windows" (HKCU only: no administrator rights needed).</summary>
public static class ShellIntegration
{
    private const string VerbName = "ExplorerOrganizer";

    /// <summary>HKCU base keys. The automated tests point them at a private scratch key so the user's real entries are never touched.</summary>
    internal static string ClassesRoot { get; set; } = @"Software\Classes";
    internal static string RunKey { get; set; } = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string[] MenuRoots =>
    [
        ClassesRoot + @"\*\shell\" + VerbName,
        ClassesRoot + @"\Directory\shell\" + VerbName,
        ClassesRoot + @"\Directory\Background\shell\" + VerbName,
    ];

    public static string ExePath => Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "ExplorerOrganizer.exe");

    /// <summary>Command line registered for the Explorer verb (%1 = selected item, %V = folder background).</summary>
    public static string MenuCommand(string exePath, bool background) => $"\"{exePath}\" --process \"{(background ? "%V" : "%1")}\"";

    /// <summary>Command line registered under the Run key.</summary>
    public static string AutostartCommand(string exePath) => $"\"{exePath}\" --minimized";

    public static bool IsContextMenuRegistered()
    {
        using var k = Registry.CurrentUser.OpenSubKey(MenuRoots[0]);
        return k != null;
    }

    public static void RegisterContextMenu()
    {
        foreach (var root in MenuRoots)
        {
            bool background = root.Contains(@"\Background\", StringComparison.Ordinal);
            using var verb = Registry.CurrentUser.CreateSubKey(root);
            verb.SetValue("MUIVerb", Loc.T(background ? "Shell.OrganizeHere" : "Shell.Organize"));
            verb.SetValue("Icon", $"\"{ExePath}\",0");
            using var cmd = verb.CreateSubKey("command");
            cmd.SetValue("", MenuCommand(ExePath, background));
        }
    }

    public static void UnregisterContextMenu()
    {
        foreach (var root in MenuRoots)
            Registry.CurrentUser.DeleteSubKeyTree(root, throwOnMissingSubKey: false);
    }

    public static bool IsAutostartEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(VerbName) != null;
    }

    public static void SetAutostart(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) k.SetValue(VerbName, AutostartCommand(ExePath));
        else k.DeleteValue(VerbName, throwOnMissingValue: false);
    }
}
