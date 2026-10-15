using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Engine;

public enum OperationKind { Move, Copy, Rename, Recycle, Delete, Compress, Extract, Shortcut, OpenWith, RemoveFolder, Error }

public enum OperationStatus { Planned, Done, Unchanged, Skipped, Failed }

/// <summary>One line of the preview / activity log.</summary>
public sealed class OperationRecord : ObservableObject
{
    private bool _selected = true;

    public DateTime Time { get; init; } = DateTime.Now;
    public string Trigger { get; init; } = "";
    public string RuleName { get; init; } = "";
    /// <summary>The file the rule was started on (preview selection works per file).</summary>
    public string FilePath { get; init; } = "";
    /// <summary>The item is a folder moved/handled as a whole.</summary>
    public bool IsFolder { get; init; }
    public OperationKind Kind { get; init; }
    public OperationStatus Status { get; init; }
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
    public string Message { get; init; } = "";

    /// <summary>Preview checkbox.</summary>
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
}
