using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Engine;

public static class ConflictResolver
{
    /// <summary>
    /// Decides the final destination when <paramref name="target"/> may already exist.
    /// Returns null when the operation must be skipped. <paramref name="overwrite"/> is true only when an
    /// existing item will be replaced (the engine sends it to the Recycle Bin first).
    /// </summary>
    public static string? Resolve(string target, ConflictPolicy policy, Func<string, bool> exists, out bool overwrite, bool isFolder = false)
    {
        overwrite = false;
        if (!exists(target)) return target;
        switch (policy)
        {
            case ConflictPolicy.Skip:
                return null;
            case ConflictPolicy.Overwrite:
                overwrite = true;
                return target;
            default:
                return NextFreeName(target, exists, isFolder);
        }
    }

    /// <summary>"report.pdf" =&gt; "report (2).pdf", "report (3).pdf", ... (a folder keeps its whole name: "Font.Family (2)")</summary>
    public static string NextFreeName(string target, Func<string, bool> exists, bool isFolder = false)
    {
        var dir = Path.GetDirectoryName(target) ?? "";
        var name = isFolder ? Path.GetFileName(target) : Path.GetFileNameWithoutExtension(target);
        var ext = isFolder ? "" : Path.GetExtension(target);
        for (int i = 2; i < 100_000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!exists(candidate)) return candidate;
        }
        throw new IOException("No free file name for " + target);
    }
}
