using System.Windows;
using System.Windows.Threading;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Microsoft.Win32;

namespace ExplorerOrganizer.App;

public partial class App : Application
{
    private SingleInstance? _single;
    private MainWindow? _main;
    private DropTargetWindow? _dropTarget;
    private TrayIcon? _tray;
    private readonly List<string> _pendingPaths = new();
    private DispatcherTimer? _pendingTimer;
    private bool _exiting;

    private AppHost Host => AppHost.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Loc.Register(UiStrings.All);
        _single = new SingleInstance();
        if (!_single.IsFirst)
        {
            _single.Forward(e.Args.Length == 0 ? new[] { "--show" } : e.Args);
            _single.Dispose();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        AppHost.Current = new AppHost();
        Loc.SetLanguage(Host.Settings.Language);
        LocalizationBinder.Apply();
        ThemeManager.Apply(Host.Settings.Theme);
        SystemEvents.UserPreferenceChanged += (_, _) => Dispatcher.BeginInvoke(() => ThemeManager.Apply(Host.Settings.Theme));
        SystemParameters.StaticPropertyChanged += (_, a) =>
        {
            if (a.PropertyName == nameof(SystemParameters.HighContrast)) ThemeManager.Apply(Host.Settings.Theme);
        };

        _tray = new TrayIcon(this);
        Host.Tray = _tray;
        Host.Journal.Changed += (_, _) => Dispatcher.BeginInvoke(() => _tray?.BuildMenu());

        if (!Host.Settings.FirstRunCompleted) RunWizard();
        if (Host.Library.Profiles.Count == 0)
        {
            Host.Library.Profiles.Add(new Profile { Name = Loc.T("Profile.DefaultName") });
            Host.SaveProfiles();
        }
        Host.SeedDefaultRules();
        var review = Host.MigrationNotes.Where(n => n.NeedsReview).ToList();
        if (review.Count > 0)
            Dialogs.Info(string.Join("\n", review.Select(n => string.Format(Loc.T("Migration.NeedsReview"), n.Profile, n.Rule))));
        Host.Monitor.Configure(Host.Library.Profiles);

        _single.ArgumentsReceived += args => Dispatcher.BeginInvoke(() => HandleArguments(args));
        _single.StartServer();

        bool minimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!minimized) ShowMainWindow();
        else Dispatcher.BeginInvoke(TrimMemory, DispatcherPriority.ApplicationIdle);
        if (Host.Settings.ShowDropTarget) SetDropTargetVisible(true);
        HandleArguments(e.Args);
    }

    private void RunWizard()
    {
        var wizard = new WizardWindow();
        wizard.ShowDialog();
        Host.Settings.FirstRunCompleted = true;
        Host.SaveSettings();
        Loc.SetLanguage(Host.Settings.Language);
        LocalizationBinder.Apply();
        ThemeManager.Apply(Host.Settings.Theme);
        _tray?.BuildMenu();
    }

    /// <summary>Command line: [--minimized] [--show] [--scan] [--process] path...</summary>
    public void HandleArguments(string[] args)
    {
        if (args.Any(a => a.Equals("--show", StringComparison.OrdinalIgnoreCase))) ShowMainWindow();
        if (args.Any(a => a.Equals("--scan", StringComparison.OrdinalIgnoreCase))) _ = ScanAllAsync();
        var paths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (paths.Count == 0) return;

        // Explorer starts one process per selected item: collect them for a moment and process them together.
        _pendingPaths.AddRange(paths);
        _pendingTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(700), DispatcherPriority.Normal, async (_, _) =>
        {
            _pendingTimer!.Stop();
            var batch = _pendingPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _pendingPaths.Clear();
            await ProcessPathsAsync(batch, "context");
        }, Dispatcher);
        _pendingTimer.Stop();
        _pendingTimer.Start();
    }

    public async Task ProcessPathsAsync(IEnumerable<string> paths, string trigger, Profile? profile = null, Window? owner = null)
    {
        try
        {
            var inputs = Host.ExpandInputs(paths);
            await Host.ProcessInteractiveAsync(profile ?? Host.ActiveProfile, inputs, trigger, owner ?? _main);
        }
        catch (Exception ex)
        {
            Host.Log.Error(ex.Message, trigger);
            Dialogs.Error(ex.Message);
        }
    }

    /// <summary>
    /// "Scan now". With the preview a single window opens on <paramref name="only"/> (or the active profile) and the
    /// user picks the profile to organize in its sidebar; without the preview every enabled profile is processed.
    /// Subfolders are included so that rules for folders can move them.
    /// </summary>
    public async Task ScanAllAsync(Profile? only = null, WatchedFolder? folder = null, bool forcePreview = false)
    {
        var ignore = Host.Settings.IgnorePatterns;
        IReadOnlyList<FileInput> InputsFor(Profile p, WatchedFolder? f) =>
            (f != null ? new[] { f } : p.Folders.Where(x => x.Enabled).ToArray())
                .SelectMany(x => FolderMonitor.Scan(x, ignore, includeFolders: true)).ToList();

        if (Host.Settings.PreviewBeforeApply || forcePreview)
        {
            var active = Host.ActiveProfile;
            var start = only ?? (active is { Enabled: true } ? active : Host.Library.Profiles.FirstOrDefault(p => p.Enabled) ?? active);
            try { await Host.ProcessInteractiveAsync(start, p => InputsFor(p, p == only ? folder : null), "scan", _main, forcePreview: true); }
            catch (Exception ex) { Host.Log.Error(ex.Message, "scan"); Dialogs.Error(ex.Message); }
            return;
        }

        var profiles = only != null ? new[] { only } : Host.Library.Profiles.Where(p => p.Enabled).ToArray();
        int total = 0;
        foreach (var profile in profiles)
        {
            var inputs = InputsFor(profile, only != null ? folder : null);
            total += inputs.Count;
            if (inputs.Count == 0) continue;
            try { await Host.ProcessInteractiveAsync(profile, inputs, "scan", _main, forcePreview); }
            catch (Exception ex) { Host.Log.Error(ex.Message, "scan"); Dialogs.Error(ex.Message); }
        }
        if (total == 0) Dialogs.Info(Loc.T("Msg.NoFilesInFolders"));
    }

    public void ShowMainWindow(string? page = null)
    {
        if (_main == null)
        {
            _main = new MainWindow();
            _main.Closed += (_, _) =>
            {
                // Closing the window keeps the app in the tray but releases the whole UI tree (low idle footprint).
                _main = null;
                if (_exiting) return;
                if (Host.Settings.ShowNotifications) _tray?.Notify(Loc.T("App.Title"), Loc.T("Notify.StillRunning"), false);
                Dispatcher.BeginInvoke(TrimMemory, DispatcherPriority.ApplicationIdle);
            };
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
        if (page != null) _main.Navigate(page);
    }

    public void SetDropTargetVisible(bool visible)
    {
        Host.Settings.ShowDropTarget = visible;
        Host.SaveSettings();
        if (visible)
        {
            _dropTarget ??= new DropTargetWindow();
            _dropTarget.Show();
        }
        else _dropTarget?.Hide();
        _tray?.BuildMenu();
        _main?.RefreshState();
    }

    public void UndoLastWithConfirmation()
    {
        var batch = Host.Journal.Last;
        if (batch == null) { Dialogs.Info(Loc.T("Undo.Nothing")); return; }
        var text = string.Format(Loc.T("Confirm.Undo"), batch.Description, batch.Time.ToString("g", Loc.Culture));
        if (batch.HasIrreversibleSteps) text += "\n\n" + Loc.T("Confirm.UndoPartial");
        if (!Dialogs.Confirm(text)) return;
        var result = Host.UndoLast();
        if (result.Errors.Count == 0) Dialogs.Info(string.Format(Loc.T("Msg.UndoOk"), result.Reverted));
        else Dialogs.Error(string.Format(Loc.T("Msg.UndoErrors"), result.Reverted, result.Errors.Count) + "\n\n" + string.Join("\n", result.Errors.Take(10)));
    }

    /// <summary>Re-applies language and theme everywhere after a settings change.</summary>
    public void ApplyAppearance()
    {
        Loc.SetLanguage(Host.Settings.Language);
        LocalizationBinder.Apply();
        ThemeManager.Apply(Host.Settings.Theme);
        _tray?.BuildMenu();
        if (ShellIntegration.IsContextMenuRegistered()) ShellIntegration.RegisterContextMenu(); // menu text follows the language
        _main?.RebuildPages();
        _dropTarget?.RefreshText();
    }

    public void ExitApplication()
    {
        _exiting = true;
        _dropTarget?.Close();
        _main?.Close();
        _tray?.Dispose();
        Host.Dispose();
        _single?.Dispose();
        Shutdown();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

    /// <summary>Returns unused memory to Windows when only the tray icon and watchers are left.</summary>
    private static void TrimMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        SetProcessWorkingSetSize(self.Handle, -1, -1);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try { AppHost.Current?.Log.Error("Unexpected error: " + e.Exception); } catch { }
        Dialogs.Error(Loc.T("Msg.UnexpectedError") + "\n\n" + e.Exception.Message);
        e.Handled = true;
    }
}
