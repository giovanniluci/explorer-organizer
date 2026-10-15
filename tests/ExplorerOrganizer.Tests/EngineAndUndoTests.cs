using System.IO.Compression;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Tests;

public class EngineAndUndoTests
{
    private static RunResult Run(TempDir t, FakeRecycleBin bin, Profile p, bool simulate = false, string? root = null, Func<bool>? confirm = null, params string[] files) =>
        Build.Engine(bin).Run(files.Select(f => new FileInput(f, root)), p, new RunOptions { Simulate = simulate, ConfirmPermanentDelete = confirm });

    [Fact]
    public void First_matching_rule_wins_and_disabled_rules_are_ignored()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.jpg");
        var disabled = Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("Disabled") });
        disabled.Enabled = false;
        var images = Build.Rule(new FileTypeCondition { Category = FileCategory.Image }, new MoveAction { Destination = t.Combine("Img") });
        var all = Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("All") });
        Run(t, bin, Build.Profile(disabled, images, all), files: src);
        Assert.True(File.Exists(t.Combine("Img", "a.jpg")));
        Assert.False(Directory.Exists(t.Combine("All")));
        Assert.False(Directory.Exists(t.Combine("Disabled")));
    }

    [Fact]
    public void Simulation_changes_nothing_and_matches_real_run()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var a = t.File("a.txt");
        var b = t.File("b.txt");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(),
            new RenameAction { NewName = "doc_{counter:2}.{ext}" },
            new MoveAction { Destination = t.Combine("Out") }));

        var preview = Run(t, bin, p, simulate: true, files: new[] { a, b });
        Assert.True(File.Exists(a) && File.Exists(b));
        Assert.False(Directory.Exists(t.Combine("Out")));
        Assert.All(preview.Records, r => Assert.Equal(OperationStatus.Planned, r.Status));
        Assert.Null(preview.Undo);

        var real = Run(t, bin, p, files: new[] { a, b });
        Assert.Equal(preview.Records.Select(r => r.Target), real.Records.Select(r => r.Target));
        Assert.True(File.Exists(t.Combine("Out", "doc_01.txt")));
        Assert.True(File.Exists(t.Combine("Out", "doc_02.txt")));
    }

    [Fact]
    public void Simulation_detects_conflicts_between_files_of_the_same_run()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var a = t.File(@"x\same.txt");
        var b = t.File(@"y\same.txt");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("Out") }));
        var preview = Run(t, bin, p, simulate: true, files: new[] { a, b });
        Assert.Equal(t.Combine("Out", "same.txt"), preview.Records[0].Target);
        Assert.Equal(t.Combine("Out", "same (2).txt"), preview.Records[1].Target);
    }

    [Fact]
    public void Undo_reverts_move_rename_and_removes_created_folders()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("photo.jpg", "data");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(),
            new RenameAction { NewName = "renamed.{ext}" },
            new MoveAction { Destination = t.Combine("A", "B") }));
        var result = Run(t, bin, p, files: src);
        Assert.True(File.Exists(t.Combine("A", "B", "renamed.jpg")));

        var journal = new UndoJournal(null);
        journal.Push(result.Undo!);
        var undo = journal.UndoLast(bin);
        Assert.Empty(undo.Errors);
        Assert.True(File.Exists(src));
        Assert.Equal("data", File.ReadAllText(src));
        Assert.False(Directory.Exists(t.Combine("A")));
        Assert.Equal(0, journal.Count);
    }

    [Fact]
    public void Undo_restores_overwritten_file_from_recycle_bin()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"in\a.txt", "new");
        var existing = t.File(@"out\a.txt", "old");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(),
            new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Overwrite }));
        var result = Run(t, bin, p, files: src);
        var undo = UndoJournal.Revert(result.Undo!, bin);
        Assert.Empty(undo.Errors);
        Assert.Equal("new", File.ReadAllText(src));
        Assert.Equal("old", File.ReadAllText(existing));
    }

    [Fact]
    public void Undo_of_copy_recycles_the_copy_but_not_if_modified()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt", "v1");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new CopyAction { Destination = t.Combine("Backup") }));

        var r1 = Run(t, bin, p, files: src);
        Assert.Empty(UndoJournal.Revert(r1.Undo!, bin).Errors);
        Assert.False(File.Exists(t.Combine("Backup", "a.txt")));
        Assert.True(File.Exists(src));

        var r2 = Run(t, bin, p, files: src);
        File.AppendAllText(t.Combine("Backup", "a.txt"), " user edit");
        var undo = UndoJournal.Revert(r2.Undo!, bin);
        Assert.NotEmpty(undo.Errors);
        Assert.True(File.Exists(t.Combine("Backup", "a.txt"))); // user's change is never thrown away
    }

    [Fact]
    public void Delete_goes_to_recycle_bin_by_default_and_undo_restores()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("junk.tmp", "j");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new DeleteAction()));
        var r = Run(t, bin, p, files: src);
        Assert.False(File.Exists(src));
        Assert.Contains(src, bin.RecycledPaths);
        Assert.Equal(OperationKind.Recycle, r.Records.Single().Kind);
        UndoJournal.Revert(r.Undo!, bin);
        Assert.True(File.Exists(src));
    }

    [Fact]
    public void Permanent_delete_without_confirmation_falls_back_to_recycle_bin()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("junk.tmp");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new DeleteAction { Permanent = true }));
        var r = Run(t, bin, p, confirm: () => false, files: src);
        Assert.Contains(src, bin.RecycledPaths);
        Assert.Equal(OperationKind.Recycle, r.Records.Single().Kind);

        var src2 = t.File("junk2.tmp");
        var r2 = Run(t, bin, p, confirm: null, files: src2); // background trigger: no way to confirm
        Assert.Contains(src2, bin.RecycledPaths);
    }

    [Fact]
    public void Permanent_delete_with_confirmation_is_asked_once_and_marked_irreversible()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var a = t.File("a.tmp");
        var b = t.File("b.tmp");
        int asked = 0;
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("Stage") }, new DeleteAction { Permanent = true }));
        var r = Run(t, bin, p, confirm: () => { asked++; return true; }, files: new[] { a, b });
        Assert.Equal(1, asked);
        Assert.False(File.Exists(t.Combine("Stage", "a.tmp")));
        Assert.Empty(bin.RecycledPaths);
        Assert.True(r.Undo!.HasIrreversibleSteps);
    }

    [Fact]
    public void Delete_is_refused_where_recycle_bin_is_unavailable()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash")) { Supported = false };
        var src = t.File("keep.txt");
        var r = Run(t, bin, Build.Profile(Build.Rule(new AllFilesCondition(), new DeleteAction())), files: src);
        Assert.True(File.Exists(src));
        Assert.Equal(OperationStatus.Failed, r.Records.Single().Status);
    }

    [Fact]
    public void Sort_into_subfolders_by_type_extension_letter_and_date()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var img = t.File("cat.png");
        var doc = t.File("notes.docx");
        var odd = t.File("zeta.xyz", modified: new DateTime(2020, 2, 3));
        var other = t.File("_x.dat");
        Run(t, bin, Build.Profile(Build.Rule(new FileTypeCondition { Category = FileCategory.Image }, new SortIntoSubfoldersAction { Mode = SortMode.ByType })), files: img);
        Run(t, bin, Build.Profile(Build.Rule(new ExtensionCondition { Extensions = "docx" }, new SortIntoSubfoldersAction { Mode = SortMode.ByExtension })), files: doc);
        Run(t, bin, Build.Profile(Build.Rule(new ExtensionCondition { Extensions = "xyz" }, new SortIntoSubfoldersAction { Mode = SortMode.ByDate, DateFormat = "yyyy/MM" })), files: odd);
        Run(t, bin, Build.Profile(Build.Rule(new ExtensionCondition { Extensions = "dat" }, new SortIntoSubfoldersAction { Mode = SortMode.ByFirstLetter })), files: other);
        Assert.True(File.Exists(t.Combine("Images", "cat.png")));
        Assert.True(File.Exists(t.Combine("DOCX", "notes.docx")));
        Assert.True(File.Exists(t.Combine("2020", "02", "zeta.xyz")));
        Assert.True(File.Exists(t.Combine("#", "_x.dat")));
    }

    [Fact]
    public void Compress_and_extract_zip_with_undo()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("report.txt", "hello");
        var r = Run(t, bin, Build.Profile(Build.Rule(new AllFilesCondition(), new CompressAction { ArchivePath = t.Combine("zips", "{name}") })), files: src);
        var zip = t.Combine("zips", "report.zip");
        Assert.True(File.Exists(zip));
        using (var z = ZipFile.OpenRead(zip)) Assert.Equal("report.txt", z.Entries.Single().FullName);

        var x = Run(t, bin, Build.Profile(Build.Rule(new ExtensionCondition { Extensions = "zip" }, new ExtractAction { Destination = t.Combine("unz") })), files: zip);
        Assert.Equal("hello", File.ReadAllText(t.Combine("unz", "report.txt")));
        UndoJournal.Revert(x.Undo!, bin);
        Assert.False(Directory.Exists(t.Combine("unz")));
        UndoJournal.Revert(r.Undo!, bin);
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void Extract_blocks_zip_slip()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var zip = t.Combine("evil.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(z.CreateEntry("../../escaped.txt").Open());
            w.Write("bad");
        }
        var r = Run(t, bin, Build.Profile(Build.Rule(new AllFilesCondition(), new ExtractAction { Destination = t.Combine("out") })), files: zip);
        Assert.Equal(OperationStatus.Failed, r.Records.Single().Status);
        Assert.False(File.Exists(System.IO.Path.Combine(t.Path, "..", "..", "escaped.txt")));
    }

    [Fact]
    public void Remove_empty_source_folder_never_removes_the_root()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var root = t.Combine("watch");
        var src = t.File(@"watch\deep\er\a.txt");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out"), RemoveEmptySourceFolder = true }));
        var r = Run(t, bin, p, root: root, files: src);
        Assert.False(Directory.Exists(t.Combine("watch", "deep")));
        Assert.True(Directory.Exists(root));
        UndoJournal.Revert(r.Undo!, bin);
        Assert.True(File.Exists(src));
    }

    [Fact]
    public void Locked_file_is_reported_and_other_files_continue()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var locked = t.File("locked.txt");
        var free = t.File("free.txt");
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out") }));
        RunResult r;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            r = Run(t, bin, p, files: new[] { locked, free });
        Assert.True(File.Exists(locked));
        Assert.True(File.Exists(t.Combine("out", "free.txt")));
        Assert.Contains(r.Records, x => x.Status == OperationStatus.Failed && x.FilePath == locked);
    }

    [Fact]
    public void Long_paths_are_supported()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt");
        var deep = t.Combine(Enumerable.Range(0, 12).Select(i => new string((char)('a' + i), 25)).ToArray());
        var p = Build.Profile(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = deep }));
        var r = Run(t, bin, p, files: src);
        Assert.True(deep.Length > 260);
        Assert.True(File.Exists(System.IO.Path.Combine(deep, "a.txt")));
        Assert.Empty(UndoJournal.Revert(r.Undo!, bin).Errors);
        Assert.True(File.Exists(src));
    }

    [Fact]
    public void Journal_persists_and_caps_batches()
    {
        using var t = new TempDir();
        var file = t.Combine("journal.json");
        var j = new UndoJournal(file);
        for (int i = 0; i < UndoJournal.MaxBatches + 5; i++)
            j.Push(new UndoBatch { Description = "b" + i, Steps = { new UndoStep { Kind = UndoKind.RecreateDirectory, Path = t.Combine("d" + i) } } });
        var reloaded = new UndoJournal(file);
        Assert.Equal(UndoJournal.MaxBatches, reloaded.Count);
        Assert.Equal("b" + (UndoJournal.MaxBatches + 4), reloaded.Last!.Description);
        j.Push(new UndoBatch()); // empty batches are ignored
        Assert.Equal(UndoJournal.MaxBatches, j.Count);
    }

    [Fact]
    public void Recycle_bin_info_file_parser_v2_and_v1()
    {
        var path = @"C:\Users\me\Downloads\file.txt";
        var ft = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToFileTimeUtc();
        var name = System.Text.Encoding.Unicode.GetBytes(path + "\0");
        var v2 = BitConverter.GetBytes(2L).Concat(BitConverter.GetBytes(10L)).Concat(BitConverter.GetBytes(ft))
            .Concat(BitConverter.GetBytes(path.Length + 1)).Concat(name).ToArray();
        Assert.True(WindowsRecycleBin.TryParseInfoFile(v2, out var p2, out var d2));
        Assert.Equal(path, p2);
        Assert.Equal(2026, d2.Year);

        var v1 = new byte[24 + 520];
        BitConverter.GetBytes(1L).CopyTo(v1, 0);
        BitConverter.GetBytes(ft).CopyTo(v1, 16);
        name.CopyTo(v1, 24);
        Assert.True(WindowsRecycleBin.TryParseInfoFile(v1, out var p1, out _));
        Assert.Equal(path, p1);

        Assert.False(WindowsRecycleBin.TryParseInfoFile(new byte[10], out _, out _));
    }
}
