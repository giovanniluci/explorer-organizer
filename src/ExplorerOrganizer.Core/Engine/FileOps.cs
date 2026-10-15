using System.Diagnostics;
using System.IO.Compression;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

/// <summary>
/// File system operations used by the engine. <see cref="RealFileOps"/> touches the disk and records undo
/// steps; <see cref="SimulatedFileOps"/> only tracks a virtual overlay so the preview shows exactly what would happen.
/// </summary>
public interface IFileOps
{
    bool Simulated { get; }
    bool Exists(string path);
    void EnsureDirectory(string dir);
    /// <summary>Moves a file or a folder. When <paramref name="overwrite"/> is true the existing target goes to the Recycle Bin first.</summary>
    void Move(string source, string target, bool overwrite);
    void Copy(string source, string target, bool overwrite);
    void Recycle(string path);
    void DeletePermanently(string path);
    /// <summary>Removes <paramref name="dir"/> and its empty parents, never going above <paramref name="stopAt"/>.</summary>
    void RemoveEmptyFolders(string dir, string? stopAt);
    void CreateZip(string source, string zipPath, bool overwrite);
    /// <summary>Extracts a zip, resolving each entry conflict with <paramref name="policy"/>. Returns the number of files written.</summary>
    int ExtractZip(string zipPath, string destination, ConflictPolicy policy);
    void CreateShortcut(string target, string shortcutPath, bool overwrite);
    void Launch(string application, string arguments);
}

public sealed class RealFileOps : IFileOps
{
    private readonly IRecycleBin _recycleBin;

    public RealFileOps(IRecycleBin recycleBin) { _recycleBin = recycleBin; }

    public bool Simulated => false;
    public List<UndoStep> UndoSteps { get; } = new();
    public bool HasIrreversibleSteps { get; private set; }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public void EnsureDirectory(string dir)
    {
        if (Directory.Exists(dir)) return;
        var missing = new Stack<string>();
        for (var d = dir; !string.IsNullOrEmpty(d) && !Directory.Exists(d); d = Path.GetDirectoryName(d))
            missing.Push(d);
        while (missing.Count > 0)
        {
            var d = missing.Pop();
            Directory.CreateDirectory(d);
            UndoSteps.Add(new UndoStep { Kind = UndoKind.RemoveCreatedDirectory, Path = d });
        }
    }

    public void Move(string source, string target, bool overwrite)
    {
        bool caseOnly = string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
        if (overwrite && !caseOnly && Exists(target)) RecycleInternal(target, Err("Err.CannotOverwriteSafely", target));
        if (Directory.Exists(source)) Retry(() => Directory.Move(source, target));
        else Retry(() => File.Move(source, target));
        UndoSteps.Add(new UndoStep { Kind = UndoKind.MoveBack, Path = target, OriginalPath = source });
    }

    public void Copy(string source, string target, bool overwrite)
    {
        if (overwrite && Exists(target)) RecycleInternal(target, Err("Err.CannotOverwriteSafely", target));
        Retry(() => File.Copy(source, target));
        AddCreated(target);
    }

    public void Recycle(string path) => RecycleInternal(path, Err("Err.RecycleUnsupported", path));

    public void DeletePermanently(string path)
    {
        if (Directory.Exists(path)) Retry(() => Directory.Delete(path, recursive: true));
        else Retry(() => File.Delete(path));
        HasIrreversibleSteps = true;
    }

    public void RemoveEmptyFolders(string dir, string? stopAt)
    {
        // Only folders strictly inside the watched/dropped root are removed: the root itself (e.g. Downloads) never is.
        if (stopAt == null) return;
        var stop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stopAt));
        for (var d = dir; !string.IsNullOrEmpty(d); d = Path.GetDirectoryName(d))
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(d));
            if (!full.StartsWith(stop + "\\", StringComparison.OrdinalIgnoreCase)) break;
            if (!Directory.Exists(full) || Directory.EnumerateFileSystemEntries(full).Any()) break;
            Directory.Delete(full);
            UndoSteps.Add(new UndoStep { Kind = UndoKind.RecreateDirectory, Path = full });
        }
    }

    public void CreateZip(string source, string zipPath, bool overwrite)
    {
        if (overwrite && Exists(zipPath)) RecycleInternal(zipPath, Err("Err.CannotOverwriteSafely", zipPath));
        Retry(() =>
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(source, Path.GetFileName(source), CompressionLevel.Optimal);
        });
        AddCreated(zipPath);
    }

    public int ExtractZip(string zipPath, string destination, ConflictPolicy policy)
    {
        int count = 0;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + "\\";
        using var zip = ZipFile.OpenRead(zipPath);
        EnsureDirectory(destination);
        foreach (var entry in zip.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException(Err("Err.ZipSlip", entry.FullName)); // zip-slip protection
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { EnsureDirectory(dest); continue; }
            var resolved = ConflictResolver.Resolve(dest, policy, Exists, out bool overwrite);
            if (resolved == null) continue;
            EnsureDirectory(Path.GetDirectoryName(resolved)!);
            if (overwrite) RecycleInternal(resolved, Err("Err.CannotOverwriteSafely", resolved));
            entry.ExtractToFile(resolved, overwrite: false);
            AddCreated(resolved);
            count++;
        }
        return count;
    }

    public void CreateShortcut(string target, string shortcutPath, bool overwrite)
    {
        if (overwrite && Exists(shortcutPath)) RecycleInternal(shortcutPath, Err("Err.CannotOverwriteSafely", shortcutPath));
        ShellLink.Create(target, shortcutPath);
        AddCreated(shortcutPath);
    }

    public void Launch(string application, string arguments)
    {
        using var p = Process.Start(new ProcessStartInfo(application, arguments) { UseShellExecute = true });
    }

    private void RecycleInternal(string path, string unsupportedMessage)
    {
        if (!_recycleBin.IsSupported(path)) throw new IOException(unsupportedMessage);
        RecycledItem? item = null;
        Retry(() => item = _recycleBin.Recycle(path));
        UndoSteps.Add(new UndoStep { Kind = UndoKind.RestoreRecycled, Path = path, Recycled = item });
    }

    private void AddCreated(string path)
    {
        var fi = new FileInfo(path);
        UndoSteps.Add(new UndoStep { Kind = UndoKind.DeleteCreated, Path = path, Length = fi.Length, LastWriteUtc = fi.LastWriteTimeUtc });
    }

    private static string Err(string key, string arg) => string.Format(Loc.T(key), arg);

    /// <summary>Retries on sharing/lock violations (file in use) with a short back-off.</summary>
    internal static void Retry(Action action, int attempts = 4)
    {
        for (int i = 1; ; i++)
        {
            try { action(); return; }
            catch (IOException ex) when (i < attempts && IsInUse(ex))
            {
                Thread.Sleep(250 * i);
            }
        }
    }

    public static bool IsInUse(IOException ex)
    {
        int code = ex.HResult & 0xFFFF;
        return code is 32 or 33; // ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION
    }
}

public sealed class SimulatedFileOps : IFileOps
{
    private readonly HashSet<string> _added = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);

    public bool Simulated => true;

    public bool Exists(string path) =>
        _added.Contains(path) || (!_removed.Contains(path) && (File.Exists(path) || Directory.Exists(path)));

    public void EnsureDirectory(string dir)
    {
        for (var d = dir; !string.IsNullOrEmpty(d) && !Exists(d); d = Path.GetDirectoryName(d)) Add(d);
    }

    public void Move(string source, string target, bool overwrite) { Remove(source); Add(target); }
    public void Copy(string source, string target, bool overwrite) => Add(target);
    public void Recycle(string path) => Remove(path);
    public void DeletePermanently(string path) => Remove(path);
    public void RemoveEmptyFolders(string dir, string? stopAt) { }
    public void CreateZip(string source, string zipPath, bool overwrite) => Add(zipPath);
    public void CreateShortcut(string target, string shortcutPath, bool overwrite) => Add(shortcutPath);
    public void Launch(string application, string arguments) { }

    public int ExtractZip(string zipPath, string destination, ConflictPolicy policy)
    {
        if (!File.Exists(zipPath)) return 0; // archive itself is only virtual (e.g. created earlier in the preview)
        int count = 0;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + "\\";
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            var dest = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException(string.Format(Loc.T("Err.ZipSlip"), entry.FullName));
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
            var resolved = ConflictResolver.Resolve(dest, policy, Exists, out _);
            if (resolved == null) continue;
            Add(resolved);
            count++;
        }
        return count;
    }

    private void Add(string p) { _removed.Remove(p); _added.Add(p); }
    private void Remove(string p) { _added.Remove(p); _removed.Add(p); }
}
