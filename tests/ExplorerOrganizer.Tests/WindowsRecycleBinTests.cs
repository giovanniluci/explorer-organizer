using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Tests;

/// <summary>Integration test against the real Recycle Bin: the file is recycled and immediately restored.</summary>
public class WindowsRecycleBinTests
{
    [Fact]
    public void Recycle_and_restore_roundtrip()
    {
        using var t = new TempDir();
        var bin = new WindowsRecycleBin();
        var path = t.File("eo-recycle-test-" + Guid.NewGuid().ToString("N") + ".txt", "keep me");
        if (!bin.IsSupported(path)) return; // temp folder not on a fixed drive: nothing to verify here

        var item = bin.Recycle(path);
        Assert.False(File.Exists(path));
        bin.Restore(item);
        Assert.True(File.Exists(path));
        Assert.Equal("keep me", File.ReadAllText(path));
    }

    [Fact]
    public void Network_paths_are_not_supported()
    {
        Assert.False(new WindowsRecycleBin().IsSupported(@"\\server\share\file.txt"));
    }
}
