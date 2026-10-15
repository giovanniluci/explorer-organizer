using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExplorerOrganizer.App;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Views;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using Microsoft.Win32;

namespace ExplorerOrganizer.UiTests;

/// <summary>UI tests that drive the real WPF windows in-process (isolated data folder).</summary>
[Collection("wpf")]
public class InProcessUiTests
{
    private readonly WpfAppFixture _fx;

    public InProcessUiTests(WpfAppFixture fx) => _fx = fx;

    private static MainWindow OpenMain()
    {
        var w = new MainWindow();
        w.Show();
        WpfAppFixture.Pump(400);
        return w;
    }

    private static string? PageId(MainWindow w) =>
        AutomationProperties.GetAutomationId((DependencyObject)((ContentControl)w.FindName("Page")).Content);

    [Fact]
    public void Startup_main_window_is_compact_and_opens_on_home()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                Assert.Equal(450, w.Width);
                Assert.Equal(660, w.Height);
                Assert.Equal(420, w.MinWidth);
                Assert.Equal(560, w.MinHeight);
                Assert.Equal(WindowStyle.None, w.WindowStyle); // custom title bar
                Assert.Equal("home", w.CurrentPage);
                Assert.Equal("Page_home", PageId(w));
                var top = (ListBox)w.FindName("NavTop");
                var bottom = (ListBox)w.FindName("NavBottom");
                Assert.Equal(new[] { "home", "rules", "folders", "history", "toolbox" }, top.Items.Cast<ListBoxItem>().Select(i => (string)i.Tag));
                Assert.Equal(new[] { "restore", "settings", "feedback" }, bottom.Items.Cast<ListBoxItem>().Select(i => (string)i.Tag));
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Navigation_reaches_every_page_and_keeps_one_selection()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                var top = (ListBox)w.FindName("NavTop");
                var bottom = (ListBox)w.FindName("NavBottom");
                foreach (var key in MainWindow.PageKeys)
                {
                    w.Navigate(key);
                    WpfAppFixture.Pump(150);
                    Assert.Equal(key, w.CurrentPage);
                    Assert.Equal("Page_" + key, PageId(w));
                    Assert.True(top.SelectedIndex < 0 || bottom.SelectedIndex < 0, "only one rail item may be selected");
                }
                w.Navigate("activity"); // legacy name used by the tray balloon
                Assert.Equal("history", w.CurrentPage);
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Keyboard_ctrl_digits_navigate()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                for (int i = 0; i < MainWindow.PageKeys.Length; i++)
                {
                    var binding = w.InputBindings.OfType<KeyBinding>()
                        .Single(b => b.Key == Key.D1 + i && b.Modifiers == ModifierKeys.Control);
                    binding.Command.Execute(null);
                    WpfAppFixture.Pump(100);
                    Assert.Equal(MainWindow.PageKeys[i], w.CurrentPage);
                }
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Rail_items_have_accessible_names_and_ids()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                foreach (var list in new[] { (ListBox)w.FindName("NavTop"), (ListBox)w.FindName("NavBottom") })
                    foreach (ListBoxItem item in list.Items)
                    {
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(item)));
                        Assert.Equal("Nav_" + item.Tag, AutomationProperties.GetAutomationId(item));
                    }
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Home_shows_waiting_files_and_active_rules()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                WpfAppFixture.Pump(1500); // async file count
                var home = (DashboardView)((ContentControl)w.FindName("Page")).Content;
                int files = Directory.GetFiles(_fx.DemoFolder).Length;
                int active = AppHost.Current.ActiveProfile!.Rules.Count(r => r.Enabled);
                Assert.Equal(files.ToString(), ((TextBlock)home.FindName("PendingValue")).Text);
                Assert.Equal(active.ToString(), ((TextBlock)home.FindName("RulesValue")).Text);
                Assert.Contains(files.ToString(), ((TextBlock)home.FindName("BannerTitle")).Text);
                Assert.True(((BarIndicator)home.FindName("RulesBar")).Value > 0);
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Theme_light_and_dark_switch_palette_and_render_every_page()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                foreach (var (mode, surface, dark) in new[] { (ThemeMode.Light, Colors.White, false), (ThemeMode.Dark, Color.FromRgb(0x27, 0x2C, 0x36), true) })
                {
                    ThemeManager.Apply(mode);
                    WpfAppFixture.Pump(150);
                    Assert.Equal(dark, ThemeManager.IsDark);
                    Assert.Equal(surface, ((SolidColorBrush)Application.Current.Resources["Surface"]).Color);
                    Assert.Equal(Color.FromRgb(0x15, 0x65, 0xC0), ((SolidColorBrush)Application.Current.Resources["Accent"]).Color);
                    foreach (var key in MainWindow.PageKeys)
                    {
                        w.Navigate(key);
                        WpfAppFixture.Pump(key == "home" ? 900 : 200);
                        Render(w, Path.Combine(TestData.ScreenshotDir, $"render-{key}-{(dark ? "dark" : "light")}.png"));
                    }
                }
            }
            finally
            {
                ThemeManager.Apply(ThemeMode.Light);
                w.Close();
            }
        });
    }

    private static void Render(Window w, string file)
    {
        var root = (FrameworkElement)w.Content;
        var dpi = VisualTreeHelper.GetDpi(root);
        var size = new Size(root.ActualWidth, root.ActualHeight);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle((Brush)Application.Current.Resources["WindowBg"], null, new Rect(size));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(size));
        }
        var bmp = new RenderTargetBitmap((int)(size.Width * dpi.DpiScaleX), (int)(size.Height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
    }

    // ---- WCAG AA contrast of the palette (both themes) ----

    private static double Luminance(Color c)
    {
        static double Ch(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Contrast(string fg, string bg)
    {
        var a = Luminance(((SolidColorBrush)Application.Current.Resources[fg]).Color);
        var b = Luminance(((SolidColorBrush)Application.Current.Resources[bg]).Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Palette_meets_WCAG_AA_contrast(ThemeMode mode)
    {
        _fx.Invoke(() =>
        {
            try
            {
                ThemeManager.Apply(mode);
                foreach (var bg in new[] { "Surface", "BgTop", "BgBottom", "Hover" })
                    foreach (var fg in new[] { "Text", "TextMuted", "Link" })
                        Assert.True(Contrast(fg, bg) >= 4.5, $"{mode}: {fg} on {bg} = {Contrast(fg, bg):0.00}");
                foreach (var fg in new[] { "Success", "Warning", "Danger" })
                    Assert.True(Contrast(fg, "Surface") >= 4.5, $"{mode}: {fg} on Surface = {Contrast(fg, "Surface"):0.00}");
                Assert.True(Contrast("AccentText", "Accent") >= 4.5, $"{mode}: button text");
                Assert.True(Contrast("Focus", "Surface") >= 3, $"{mode}: focus ring");
                Assert.True(Contrast("Focus", "BgTop") >= 3, $"{mode}: focus ring on background");
            }
            finally { ThemeManager.Apply(ThemeMode.Light); }
        });
    }

    // ---- localization ----

    [Fact]
    public void Language_switch_updates_ui_strings()
    {
        _fx.Invoke(() =>
        {
            var w = OpenMain();
            try
            {
                Loc.SetLanguage("en");
                LocalizationBinder.Apply();
                w.RebuildPages();
                WpfAppFixture.Pump(200);
                Assert.Equal("Rules", Application.Current.Resources["S.Nav.Rules"]);
                Assert.Equal("Organize now", Application.Current.Resources["S.Home.OrganizeNow"]);
                Loc.SetLanguage("it");
                LocalizationBinder.Apply();
                w.RebuildPages();
                Assert.Equal("Regole", Application.Current.Resources["S.Nav.Rules"]);
                Assert.Equal("Organizza ora", Application.Current.Resources["S.Home.OrganizeNow"]);
            }
            finally { w.Close(); }
        });
    }

    [Fact]
    public void Every_string_key_used_by_the_ui_exists_in_both_languages()
    {
        var src = Path.Combine(TestData.RepoRoot, "src");
        var keys = new SortedSet<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(src, "ExplorerOrganizer.App"), "*.xaml", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"\{DynamicResource S\.([A-Za-z0-9_.]+)\}")) keys.Add(m.Groups[1].Value);
        foreach (var f in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(p => !p.Contains(@"\obj\")))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"Loc\.T\(""([A-Za-z0-9_.]+)""\)")) keys.Add(m.Groups[1].Value);
        Assert.True(keys.Count > 150, "scan found too few keys: " + keys.Count);
        var missing = new List<string>();
        foreach (var lang in new[] { "it", "en" })
        {
            Loc.SetLanguage(lang);
            foreach (var k in keys)
                if (Loc.T(k) == k || string.IsNullOrWhiteSpace(Loc.T(k))) missing.Add(lang + ":" + k);
        }
        Loc.SetLanguage("it");
        Assert.True(missing.Count == 0, "Missing strings: " + string.Join(", ", missing));
    }

    // ---- tray, autostart, context menu (sandboxed registry keys) ----

    [Fact]
    public void Tray_menu_offers_open_scan_pause_drop_undo_exit()
    {
        _fx.Invoke(() =>
        {
            using var tray = new TrayIcon((App.App)Application.Current);
            var items = tray.Menu!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Select(i => i.Text).ToList();
            Assert.Equal(new[] { Loc.T("Tray.Open"), Loc.T("Tray.ScanAll"), Loc.T("Tray.Pause"), Loc.T("Tray.DropTarget"), Loc.T("Tray.Undo"), Loc.T("Tray.Exit") }, items);
            AppHost.Current.SetPaused(true);
            tray.BuildMenu();
            Assert.Contains(Loc.T("Tray.Resume"), tray.Menu!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Select(i => i.Text));
            AppHost.Current.SetPaused(false);
        });
    }

    private const string Sandbox = @"Software\ExplorerOrganizer.UiTests";

    [Fact]
    public void Autostart_with_windows_writes_and_removes_run_entry()
    {
        var oldRun = ShellIntegration.RunKey;
        ShellIntegration.RunKey = Sandbox + @"\Run";
        try
        {
            ShellIntegration.SetAutostart(true);
            Assert.True(ShellIntegration.IsAutostartEnabled());
            using (var k = Registry.CurrentUser.OpenSubKey(ShellIntegration.RunKey)!)
                Assert.Equal(ShellIntegration.AutostartCommand(ShellIntegration.ExePath), k.GetValue("ExplorerOrganizer"));
            Assert.EndsWith("--minimized", ShellIntegration.AutostartCommand(@"C:\x\ExplorerOrganizer.exe"));
            ShellIntegration.SetAutostart(false);
            Assert.False(ShellIntegration.IsAutostartEnabled());
        }
        finally
        {
            ShellIntegration.RunKey = oldRun;
            Registry.CurrentUser.DeleteSubKeyTree(Sandbox, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Context_menu_registers_three_verbs_and_unregisters()
    {
        var oldRoot = ShellIntegration.ClassesRoot;
        ShellIntegration.ClassesRoot = Sandbox + @"\Classes";
        try
        {
            ShellIntegration.RegisterContextMenu();
            Assert.True(ShellIntegration.IsContextMenuRegistered());
            foreach (var (sub, background) in new[] { (@"*\shell", false), (@"Directory\shell", false), (@"Directory\Background\shell", true) })
            {
                using var k = Registry.CurrentUser.OpenSubKey($@"{ShellIntegration.ClassesRoot}\{sub}\ExplorerOrganizer\command")!;
                Assert.Equal(ShellIntegration.MenuCommand(ShellIntegration.ExePath, background), k.GetValue(""));
            }
            ShellIntegration.UnregisterContextMenu();
            Assert.False(ShellIntegration.IsContextMenuRegistered());
        }
        finally
        {
            ShellIntegration.ClassesRoot = oldRoot;
            Registry.CurrentUser.DeleteSubKeyTree(Sandbox, throwOnMissingSubKey: false);
        }
    }

    // ---- drop target trigger, then undo ----

    [Fact]
    public void Drop_on_floating_target_organizes_files_and_undo_restores_them()
    {
        var dropDir = Path.Combine(_fx.DataDir, "dropped-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dropDir);
        var photo = Path.Combine(dropDir, "spiaggia.jpg");
        File.WriteAllText(photo, "img");
        DropTargetWindow? target = null;
        try
        {
            _fx.Invoke(() =>
            {
                target = new DropTargetWindow();
                target.Show();
                WpfAppFixture.Pump(200);
                var data = new DataObject(DataFormats.FileDrop, new[] { photo });
                var ctor = typeof(DragEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
                var args = (DragEventArgs)ctor.Invoke(new object[] { data, DragDropKeyStates.None, DragDropEffects.Copy, target, new Point(10, 10) });
                args.RoutedEvent = DragDrop.DropEvent;
                target.RaiseEvent(args);
            });
            var moved = Path.Combine(dropDir, "Immagini", "spiaggia.jpg");
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!File.Exists(moved) && DateTime.UtcNow < deadline) Thread.Sleep(100);
            Assert.True(File.Exists(moved), "dropped file was not organized");
            Assert.False(File.Exists(photo));
            Assert.Contains(AppHost.Current.Log.Snapshot(), r => r.Trigger == "drop" && r.Target == moved);

            _fx.Invoke(() =>
            {
                var restore = new RestoreView();
                restore.Refresh();
                Assert.True(((ListBox)restore.FindName("BatchList")).Items.Count >= 1);
                var result = AppHost.Current.UndoLast();
                Assert.Empty(result.Errors);
            });
            Assert.True(File.Exists(photo));
            Assert.False(File.Exists(moved));
        }
        finally
        {
            _fx.Invoke(() => target?.Close());
        }
    }

    [Fact]
    public void Drop_of_a_folder_expands_to_its_subfolders_and_files()
    {
        var dir = Path.Combine(_fx.DataDir, "folder-drop");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(dir, "sub", "b.txt"), "b");
        var inputs = AppHost.Current.ExpandInputs(new[] { dir });
        Assert.Equal(3, inputs.Count); // "sub" is offered to the rules for folders, its files to the rules for files
        Assert.Single(inputs, i => Directory.Exists(i.Path));
        Assert.All(inputs, i => Assert.Equal(dir, i.RootFolder));
    }
}
