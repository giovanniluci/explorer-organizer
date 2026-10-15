using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Tests;

/// <summary>Temporary directory deleted at the end of each test.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Loc.SetLanguage("en");
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative, string content = "x", DateTime? modified = null)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        if (modified != null) System.IO.File.SetLastWriteTime(full, modified.Value);
        return full;
    }

    public string Combine(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}

/// <summary>Recycle Bin double: moves items into a private "trash" folder so tests never touch the real bin.</summary>
public sealed class FakeRecycleBin : IRecycleBin
{
    private readonly string _trash;
    private readonly Dictionary<string, string> _items = new(StringComparer.OrdinalIgnoreCase);

    public FakeRecycleBin(string trashDir)
    {
        _trash = trashDir;
        Directory.CreateDirectory(_trash);
    }

    public bool Supported { get; set; } = true;
    public IReadOnlyCollection<string> RecycledPaths => _items.Keys;

    public bool IsSupported(string path) => Supported;

    public RecycledItem Recycle(string path)
    {
        var dest = System.IO.Path.Combine(_trash, Guid.NewGuid().ToString("N"));
        if (Directory.Exists(path)) Directory.Move(path, dest);
        else File.Move(path, dest);
        _items[path] = dest;
        return new RecycledItem(path, DateTime.UtcNow, false);
    }

    public void Restore(RecycledItem item)
    {
        if (!_items.Remove(item.OriginalPath, out var stored)) throw new IOException("not in fake bin");
        if (Directory.Exists(stored)) Directory.Move(stored, item.OriginalPath);
        else File.Move(stored, item.OriginalPath);
    }
}

public sealed class FakeMetadata : IMetadataProvider
{
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string path, string canonicalName) => Values.TryGetValue(canonicalName, out var v) ? v : null;
}

public sealed class FakeContent : IContentReader
{
    public string Read(string path) => File.ReadAllText(path);
}

public static class Build
{
    public static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0);

    public static FileContext Context(string path, FakeMetadata? meta = null, string? root = null) =>
        new(path, root, meta ?? new FakeMetadata(), new FakeContent());

    public static RuleEngine Engine(IRecycleBin bin, FakeMetadata? meta = null) =>
        new(meta ?? new FakeMetadata(), new FakeContent(), bin, () => Now);

    public static Profile Profile(params Rule[] rules)
    {
        var p = new Profile { Name = "Test" };
        foreach (var r in rules) p.Rules.Add(r);
        return p;
    }

    public static Rule Rule(Condition condition, params RuleAction[] actions)
    {
        var r = new Rule { Name = "R" };
        r.Condition.Children.Add(condition);
        foreach (var a in actions) r.Actions.Add(a);
        return r;
    }
}
