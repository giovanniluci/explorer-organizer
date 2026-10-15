using System.Text.Json;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

public enum UndoKind { MoveBack, DeleteCreated, RestoreRecycled, RemoveCreatedDirectory, RecreateDirectory }

public sealed class UndoStep
{
    public UndoKind Kind { get; set; }
    /// <summary>Where the item is now (after the operation).</summary>
    public string Path { get; set; } = "";
    /// <summary>Where it must go back (MoveBack).</summary>
    public string? OriginalPath { get; set; }
    public RecycledItem? Recycled { get; set; }
    /// <summary>Fingerprint of created files, so Undo never removes a file the user changed afterwards.</summary>
    public long? Length { get; set; }
    public DateTime? LastWriteUtc { get; set; }
}

public sealed class UndoBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Time { get; set; } = DateTime.Now;
    public string Description { get; set; } = "";
    public int FileCount { get; set; }
    public bool HasIrreversibleSteps { get; set; }
    public List<UndoStep> Steps { get; set; } = new();
}

public sealed record UndoResult(int Reverted, IReadOnlyList<string> Errors);

/// <summary>Persistent list of reversible batches; "Undo" reverts the most recent one, step by step in reverse order.</summary>
public sealed class UndoJournal
{
    public const int MaxBatches = 50;
    private readonly string? _file;
    private readonly object _lock = new();
    private List<UndoBatch> _batches = new();

    public UndoJournal(string? file)
    {
        _file = file;
        if (file != null && File.Exists(file))
        {
            try { _batches = JsonSerializer.Deserialize<List<UndoBatch>>(File.ReadAllText(file), Json.Options) ?? new(); }
            catch { _batches = new(); }
        }
    }

    public event EventHandler? Changed;

    public UndoBatch? Last { get { lock (_lock) return _batches.Count == 0 ? null : _batches[^1]; } }

    public int Count { get { lock (_lock) return _batches.Count; } }

    /// <summary>All batches, newest first.</summary>
    public IReadOnlyList<UndoBatch> Snapshot() { lock (_lock) return _batches.AsEnumerable().Reverse().ToList(); }

    public void Push(UndoBatch batch)
    {
        if (batch.Steps.Count == 0) return;
        lock (_lock)
        {
            _batches.Add(batch);
            if (_batches.Count > MaxBatches) _batches.RemoveRange(0, _batches.Count - MaxBatches);
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public UndoResult UndoLast(IRecycleBin recycleBin)
    {
        UndoBatch? batch;
        lock (_lock)
        {
            if (_batches.Count == 0) return new UndoResult(0, new[] { Loc.T("Undo.Nothing") });
            batch = _batches[^1];
            _batches.RemoveAt(_batches.Count - 1);
            Save();
        }
        var result = Revert(batch, recycleBin);
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public static UndoResult Revert(UndoBatch batch, IRecycleBin recycleBin)
    {
        var errors = new List<string>();
        int ok = 0;
        for (int i = batch.Steps.Count - 1; i >= 0; i--)
        {
            var s = batch.Steps[i];
            try
            {
                switch (s.Kind)
                {
                    case UndoKind.MoveBack:
                        bool isDir = Directory.Exists(s.Path);
                        if (!isDir && !File.Exists(s.Path)) throw new IOException(string.Format(Loc.T("Err.UndoMissing"), s.Path));
                        if (File.Exists(s.OriginalPath) || Directory.Exists(s.OriginalPath))
                            throw new IOException(string.Format(Loc.T("Err.TargetExists"), s.OriginalPath));
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(s.OriginalPath!)!);
                        if (isDir) Directory.Move(s.Path, s.OriginalPath!);
                        else File.Move(s.Path, s.OriginalPath!);
                        break;
                    case UndoKind.DeleteCreated:
                        if (!File.Exists(s.Path)) break;
                        var fi = new FileInfo(s.Path);
                        if (s.Length.HasValue && (fi.Length != s.Length || Math.Abs((fi.LastWriteTimeUtc - s.LastWriteUtc!.Value).TotalSeconds) > 2))
                            throw new IOException(string.Format(Loc.T("Err.UndoChanged"), s.Path));
                        if (recycleBin.IsSupported(s.Path)) recycleBin.Recycle(s.Path);
                        else File.Delete(s.Path); // unchanged copy we created ourselves on a drive without Recycle Bin
                        break;
                    case UndoKind.RestoreRecycled:
                        recycleBin.Restore(s.Recycled ?? throw new IOException("missing recycle info"));
                        break;
                    case UndoKind.RemoveCreatedDirectory:
                        if (Directory.Exists(s.Path) && !Directory.EnumerateFileSystemEntries(s.Path).Any())
                            Directory.Delete(s.Path);
                        break;
                    case UndoKind.RecreateDirectory:
                        Directory.CreateDirectory(s.Path);
                        break;
                }
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }
        return new UndoResult(ok, errors);
    }

    private void Save()
    {
        if (_file == null) return;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_batches, Json.Options));
            File.Move(tmp, _file, overwrite: true);
        }
        catch { /* journal persistence is best effort: in-memory undo still works */ }
    }
}
