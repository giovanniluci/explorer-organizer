using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Tests;

public class ConflictTests
{
    [Fact]
    public void No_conflict_returns_target()
    {
        Assert.Equal(@"C:\x\a.txt", ConflictResolver.Resolve(@"C:\x\a.txt", ConflictPolicy.Skip, _ => false, out var ow));
        Assert.False(ow);
    }

    [Fact]
    public void Rename_new_finds_first_free_number()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\x\a.txt", @"C:\x\a (2).txt" };
        Assert.Equal(@"C:\x\a (3).txt", ConflictResolver.Resolve(@"C:\x\a.txt", ConflictPolicy.RenameNew, taken.Contains, out var ow));
        Assert.False(ow);
    }

    [Fact]
    public void Skip_returns_null_and_overwrite_flags()
    {
        Assert.Null(ConflictResolver.Resolve(@"C:\x\a.txt", ConflictPolicy.Skip, _ => true, out _));
        Assert.Equal(@"C:\x\a.txt", ConflictResolver.Resolve(@"C:\x\a.txt", ConflictPolicy.Overwrite, _ => true, out var ow));
        Assert.True(ow);
    }

    [Fact]
    public void Engine_rename_new_keeps_both_files()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"in\a.txt", "new");
        t.File(@"out\a.txt", "old");
        var profile = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out") }));
        var result = Build.Engine(bin).Run(new[] { new FileInput(src) }, profile, new RunOptions());
        Assert.Equal("old", File.ReadAllText(t.Combine("out", "a.txt")));
        Assert.Equal("new", File.ReadAllText(t.Combine("out", "a (2).txt")));
        Assert.Equal(OperationStatus.Done, result.Records.Single().Status);
    }

    [Fact]
    public void Engine_skip_leaves_everything_untouched_and_stops_chain()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"in\a.txt", "new");
        t.File(@"out\a.txt", "old");
        var profile = Build.Profile(Build.Rule(new AllFilesCondition(),
            new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Skip },
            new DeleteAction()));
        var result = Build.Engine(bin).Run(new[] { new FileInput(src) }, profile, new RunOptions());
        Assert.True(File.Exists(src));
        Assert.Equal("old", File.ReadAllText(t.Combine("out", "a.txt")));
        Assert.Equal(OperationStatus.Skipped, result.Records.Single().Status);
        Assert.Empty(bin.RecycledPaths);
    }

    [Fact]
    public void Engine_overwrite_sends_existing_file_to_recycle_bin()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"in\a.txt", "new");
        var existing = t.File(@"out\a.txt", "old");
        var profile = Build.Profile(Build.Rule(new AllFilesCondition(),
            new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Overwrite }));
        Build.Engine(bin).Run(new[] { new FileInput(src) }, profile, new RunOptions());
        Assert.Equal("new", File.ReadAllText(existing));
        Assert.Contains(existing, bin.RecycledPaths);
    }

    [Fact]
    public void Engine_overwrite_is_refused_without_recycle_bin()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash")) { Supported = false };
        var src = t.File(@"in\a.txt", "new");
        var existing = t.File(@"out\a.txt", "old");
        var profile = Build.Profile(Build.Rule(new AllFilesCondition(),
            new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Overwrite }));
        var result = Build.Engine(bin).Run(new[] { new FileInput(src) }, profile, new RunOptions());
        Assert.Equal("old", File.ReadAllText(existing));
        Assert.True(File.Exists(src));
        Assert.Equal(OperationStatus.Failed, result.Records.Single().Status);
    }

    [Fact]
    public void Moving_to_same_place_is_unchanged_not_renamed()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"Images\a.png");
        var profile = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("Images") }));
        var result = Build.Engine(bin).Run(new[] { new FileInput(src) }, profile, new RunOptions());
        Assert.Equal(OperationStatus.Unchanged, result.Records.Single().Status);
        Assert.Single(Directory.GetFiles(t.Combine("Images")));
    }
}
