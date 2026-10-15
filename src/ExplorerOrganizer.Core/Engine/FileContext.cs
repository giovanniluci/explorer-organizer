using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

/// <summary>Input file or folder for a run. RootFolder is the watched/dropped folder (for {subdir}).</summary>
public sealed record FileInput(string Path, string? RootFolder = null);

/// <summary>
/// Everything the engine knows about one file while a rule runs on it.
/// LogicalPath follows the file through moves/renames (also in simulation);
/// PhysicalPath is where the bytes really are (it does not change in simulation).
/// </summary>
public sealed class FileContext
{
    private readonly IMetadataProvider _metadata;
    private readonly IContentReader _content;
    private readonly Dictionary<string, object?> _metaCache = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemInfo? _info;
    private FolderContent? _folderContent;
    private string? _text;
    private string _physicalPath;

    public FileContext(string path, string? rootFolder, IMetadataProvider metadata, IContentReader content)
    {
        OriginalPath = System.IO.Path.GetFullPath(path);
        LogicalPath = OriginalPath;
        _physicalPath = OriginalPath;
        RootFolder = string.IsNullOrEmpty(rootFolder) ? null : System.IO.Path.GetFullPath(rootFolder);
        IsFolder = Directory.Exists(OriginalPath);
        _metadata = metadata;
        _content = content;
    }

    public string OriginalPath { get; }
    /// <summary>The item is a folder (moved as a whole) instead of a file.</summary>
    public bool IsFolder { get; }
    public string LogicalPath { get; set; }
    public string? RootFolder { get; }

    public string PhysicalPath
    {
        get => _physicalPath;
        set { _physicalPath = value; _info = null; }
    }

    /// <summary>Regex capture groups collected while evaluating conditions ({match:1}, {match:name}).</summary>
    public Dictionary<string, string> Captures { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemInfo Info
    {
        get
        {
            if (_info == null) { _info = IsFolder ? new DirectoryInfo(PhysicalPath) : new FileInfo(PhysicalPath); }
            return _info;
        }
    }

    /// <summary>File size; for a folder the total size of the files it contains.</summary>
    public long Length => IsFolder ? FolderContent.TotalBytes : ((FileInfo)Info).Length;

    /// <summary>Files contained in the folder (recursive, scanned once). Empty for a file.</summary>
    public FolderContent FolderContent => _folderContent ??= IsFolder ? FolderContent.Scan(PhysicalPath) : FolderContent.Empty;

    // a folder has no extension: "Font.Family" is a name, not "Font" + ".Family"
    public string NameWithoutExtension => IsFolder ? FileName : System.IO.Path.GetFileNameWithoutExtension(LogicalPath);
    public string Extension => IsFolder ? "" : System.IO.Path.GetExtension(LogicalPath).TrimStart('.');
    public string FileName => System.IO.Path.GetFileName(LogicalPath);
    public string Folder => System.IO.Path.GetDirectoryName(LogicalPath) ?? "";

    public object? GetMetadata(string canonicalName)
    {
        if (_metaCache.TryGetValue(canonicalName, out var v)) return v;
        v = IsFolder ? null : _metadata.Get(PhysicalPath, canonicalName);
        _metaCache[canonicalName] = v;
        return v;
    }

    public DateTime? DateTaken => GetMetadata("System.Photo.DateTaken") as DateTime?;

    public string Content => _text ??= IsFolder ? "" : _content.Read(PhysicalPath);
}
