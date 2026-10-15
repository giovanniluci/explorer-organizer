using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Services;

public sealed class FilesReadyEventArgs : EventArgs
{
    public FilesReadyEventArgs(Profile profile, WatchedFolder folder, IReadOnlyList<FileInput> files, string trigger)
    {
        Profile = profile; Folder = folder; Files = files; Trigger = trigger;
    }
    public Profile Profile { get; }
    public WatchedFolder Folder { get; }
    public IReadOnlyList<FileInput> Files { get; }
    public string Trigger { get; }
}

/// <summary>
/// Real-time watchers (FileSystemWatcher) and the interval/daily scheduler. A new file is handed to the rules
/// only after it has stayed unchanged and unlocked for <see cref="AppSettings.StabilitySeconds"/>, so downloads
/// and copies in progress are never touched.
/// </summary>
public sealed class FolderMonitor : IDisposable
{
    private sealed class Pending
    {
        public required Profile Profile;
        public required WatchedFolder Folder;
        public required string Root;
        public DateTime FirstSeen;
        public DateTime LastEvent;
        public long LastSize = -1;
    }

    private readonly Func<AppSettings> _settings;
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _suppressed = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _configLock = new();
    private List<(Profile Profile, WatchedFolder Folder)> _scheduled = new();
    private readonly Timer _stabilityTimer;
    private readonly Timer _scheduleTimer;
    private int _ticking;

    public FolderMonitor(Func<AppSettings> settings)
    {
        _settings = settings;
        _stabilityTimer = new Timer(_ => StabilityTick(), null, 1000, 1000);
        _scheduleTimer = new Timer(_ => ScheduleTick(), null, 15000, 30000);
    }

    public event EventHandler<FilesReadyEventArgs>? FilesReady;
    public event EventHandler<string>? WatcherError;

    public bool Paused { get; set; }
    public int ActiveWatchers { get { lock (_configLock) return _watchers.Count; } }

    public void Configure(IEnumerable<Profile> profiles)
    {
        lock (_configLock)
        {
            foreach (var w in _watchers.Values) w.Dispose();
            _watchers.Clear();
            _pending.Clear();
            var scheduled = new List<(Profile, WatchedFolder)>();
            foreach (var profile in profiles.Where(p => p.Enabled))
            {
                foreach (var folder in profile.Folders.Where(f => f.Enabled))
                {
                    if (folder.Trigger is TriggerMode.Interval or TriggerMode.Daily) scheduled.Add((profile, folder));
                    if (folder.Trigger != TriggerMode.Realtime) continue;
                    var path = ResolveFolderPath(folder.Path);
                    if (!Directory.Exists(path)) { WatcherError?.Invoke(this, path); continue; }
                    try
                    {
                        var w = new FileSystemWatcher(path)
                        {
                            IncludeSubdirectories = folder.IncludeSubfolders,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                            InternalBufferSize = 64 * 1024,
                        };
                        var (p, f, root) = (profile, folder, path);
                        w.Created += (_, e) => Enqueue(e.FullPath, p, f, root);
                        w.Changed += (_, e) => Touch(e.FullPath);
                        w.Renamed += (_, e) => Enqueue(e.FullPath, p, f, root);
                        w.Error += (_, _) => { foreach (var input in Scan(f)) Enqueue(input.Path, p, f, root); };
                        w.EnableRaisingEvents = true;
                        _watchers[folder.Id] = w;
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                    {
                        WatcherError?.Invoke(this, path + ": " + ex.Message);
                    }
                }
            }
            _scheduled = scheduled;
        }
    }

    /// <summary>Ignore events for paths the engine itself just produced (prevents processing loops).</summary>
    public void Suppress(IEnumerable<string> paths)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        foreach (var p in paths.Where(p => !string.IsNullOrEmpty(p))) _suppressed[p] = until;
    }

    /// <summary>Expands {downloads}, {pictures}, ... and %ENV% in a watched folder path.</summary>
    public static string ResolveFolderPath(string template)
    {
        var s = Regex.Replace(template ?? "", @"\{(\w+)\}", m => SystemFolders.Get(m.Groups[1].Value) ?? m.Value);
        s = Environment.ExpandEnvironmentVariables(s);
        try { return Path.GetFullPath(s); } catch { return s; }
    }

    /// <summary>
    /// Lists the files currently in a watched folder (hidden/system files are skipped). With <paramref name="includeFolders"/>
    /// the subfolders are listed too (before the files), so rules for folders can move them as a whole.
    /// </summary>
    public static IReadOnlyList<FileInput> Scan(WatchedFolder folder, string? ignorePatterns = null, bool includeFolders = false)
    {
        var root = ResolveFolderPath(folder.Path);
        if (!Directory.Exists(root)) return Array.Empty<FileInput>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = folder.IncludeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        var list = new List<FileInput>();
        if (includeFolders)
            list.AddRange(Directory.EnumerateDirectories(root, "*", options)
                .Where(d => !IsIgnored(Path.GetFileName(d), ignorePatterns))
                .Select(d => new FileInput(d, root)));
        list.AddRange(Directory.EnumerateFiles(root, "*", options)
            .Where(f => !IsIgnored(Path.GetFileName(f), ignorePatterns))
            .Select(f => new FileInput(f, root)));
        return list;
    }

    public static bool IsIgnored(string fileName, string? patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns)) return false;
        return TextMatcher.IsMatch(fileName, TextOperator.Wildcard, patterns, caseSensitive: false);
    }

    private void Enqueue(string path, Profile profile, WatchedFolder folder, string root)
    {
        if (Paused) return;
        if (Directory.Exists(path))
        {
            if (!folder.IncludeSubfolders) return;
            try { foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) Enqueue(f, profile, folder, root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return;
        }
        if (IsIgnored(Path.GetFileName(path), _settings().IgnorePatterns)) return;
        if (_suppressed.TryGetValue(path, out var until) && until > DateTime.UtcNow) return;
        var now = DateTime.UtcNow;
        _pending.AddOrUpdate(path,
            _ => new Pending { Profile = profile, Folder = folder, Root = root, FirstSeen = now, LastEvent = now },
            (_, existing) => { existing.LastEvent = now; return existing; });
    }

    private void Touch(string path)
    {
        if (_pending.TryGetValue(path, out var p)) p.LastEvent = DateTime.UtcNow;
    }

    private void StabilityTick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1) return;
        try
        {
            foreach (var key in _suppressed.Where(kv => kv.Value < DateTime.UtcNow).Select(kv => kv.Key).ToList())
                _suppressed.TryRemove(key, out _);
            if (_pending.IsEmpty) return;

            var now = DateTime.UtcNow;
            var stability = TimeSpan.FromSeconds(Math.Max(1, _settings().StabilitySeconds));
            var ready = new List<(string Path, Pending P)>();
            foreach (var (path, p) in _pending)
            {
                if (now - p.FirstSeen > TimeSpan.FromHours(2)) { _pending.TryRemove(path, out _); continue; }
                if (now - p.LastEvent < stability) continue;
                if (!File.Exists(path)) { _pending.TryRemove(path, out _); continue; }
                long size;
                try { size = new FileInfo(path).Length; } catch { continue; }
                if (size != p.LastSize) { p.LastSize = size; p.LastEvent = now; continue; } // still growing
                if (!CanOpenExclusively(path)) { p.LastEvent = now; continue; } // still being written
                ready.Add((path, p));
            }
            foreach (var group in ready.GroupBy(r => (r.P.Profile, r.P.Folder)))
            {
                foreach (var r in group) _pending.TryRemove(r.Path, out _);
                if (Paused) continue;
                var files = group.Select(r => new FileInput(r.Path, r.P.Root)).ToList();
                FilesReady?.Invoke(this, new FilesReadyEventArgs(group.Key.Profile, group.Key.Folder, files, "watch"));
            }
        }
        finally { Interlocked.Exchange(ref _ticking, 0); }
    }

    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return true; } // not a lock: the engine will report the permission error
    }

    private void ScheduleTick() => RunDueSchedules(DateTime.Now);

    /// <summary>Raises <see cref="FilesReady"/> (trigger "schedule") for every interval/daily folder that is due at <paramref name="now"/>.</summary>
    public void RunDueSchedules(DateTime now)
    {
        if (Paused) return;
        List<(Profile Profile, WatchedFolder Folder)> scheduled;
        lock (_configLock) scheduled = _scheduled.ToList();
        foreach (var (profile, folder) in scheduled)
        {
            if (!IsDue(folder, now)) continue;
            folder.LastRun = now;
            var files = Scan(folder, _settings().IgnorePatterns, includeFolders: true);
            if (files.Count > 0) FilesReady?.Invoke(this, new FilesReadyEventArgs(profile, folder, files, "schedule"));
        }
    }

    public static bool IsDue(WatchedFolder folder, DateTime now)
    {
        switch (folder.Trigger)
        {
            case TriggerMode.Interval:
                return folder.LastRun == null || now - folder.LastRun.Value >= TimeSpan.FromMinutes(folder.IntervalMinutes);
            case TriggerMode.Daily:
                if (!TimeSpan.TryParseExact(folder.DailyTime, @"hh\:mm", CultureInfo.InvariantCulture, out var at)) return false;
                return now.TimeOfDay >= at && (folder.LastRun == null || folder.LastRun.Value.Date < now.Date);
            default:
                return false;
        }
    }

    public void Dispose()
    {
        _stabilityTimer.Dispose();
        _scheduleTimer.Dispose();
        lock (_configLock)
        {
            foreach (var w in _watchers.Values) w.Dispose();
            _watchers.Clear();
        }
    }
}
