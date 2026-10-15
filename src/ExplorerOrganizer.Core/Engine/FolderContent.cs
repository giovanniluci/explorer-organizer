using ExplorerOrganizer.Core.Localization;

namespace ExplorerOrganizer.Core.Engine;

/// <summary>What a folder contains: used by the "folder contains files with extension" condition and the preview notes.</summary>
public sealed class FolderContent
{
    public static readonly FolderContent Empty = new();

    public int FileCount { get; private set; }
    public long TotalBytes { get; private set; }
    /// <summary>Lower-case extension without dot ("" = no extension) → number of files.</summary>
    public Dictionary<string, int> Extensions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Scans the files of <paramref name="dir"/> and its subfolders (hidden/system files and links are skipped).</summary>
    public static FolderContent Scan(string dir)
    {
        var c = new FolderContent();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        try
        {
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", options))
            {
                c.FileCount++;
                try { c.TotalBytes += f.Length; } catch (IOException) { }
                var ext = f.Extension.TrimStart('.').ToLowerInvariant();
                c.Extensions[ext] = c.Extensions.TryGetValue(ext, out var n) ? n + 1 : 1;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return c;
    }

    public int CountWithExtensions(IEnumerable<string> extensions) =>
        extensions.Distinct(StringComparer.OrdinalIgnoreCase).Sum(e => Extensions.TryGetValue(e, out var n) ? n : 0);

    /// <summary>"42 file: .ttf, .otf" (most frequent extensions first).</summary>
    public string Summary
    {
        get
        {
            if (FileCount == 0) return Loc.T("Folder.Empty");
            var top = Extensions.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Key.Length == 0 ? Loc.T("Folder.NoExtension") : "." + kv.Key).ToList();
            var list = string.Join(", ", top.Take(4)) + (top.Count > 4 ? ", …" : "");
            return string.Format(Loc.T("Folder.Summary"), FileCount, list);
        }
    }
}
