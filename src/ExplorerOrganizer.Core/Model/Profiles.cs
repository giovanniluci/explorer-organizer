using System.Collections.ObjectModel;

namespace ExplorerOrganizer.Core.Model;

public sealed class Rule : ObservableObject
{
    private string _name = "";
    private bool _enabled = true;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    /// <summary>Root condition ("IF ..."). Always a group so the editor can add children.</summary>
    public ConditionGroup Condition { get; set; } = new();
    /// <summary>Actions ("THEN ..."), executed in order on the matching file.</summary>
    public ObservableCollection<RuleAction> Actions { get; set; } = new();
}

public sealed class WatchedFolder : ObservableObject
{
    private string _path = "";
    private bool _enabled = true;
    private bool _includeSubfolders;
    private TriggerMode _trigger = TriggerMode.Manual;
    private int _intervalMinutes = 60;
    private string _dailyTime = "09:00";
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>May contain tokens such as {downloads}.</summary>
    public string Path { get => _path; set => Set(ref _path, value ?? ""); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public bool IncludeSubfolders { get => _includeSubfolders; set => Set(ref _includeSubfolders, value); }
    public TriggerMode Trigger { get => _trigger; set => Set(ref _trigger, value); }
    public int IntervalMinutes { get => _intervalMinutes; set => Set(ref _intervalMinutes, Math.Max(1, value)); }
    /// <summary>"HH:mm", local time.</summary>
    public string DailyTime { get => _dailyTime; set => Set(ref _dailyTime, value ?? "09:00"); }
    public DateTime? LastRun { get; set; }
}

public sealed class Profile : ObservableObject
{
    private string _name = "";
    private bool _enabled = true;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    /// <summary>Evaluated top to bottom: the first matching rule handles the file.</summary>
    public ObservableCollection<Rule> Rules { get; set; } = new();
    public ObservableCollection<WatchedFolder> Folders { get; set; } = new();
}

public sealed class ProfileLibrary
{
    public int SchemaVersion { get; set; } = 1;
    public Guid ActiveProfileId { get; set; }
    public ObservableCollection<Profile> Profiles { get; set; } = new();
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    /// <summary>"it", "en" or "" (system language).</summary>
    public string Language { get; set; } = "";
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    /// <summary>Show the preview window before applying dropped / context-menu / manual scans.</summary>
    public bool PreviewBeforeApply { get; set; } = true;
    /// <summary>When true, automatic triggers only log what they would do.</summary>
    public bool SimulationMode { get; set; }
    public bool ShowDropTarget { get; set; }
    public double DropTargetLeft { get; set; } = double.NaN;
    public double DropTargetTop { get; set; } = double.NaN;
    public bool FirstRunCompleted { get; set; }
    /// <summary>The ready-made rules (Video, Foto, Immagini, PDF, Archivi, Programmi) were added once: never add them again.</summary>
    public bool DefaultRulesSeeded { get; set; }
    public bool MonitoringPaused { get; set; }
    /// <summary>Seconds a new file must stay unchanged before a real-time rule touches it.</summary>
    public int StabilitySeconds { get; set; } = 3;
    public string IgnorePatterns { get; set; } = "*.crdownload;*.part;*.partial;*.download;*.opdownload;*.tmp;~$*;desktop.ini;thumbs.db";
}
