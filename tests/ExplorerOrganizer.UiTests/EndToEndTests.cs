using System.Diagnostics;
using System.Windows.Automation;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.UiTests;

/// <summary>
/// Launches the real ExplorerOrganizer.exe (isolated data folder via EXPLORER_ORGANIZER_DATA) and drives it through
/// Windows UI Automation, like a user or a screen reader would.
/// </summary>
[Collection("wpf")] // never in parallel with the in-process tests (focus, tray)
public sealed class EndToEndTests : IDisposable
{
    private readonly List<Process> _processes = new();
    private readonly List<string> _dirs = new();

    private string NewData(string tag, string language = "it", ThemeMode theme = ThemeMode.Light, bool preview = false)
    {
        var dir = TestData.NewDataDir(tag);
        _dirs.Add(dir);
        TestData.Seed(dir, language, theme, preview);
        return dir;
    }

    private Process Launch(string dataDir, params string[] args)
    {
        Assert.True(File.Exists(TestData.ExePath), "Build the app first: " + TestData.ExePath);
        var psi = new ProcessStartInfo(TestData.ExePath) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment[AppPaths.DataDirectoryVariable] = dataDir;
        var p = Process.Start(psi)!;
        _processes.Add(p);
        return p;
    }

    private static AutomationElement WaitForMainWindow(Process p, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "MainWindow"));
        while (DateTime.UtcNow < deadline)
        {
            Assert.False(p.HasExited, "process exited with code " + (p.HasExited ? p.ExitCode : 0));
            var w = AutomationElement.RootElement.FindFirst(TreeScope.Children, cond);
            if (w != null) return w;
            Thread.Sleep(250);
        }
        throw new TimeoutException("Main window did not appear");
    }

    private static AutomationElement WaitFor(AutomationElement root, string automationId, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        while (DateTime.UtcNow < deadline)
        {
            var e = root.FindFirst(TreeScope.Descendants, cond);
            if (e != null) return e;
            Thread.Sleep(150);
        }
        throw new TimeoutException("Element not found: " + automationId);
    }

    private static void SaveCapture(Process p, string name)
    {
        p.Refresh();
        using var bmp = WindowCapture.Capture(p.MainWindowHandle);
        bmp.Save(Path.Combine(TestData.ScreenshotDir, name), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void WaitUntil(Func<bool> condition, int seconds, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException(what);
            Thread.Sleep(200);
        }
    }

    [Fact]
    public void Startup_shows_main_window_on_home()
    {
        var p = Launch(NewData("start"), "--show");
        var w = WaitForMainWindow(p);
        Assert.Equal("Explorer Organizer", w.Current.Name);
        WaitFor(w, "Page_home");
        WaitFor(w, "OrganizeNowButton");
        Thread.Sleep(1500); // async counters
        SaveCapture(p, "home-light.png");
    }

    [Fact]
    public void Navigation_through_the_rail_with_ui_automation()
    {
        var p = Launch(NewData("nav"), "--show");
        var w = WaitForMainWindow(p);
        foreach (var key in App.MainWindow.PageKeys)
        {
            var item = WaitFor(w, "Nav_" + key);
            ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            WaitFor(w, "Page_" + key);
            Assert.True(((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected);
            Thread.Sleep(key == "home" ? 1200 : 400);
            SaveCapture(p, $"page-{key}-light.png");
        }
    }

    [Fact]
    public void Dark_theme_start_renders_dark_and_light_renders_light()
    {
        var light = Launch(NewData("light", theme: ThemeMode.Light), "--show");
        WaitFor(WaitForMainWindow(light), "Page_home");
        Thread.Sleep(1200);
        light.Refresh();
        double lumLight;
        using (var bmp = WindowCapture.Capture(light.MainWindowHandle)) lumLight = WindowCapture.AverageLuminance(bmp);
        light.Kill();

        var dark = Launch(NewData("dark", theme: ThemeMode.Dark), "--show");
        WaitFor(WaitForMainWindow(dark), "Page_home");
        Thread.Sleep(1200);
        dark.Refresh();
        double lumDark;
        using (var bmp = WindowCapture.Capture(dark.MainWindowHandle)) lumDark = WindowCapture.AverageLuminance(bmp);
        SaveCapture(dark, "home-dark.png");

        Assert.True(lumLight > 0.6, $"light theme luminance {lumLight:0.00}");
        Assert.True(lumDark < 0.35, $"dark theme luminance {lumDark:0.00}");
    }

    [Fact]
    public void English_ui_when_language_is_en()
    {
        var p = Launch(NewData("en", language: "en"), "--show");
        var w = WaitForMainWindow(p);
        Assert.Equal("Rules", WaitFor(w, "Nav_rules").Current.Name);
        Assert.Equal("Organize now", WaitFor(w, "OrganizeNowButton").Current.Name);
    }

    [Fact]
    public void Minimized_start_stays_in_tray_and_second_launch_shows_the_window()
    {
        var data = NewData("tray");
        var first = Launch(data, "--minimized");
        Thread.Sleep(4000);
        Assert.False(first.HasExited);
        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, first.Id),
            new PropertyCondition(AutomationElement.AutomationIdProperty, "MainWindow"));
        Assert.Null(AutomationElement.RootElement.FindFirst(TreeScope.Children, cond)); // tray only

        var second = Launch(data, "--show"); // single instance: forwards and exits
        Assert.True(second.WaitForExit(15000), "second instance did not exit");
        WaitForMainWindow(first);
    }

    [Fact]
    public void Explorer_context_menu_command_organizes_the_selected_file()
    {
        var data = NewData("ctx");
        var demo = Path.Combine(data, "demo-downloads");
        var first = Launch(data, "--minimized");
        Thread.Sleep(2500);

        // Exactly what Explorer runs for "Organize with Explorer Organizer" on note.txt
        var command = ShellIntegration.MenuCommand(TestData.ExePath, background: false).Replace("%1", Path.Combine(demo, "note.txt"));
        var parts = System.Text.RegularExpressions.Regex.Matches(command, "\"([^\"]*)\"|(\\S+)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToArray();
        Assert.Equal(TestData.ExePath, parts[0]);
        var second = Launch(data, parts.Skip(1).ToArray());
        Assert.True(second.WaitForExit(15000));

        WaitUntil(() => File.Exists(Path.Combine(demo, "Sorted", "note.txt")), 20, "file not organized from the context menu");
        Assert.False(File.Exists(Path.Combine(demo, "note.txt")));
        Assert.False(first.HasExited);
    }

    [Fact]
    public void Scan_from_command_line_organizes_monitored_folder()
    {
        var data = NewData("scan");
        var demo = Path.Combine(data, "demo-downloads");
        Launch(data, "--minimized", "--scan");
        WaitUntil(() => File.Exists(Path.Combine(demo, "Sorted", "note.txt")), 25, "scan did not run");
        WaitUntil(() => File.Exists(Path.Combine(demo, "Immagini", "scansione.png")), 10, "images not organized");
        Assert.True(File.Exists(Path.Combine(demo, "video.mp4")), "disabled rule must not run");
        // the journal is saved once the whole run is over, a moment after the last file has moved
        WaitUntil(() => File.Exists(Path.Combine(data, "undo-journal.json")), 10, "undo journal not written");
    }

    public void Dispose()
    {
        foreach (var p in _processes)
        {
            try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5000); } } catch { }
            p.Dispose();
        }
        foreach (var d in _dirs) TestData.TryDelete(d);
    }
}
