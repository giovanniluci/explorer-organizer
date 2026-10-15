namespace ExplorerOrganizer.Core.Platform;

/// <summary>Identifies an item sent to the Recycle Bin so it can be restored by Undo.</summary>
public sealed record RecycledItem(string OriginalPath, DateTime DeletedAtUtc, bool IsDirectory);

public interface IRecycleBin
{
    /// <summary>True when items under <paramref name="path"/> can be recycled (never silently destroyed).</summary>
    bool IsSupported(string path);

    /// <summary>Moves a file or folder to the Recycle Bin. Throws when it cannot be done safely.</summary>
    RecycledItem Recycle(string path);

    /// <summary>Puts a recycled item back at its original location. Throws on failure.</summary>
    void Restore(RecycledItem item);
}
