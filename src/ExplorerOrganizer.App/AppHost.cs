using System.Windows;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.App;

/// <summary>Owns the services and the run pipeline (preview → apply → log/statistics/undo/notification).</summary>
public sealed class AppHost : IDisposable
{
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly object _statsLock = new();

    public AppHost()
    {
        Paths = AppPaths.Detect();
        Settings = JsonStore.Load(Paths.SettingsFile, () => new AppSettings());
        Loc.SetLanguage(Settings.Language); // migration notes are written in the user's language
        Library = JsonStore.LoadProfiles(Paths.ProfilesFile, () => new ProfileLibrary(), MigrationNotes);
        Stats = JsonStore.Load(Paths.StatisticsFile, () => new Statistics());
        Log = new ActivityLog(Paths.LogDirectory);
        Journal = new UndoJournal(Paths.JournalFile);
        RecycleBin = new WindowsRecycleBin();
        Engine = new RuleEngine(new ShellMetadataProvider(), new ContentReader(), RecycleBin);
        Monitor = new FolderMonitor(() => Settings) { Paused = Settings.MonitoringPaused };
        Monitor.FilesReady += OnFilesReady;
        Monitor.WatcherError += (_, msg) => Log.Error(string.Format(Loc.T("Msg.WatcherError"), msg), "watch");
        Journal.Changed += (_, _) => RaiseStateChanged();
        foreach (var n in MigrationNotes)
            Log.Info(string.Format(Loc.T(n.NeedsReview ? "Migration.NeedsReview" : "Migration.Converted"), n.Profile, n.Rule));
    }

    /// <summary>Rules whose old NOT checkbox was migrated while loading the profiles.</summary>
    public List<MigrationNote> MigrationNotes { get; } = new();

    /// <summary>Adds the ready-made category rules once (first start); later the user can edit or delete them freely.</summary>
    public void SeedDefaultRules()
    {
        if (Settings.DefaultRulesSeeded) return;
        DefaultRules.Seed(Library, SystemFolders.Downloads);
        Settings.DefaultRulesSeeded = true;
        SaveProfiles();
        SaveSettings();
    }

    public static AppHost Current { get; set; } = null!;

    public AppPaths Paths { get; }
    public AppSettings Settings { get; }
    public ProfileLibrary Library { get; }
    public Statistics Stats { get; }
    public ActivityLog Log { get; }
    public UndoJournal Journal { get; }
    public IRecycleBin RecycleBin { get; }
    public RuleEngine Engine { get; }
    public FolderMonitor Monitor { get; }
    public TrayIcon? Tray { get; set; }

    /// <summary>Statistics, undo journal or monitoring state changed (raised on any thread).</summary>
    public event EventHandler? StateChanged;
    public event EventHandler? BusyChanged;
    public bool IsBusy { get; private set; }

    public Profile? ActiveProfile
    {
        get => Library.Profiles.FirstOrDefault(p => p.Id == Library.ActiveProfileId) ?? Library.Profiles.FirstOrDefault();
        set
        {
            if (value == null) return;
            Library.ActiveProfileId = value.Id;
            SaveProfiles();
        }
    }

    public void SaveSettings() => TrySave(() => JsonStore.Save(Paths.SettingsFile, Settings));

    public void SaveProfiles() => TrySave(() => JsonStore.Save(Paths.ProfilesFile, Library));

    /// <summary>Call after any edit to profiles/rules/folders: persists and reloads watchers.</summary>
    public void ProfilesChanged()
    {
        SaveProfiles();
        Monitor.Configure(Library.Profiles);
        RaiseStateChanged();
    }

    public void SetPaused(bool paused)
    {
        Settings.MonitoringPaused = paused;
        Monitor.Paused = paused;
        SaveSettings();
        RaiseStateChanged();
    }

    /// <summary>Expands dropped paths: folders become the subfolders and files they contain (recursively) with the folder as root.</summary>
    public IReadOnlyList<FileInput> ExpandInputs(IEnumerable<string> paths)
    {
        var list = new List<FileInput>();
        foreach (var raw in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var p = raw.Trim().Trim('"');
            if (File.Exists(p)) list.Add(new FileInput(p));
            else if (Directory.Exists(p))
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint };
                try
                {
                    // subfolders too: rules for folders move them as a whole (the engine then skips their content)
                    list.AddRange(Directory.EnumerateDirectories(p, "*", options).Select(d => new FileInput(d, p)));
                    list.AddRange(Directory.EnumerateFiles(p, "*", options).Select(f => new FileInput(f, p)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Error(ex.Message); }
            }
        }
        return list;
    }

    /// <summary>
    /// User-initiated run (drop, context menu, "scan now"). With "preview before apply" the user confirms
    /// in the preview window; otherwise the files are processed directly. Must be called on the UI thread.
    /// </summary>
    public async Task ProcessInteractiveAsync(Profile? profile, IReadOnlyList<FileInput> inputs, string trigger, Window? owner = null, bool forcePreview = false)
    {
        if (profile == null) { Dialogs.Info(Loc.T("Msg.NoProfile")); return; }
        if (inputs.Count == 0) { Dialogs.Info(Loc.T("Msg.NoFiles")); return; }
        await ProcessInteractiveAsync(profile, _ => inputs, trigger, owner, forcePreview);
    }

    /// <summary>
    /// Same as above, but the items depend on the profile: in the preview the user can pick another profile in the
    /// sidebar, its items are collected with <paramref name="inputsFor"/> and only that profile is applied.
    /// </summary>
    public async Task ProcessInteractiveAsync(Profile? profile, Func<Profile, IReadOnlyList<FileInput>> inputsFor, string trigger, Window? owner = null, bool forcePreview = false)
    {
        if (profile == null) { Dialogs.Info(Loc.T("Msg.NoProfile")); return; }
        IReadOnlyList<FileInput> inputs;
        if (Settings.PreviewBeforeApply || forcePreview)
        {
            var window = new PreviewWindow(Library.Profiles, profile, async p =>
            {
                var items = await Task.Run(() => inputsFor(p));
                var preview = await RunAsync(p, items, trigger, simulate: true);
                return new PreviewData(preview, items);
            });
            if (owner is { IsVisible: true }) window.Owner = owner;
            if (window.ShowDialog() != true) return;
            profile = window.Profile;
            var selected = new HashSet<string>(window.SelectedFiles, StringComparer.OrdinalIgnoreCase);
            inputs = window.Inputs.Where(i => selected.Contains(Path.GetFullPath(i.Path))).ToList();
            if (inputs.Count == 0) return;
        }
        else
        {
            inputs = inputsFor(profile);
            if (inputs.Count == 0) { Dialogs.Info(Loc.T("Msg.NoFiles")); return; }
        }

        var result = await RunAsync(profile, inputs, trigger, simulate: false, ConfirmPermanentDeleteOnUi);
        ShowResultNotification(result, force: true);
    }

    /// <summary>Runs the engine off the UI thread. Real runs are serialized and update log, statistics and undo journal.</summary>
    public async Task<RunResult> RunAsync(Profile profile, IReadOnlyList<FileInput> inputs, string trigger, bool simulate, Func<bool>? confirmPermanent = null)
    {
        await _runLock.WaitAsync();
        SetBusy(true);
        try
        {
            var result = await Task.Run(() => Engine.Run(inputs, profile, new RunOptions
            {
                Simulate = simulate,
                Trigger = trigger,
                ConfirmPermanentDelete = confirmPermanent,
            }));
            if (!simulate) AfterRealRun(result);
            return result;
        }
        finally
        {
            SetBusy(false);
            _runLock.Release();
        }
    }

    public UndoResult UndoLast()
    {
        var batch = Journal.Last;
        if (batch == null) return new UndoResult(0, new[] { Loc.T("Undo.Nothing") });
        Monitor.Suppress(batch.Steps.Select(s => s.OriginalPath ?? s.Path));
        var result = Journal.UndoLast(RecycleBin);
        Log.Info(string.Format(Loc.T("Msg.UndoDone"), batch.Description, result.Reverted, result.Errors.Count), "manual");
        foreach (var e in result.Errors) Log.Error(e, "manual");
        return result;
    }

    private void AfterRealRun(RunResult result)
    {
        Monitor.Suppress(result.Records.Select(r => r.Target));
        Log.Add(result.Records);
        lock (_statsLock)
        {
            Stats.Apply(result, DateTime.Now);
            TrySave(() => JsonStore.Save(Paths.StatisticsFile, Stats));
        }
        if (result.Undo != null) Journal.Push(result.Undo);
        RaiseStateChanged();
    }

    private async void OnFilesReady(object? sender, FilesReadyEventArgs e)
    {
        try
        {
            var simulate = Settings.SimulationMode;
            var result = await RunAsync(e.Profile, e.Files, e.Trigger, simulate);
            if (simulate)
            {
                foreach (var r in result.Records) r.Selected = false;
                Log.Add(result.Records); // simulation mode: only log what would happen
            }
            if (e.Trigger == "schedule") SaveProfiles(); // persists LastRun
            ShowResultNotification(result, force: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message, e.Trigger);
        }
    }

    private void ShowResultNotification(RunResult result, bool force)
    {
        if (!Settings.ShowNotifications || Tray == null) return;
        int changed = result.Records.Where(r => r.Status is OperationStatus.Done or OperationStatus.Planned)
            .Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (changed == 0 && result.Failed == 0 && !force) return;
        var text = result.Records.Any(r => r.Status == OperationStatus.Planned)
            ? string.Format(Loc.T("Notify.Simulated"), changed)
            : string.Format(Loc.T("Notify.Done"), changed, result.Failed);
        Tray.Notify(Loc.T("App.Title"), text, result.Failed > 0);
    }

    private bool ConfirmPermanentDeleteOnUi() =>
        Application.Current.Dispatcher.Invoke(() => Dialogs.Confirm(Loc.T("Confirm.PermanentDelete"), danger: true));

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        BusyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void TrySave(Action save)
    {
        try { save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(string.Format(Loc.T("Msg.SaveFailed"), ex.Message));
        }
    }

    public void Dispose()
    {
        Monitor.Dispose();
        SaveSettings();
        SaveProfiles();
    }
}
