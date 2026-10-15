using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ExplorerOrganizer.App;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Condition = ExplorerOrganizer.Core.Model.Condition;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ExplorerOrganizer.UiTests;

/// <summary>Isolated data folders (settings + a demo profile + a demo "Downloads" folder). Never the user's real data.</summary>
public static class TestData
{
    public static readonly string[] DemoFiles =
    {
        "foto vacanze.jpg", "scansione.png", "fattura 2026-10.pdf", "contratto.docx", "report.xlsx", "setup-tool.exe",
        "archivio.zip", "brano.mp3", "video.mp4", "note.txt", "Città di notte.jpg", "preventivo.pdf",
    };

    public static string NewDataDir(string tag) =>
        Path.Combine(Path.GetTempPath(), "eo-uitests", tag + "-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Writes settings and profiles; returns the demo folder watched by the profile.</summary>
    public static string Seed(string dataDir, string language = "it", ThemeMode theme = ThemeMode.Light, bool preview = false)
    {
        Directory.CreateDirectory(dataDir);
        var demo = Path.Combine(dataDir, "demo-downloads");
        Directory.CreateDirectory(demo);
        foreach (var f in DemoFiles) File.WriteAllText(Path.Combine(demo, f), "demo");

        JsonStore.Save(Path.Combine(dataDir, "settings.json"), new AppSettings
        {
            FirstRunCompleted = true,
            DefaultRulesSeeded = true, // the ready-made rules point to the real Downloads folder: never in tests
            Language = language,
            Theme = theme,
            StartMinimized = false,
            ShowNotifications = false,
            PreviewBeforeApply = preview,
        });

        var p = new Profile { Name = "Download" };
        p.Rules.Add(Rule("Testi in Sorted", new ExtensionCondition { Extensions = "txt" }, new MoveAction { Destination = "{folder}\\Sorted" }));
        p.Rules.Add(Rule("Immagini", new FileTypeCondition { Category = FileCategory.Image }, new MoveAction { Destination = "{folder}\\{category}" }));
        p.Rules.Add(Rule("Documenti per anno", new ExtensionCondition { Extensions = "pdf;docx;xlsx" }, new MoveAction { Destination = "{folder}\\Documenti\\{modified:yyyy}" }));
        p.Rules.Add(Rule("Archivi e installer", new FileTypeCondition { Category = FileCategory.Archive }, new SortIntoSubfoldersAction { BaseFolder = "{folder}", Mode = SortMode.ByType }));
        var media = Rule("Media per tipo", new FileTypeCondition { Category = FileCategory.Video }, new SortIntoSubfoldersAction { BaseFolder = "{folder}", Mode = SortMode.ByType });
        media.Enabled = false;
        p.Rules.Add(media);
        p.Folders.Add(new WatchedFolder { Path = demo, Trigger = TriggerMode.Manual });
        var lib = new ProfileLibrary { ActiveProfileId = p.Id };
        lib.Profiles.Add(p);
        JsonStore.Save(Path.Combine(dataDir, "profiles.json"), lib);
        return demo;
    }

    private static Rule Rule(string name, Condition c, RuleAction a)
    {
        var r = new Rule { Name = name };
        r.Condition.Children.Add(c);
        r.Actions.Add(a);
        return r;
    }

    public static void TryDelete(string dir)
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); return; }
            catch (IOException) { Thread.Sleep(300); }
            catch (UnauthorizedAccessException) { Thread.Sleep(300); }
        }
    }

    /// <summary>Where screenshots go: $EO_SCREENSHOTS_DIR, else docs\screenshots of the repository.</summary>
    public static string ScreenshotDir
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable("EO_SCREENSHOTS_DIR");
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(RepoRoot, "docs", "screenshots");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string RepoRoot
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "ExplorerOrganizer.sln"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }

    /// <summary>The app under test: $EO_EXE (e.g. the published single-file exe) or the build output of the App project.</summary>
    public static string ExePath
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("EO_EXE");
            if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
            var config = AppContext.BaseDirectory.Contains(@"\Debug\", StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";
            return Path.Combine(RepoRoot, "src", "ExplorerOrganizer.App", "bin", config, "net8.0-windows", "win-x64", "ExplorerOrganizer.exe");
        }
    }
}

/// <summary>
/// Hosts the real WPF application object (App.xaml resources, AppHost, theme, strings) on a dedicated STA thread,
/// pointed at an isolated data folder. Tests run UI code through <see cref="Invoke"/>.
/// </summary>
public sealed class WpfAppFixture : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;

    public WpfAppFixture()
    {
        DataDir = TestData.NewDataDir("inproc");
        DemoFolder = TestData.Seed(DataDir);
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, DataDir);
        var ready = new ManualResetEventSlim();
        Exception? error = null;
        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                var app = new App.App();
                app.InitializeComponent();
                Loc.Register(UiStrings.All);
                AppHost.Current = new AppHost();
                Loc.SetLanguage(AppHost.Current.Settings.Language);
                LocalizationBinder.Apply();
                ThemeManager.Apply(AppHost.Current.Settings.Theme);
            }
            catch (Exception ex) { error = ex; }
            ready.Set();
            if (error == null) Dispatcher.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
        if (error != null) throw new InvalidOperationException("WPF host failed to start", error);
    }

    public string DataDir { get; }
    public string DemoFolder { get; }

    public T Invoke<T>(Func<T> f) => _dispatcher!.Invoke(f);
    public void Invoke(Action a) => _dispatcher!.Invoke(a);

    /// <summary>Runs async UI code and waits for it (continuations run on the free dispatcher).</summary>
    public void InvokeAsync(Func<Task> f)
    {
        var task = _dispatcher!.Invoke(f);
        if (!task.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("UI task did not complete");
    }

    /// <summary>On the UI thread: processes queued work (layout, bindings, async continuations) for a while.</summary>
    public static void Pump(int milliseconds = 300)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        do
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Thread.Sleep(15);
        } while (DateTime.UtcNow < until);
    }

    public void Dispose()
    {
        try
        {
            _dispatcher?.Invoke(() =>
            {
                foreach (Window w in Application.Current.Windows.Cast<Window>().ToList()) w.Close();
                AppHost.Current?.Dispose();
                Application.Current.Shutdown();
            });
            _dispatcher?.InvokeShutdown();
        }
        catch (Exception) { /* best effort */ }
        _thread.Join(5000);
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, null);
        TestData.TryDelete(DataDir);
    }
}

[CollectionDefinition("wpf")]
public sealed class WpfCollection : ICollectionFixture<WpfAppFixture> { }

/// <summary>Captures a top-level window as composed by DWM (Mica, rounded corners included).</summary>
public static class WindowCapture
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);

    public static System.Drawing.Bitmap Capture(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        var bmp = new System.Drawing.Bitmap(Math.Max(1, r.R - r.L), Math.Max(1, r.B - r.T));
        using var g = System.Drawing.Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        try { PrintWindow(hwnd, hdc, 2); } // PW_RENDERFULLCONTENT
        finally { g.ReleaseHdc(hdc); }
        return bmp;
    }

    /// <summary>Average relative luminance (0 = black, 1 = white) of the central area.</summary>
    public static double AverageLuminance(System.Drawing.Bitmap bmp)
    {
        double sum = 0; int n = 0;
        for (int y = bmp.Height / 5; y < bmp.Height * 4 / 5; y += 7)
            for (int x = bmp.Width / 4; x < bmp.Width * 3 / 4; x += 7)
            {
                var c = bmp.GetPixel(x, y);
                sum += (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
                n++;
            }
        return n == 0 ? 0 : sum / n;
    }
}
