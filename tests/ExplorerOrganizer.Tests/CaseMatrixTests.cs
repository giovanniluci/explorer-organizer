using System.Security.AccessControl;
using System.Security.Principal;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.Tests;

/// <summary>
/// One explicit test per case of docs/test-matrix.md (conditions, actions, tokens, conflicts, triggers, safety).
/// Some cases are also exercised, in more depth, by the older focused test classes.
/// </summary>
public class CaseMatrixTests
{
    private static bool Eval(Condition c, FileContext f) => ConditionEvaluator.Evaluate(c, f, Build.Now);

    private static RunResult Run(FakeRecycleBin bin, Profile p, IEnumerable<string> files, bool simulate = false, string? root = null, FakeMetadata? meta = null) =>
        Build.Engine(bin, meta).Run(files.Select(f => new FileInput(f, root)), p, new RunOptions { Simulate = simulate });

    private static Profile One(Condition c, params RuleAction[] actions) => Build.Profile(Build.Rule(c, actions));

    // ------------------------------------------------------------------ conditions

    [Theory]
    [InlineData(NameTarget.Name, TextOperator.Is, "Report Q3", true)]
    [InlineData(NameTarget.NameWithExtension, TextOperator.EndsWith, "Q3.pdf", true)]
    [InlineData(NameTarget.FullPath, TextOperator.Contains, @"\inbox\", true)]
    [InlineData(NameTarget.FolderPath, TextOperator.EndsWith, "inbox", true)]
    [InlineData(NameTarget.FolderPath, TextOperator.Contains, "Report", false)]
    public void Condition_name_targets(NameTarget target, TextOperator op, string value, bool expected)
    {
        using var t = new TempDir();
        var f = Build.Context(t.File(@"inbox\Report Q3.pdf"));
        Assert.Equal(expected, Eval(new NameCondition { Target = target, Operator = op, Value = value }, f));
    }

    [Fact]
    public void Condition_name_is_empty_operator()
    {
        using var t = new TempDir();
        Assert.False(Eval(new NameCondition { Operator = TextOperator.IsEmpty }, Build.Context(t.File("a.txt"))));
    }

    [Fact]
    public void Condition_extension()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("photo.JPEG"));
        Assert.True(Eval(new ExtensionCondition { Extensions = "jpg; .jpeg ;png" }, f));
        Assert.False(Eval(new ExtensionCondition { Extensions = "pdf" }, f));
    }

    [Theory]
    [InlineData(SizeComparison.EqualTo, 2048, SizeUnit.Bytes, true)]
    [InlineData(SizeComparison.EqualTo, 2, SizeUnit.KB, true)]
    [InlineData(SizeComparison.GreaterThan, 1, SizeUnit.KB, true)]
    [InlineData(SizeComparison.LessThan, 1, SizeUnit.MB, true)]
    [InlineData(SizeComparison.GreaterThan, 1, SizeUnit.GB, false)]
    public void Condition_size_units_and_comparisons(SizeComparison cmp, double value, SizeUnit unit, bool expected)
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("blob.bin", new string('x', 2048)));
        Assert.Equal(expected, Eval(new SizeCondition { Comparison = cmp, Value = value, Unit = unit }, f));
    }

    [Theory]
    [InlineData(DateField.Created)]
    [InlineData(DateField.Modified)]
    [InlineData(DateField.Accessed)]
    public void Condition_date_fields(DateField field)
    {
        using var t = new TempDir();
        var path = t.File("old.txt");
        var old = new DateTime(2019, 5, 1, 10, 0, 0);
        File.SetCreationTime(path, old);
        File.SetLastWriteTime(path, old);
        File.SetLastAccessTime(path, old);
        var f = Build.Context(path);
        Assert.True(Eval(new DateCondition { Field = field, Mode = DateMode.OlderThan, Amount = 1, Unit = TimeUnit.Years }, f));
        Assert.False(Eval(new DateCondition { Field = field, Mode = DateMode.NewerThan, Amount = 30, Unit = TimeUnit.Days }, f));
        Assert.True(Eval(new DateCondition { Field = field, Mode = DateMode.Before, Date = new DateTime(2020, 1, 1) }, f));
        Assert.True(Eval(new DateCondition { Field = field, Mode = DateMode.After, Date = new DateTime(2018, 12, 31) }, f));
    }

    [Fact]
    public void Condition_date_taken_exif()
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values["System.Photo.DateTaken"] = new DateTime(2015, 6, 1);
        var f = Build.Context(t.File("p.jpg"), meta);
        Assert.True(Eval(new DateCondition { Field = DateField.Taken, Mode = DateMode.Before, Date = new DateTime(2016, 1, 1) }, f));
    }

    [Theory]
    [InlineData("a.png", FileCategory.Image)]
    [InlineData("a.mkv", FileCategory.Video)]
    [InlineData("a.flac", FileCategory.Audio)]
    [InlineData("a.docx", FileCategory.Document)]
    [InlineData("a.rar", FileCategory.Archive)]
    [InlineData("a.cs", FileCategory.Code)]
    [InlineData("a.msi", FileCategory.Executable)]
    [InlineData("a.qqq", FileCategory.Other)]
    public void Condition_file_type_each_category(string name, FileCategory category)
    {
        using var t = new TempDir();
        var f = Build.Context(t.File(name));
        Assert.True(Eval(new FileTypeCondition { Category = category }, f));
        var other = category == FileCategory.Image ? FileCategory.Video : FileCategory.Image;
        Assert.False(Eval(new FileTypeCondition { Category = other }, f));
    }

    [Theory]
    [InlineData(FileAttributeKind.Hidden, FileAttributes.Hidden)]
    [InlineData(FileAttributeKind.ReadOnly, FileAttributes.ReadOnly)]
    [InlineData(FileAttributeKind.System, FileAttributes.System)]
    [InlineData(FileAttributeKind.Archive, FileAttributes.Archive)]
    public void Condition_attribute_each_kind(FileAttributeKind kind, FileAttributes attr)
    {
        using var t = new TempDir();
        var path = t.File("f.txt");
        File.SetAttributes(path, attr);
        try
        {
            Assert.True(Eval(new AttributeCondition { Attribute = kind }, Build.Context(path)));
            File.SetAttributes(path, FileAttributes.Normal);
            Assert.False(Eval(new AttributeCondition { Attribute = kind }, Build.Context(path)));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }

    [Fact]
    public void Condition_metadata()
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values["System.Photo.CameraModel"] = "NEX-7";
        var f = Build.Context(t.File("p.jpg"), meta);
        Assert.True(Eval(new MetadataCondition { Property = "exif:CameraModel", Operator = TextOperator.StartsWith, Value = "NEX" }, f));
        Assert.False(Eval(new MetadataCondition { Property = "exif:CameraModel", Operator = TextOperator.Is, Value = "EOS" }, f));
    }

    [Fact]
    public void Condition_content()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("n.txt", "Fattura n. 77 – ACME S.p.A."));
        Assert.True(Eval(new ContentCondition { Operator = TextOperator.Contains, Value = "acme" }, f));
        Assert.False(Eval(new ContentCondition { Operator = TextOperator.Contains, Value = "globex" }, f));
    }

    [Fact]
    public void Condition_all_files()
    {
        using var t = new TempDir();
        Assert.True(Eval(new AllFilesCondition(), Build.Context(t.File("anything.xyz"))));
    }

    [Fact]
    public void Condition_group_and()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.pdf", "abc"));
        var g = new ConditionGroup { Operator = GroupOperator.All, Children = { new ExtensionCondition { Extensions = "pdf" }, new SizeCondition { Comparison = SizeComparison.LessThan, Value = 1, Unit = SizeUnit.KB } } };
        Assert.True(Eval(g, f));
        g.Children.Add(new NameCondition { Operator = TextOperator.Contains, Value = "zzz" });
        Assert.False(Eval(g, f));
    }

    [Fact]
    public void Condition_group_or()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.pdf"));
        var g = new ConditionGroup { Operator = GroupOperator.Any, Children = { new ExtensionCondition { Extensions = "jpg" }, new ExtensionCondition { Extensions = "pdf" } } };
        Assert.True(Eval(g, f));
        g.Children.RemoveAt(1);
        Assert.False(Eval(g, f));
    }

    [Fact]
    public void Conditions_have_no_not_flag_anymore()
    {
        Assert.Null(typeof(Condition).GetProperty("Negate"));
        var json = System.Text.Json.JsonSerializer.Serialize<Condition>(new ExtensionCondition { Extensions = "pdf" }, Json.Options);
        Assert.DoesNotContain("negate", json);
    }

    // ------------------------------------------------------------------ actions

    [Fact]
    public void Action_move()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt");
        Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out", "{ext}") }), new[] { src });
        Assert.False(File.Exists(src));
        Assert.True(File.Exists(t.Combine("out", "txt", "a.txt")));
    }

    [Fact]
    public void Action_copy()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt", "data");
        Run(bin, One(new AllFilesCondition(), new CopyAction { Destination = t.Combine("bak") }), new[] { src });
        Assert.True(File.Exists(src));
        Assert.Equal("data", File.ReadAllText(t.Combine("bak", "a.txt")));
    }

    [Fact]
    public void Action_rename_and_change_extension()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("IMG_1.jpeg", modified: new DateTime(2024, 2, 29));
        Run(bin, One(new AllFilesCondition(), new RenameAction { NewName = "{modified:yyyy-MM-dd} {name|lower}.jpg" }), new[] { src });
        Assert.True(File.Exists(t.Combine("2024-02-29 img_1.jpg")));
    }

    [Fact]
    public void Action_sort_into_subfolders()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("song.mp3");
        Run(bin, One(new AllFilesCondition(), new SortIntoSubfoldersAction { BaseFolder = t.Combine("lib"), Mode = SortMode.ByExtension }), new[] { src });
        Assert.True(File.Exists(t.Combine("lib", "MP3", "song.mp3")));
    }

    [Fact]
    public void Action_delete_to_recycle_bin()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("junk.tmp");
        var r = Run(bin, One(new AllFilesCondition(), new DeleteAction()), new[] { src });
        Assert.False(File.Exists(src));
        Assert.Contains(src, bin.RecycledPaths);
        Assert.Equal(OperationKind.Recycle, r.Records.Single().Kind);
    }

    [Fact]
    public void Action_delete_permanent_requires_confirmation()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("junk.tmp");
        var r = Build.Engine(bin).Run(new[] { new FileInput(src) }, One(new AllFilesCondition(), new DeleteAction { Permanent = true }),
            new RunOptions { ConfirmPermanentDelete = () => true });
        Assert.False(File.Exists(src));
        Assert.Empty(bin.RecycledPaths);
        Assert.Equal(OperationKind.Delete, r.Records.Single().Kind);
    }

    [Fact]
    public void Action_compress_zip()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("r.txt", "hi");
        Run(bin, One(new AllFilesCondition(), new CompressAction { ArchivePath = t.Combine("{name}"), DeleteSource = true }), new[] { src });
        Assert.True(File.Exists(t.Combine("r.zip")));
        Assert.False(File.Exists(src));
        Assert.Contains(src, bin.RecycledPaths); // "delete source" goes to the Recycle Bin
    }

    [Fact]
    public void Action_extract_zip()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var zip = t.Combine("in.zip");
        using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
        using (var w = new StreamWriter(z.CreateEntry("dir/x.txt").Open())) w.Write("x");
        Run(bin, One(new ExtensionCondition { Extensions = "zip" }, new ExtractAction { Destination = t.Combine("{name}") }), new[] { zip });
        Assert.Equal("x", File.ReadAllText(t.Combine("in", "dir", "x.txt")));
    }

    [Fact]
    public void Action_create_shortcut()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("target.txt");
        var r = Run(bin, One(new AllFilesCondition(), new CreateShortcutAction { Destination = t.Combine("links"), ShortcutName = "{name}" }), new[] { src });
        var lnk = t.Combine("links", "target.lnk");
        Assert.True(File.Exists(lnk), string.Join("; ", r.Records.Select(x => x.Message)));
        Assert.True(File.Exists(src));
        UndoJournal.Revert(r.Undo!, bin); // undo removes the created shortcut
        Assert.False(File.Exists(lnk));
    }

    [Fact]
    public void Action_open_with_runs_program_with_rendered_arguments()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("hello.txt");
        var marker = t.Combine("marker.txt");
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var r = Run(bin, One(new AllFilesCondition(), new OpenWithAction { Application = cmd, Arguments = "/c echo {fullname}> \"" + marker + "\"" }), new[] { src });
        Assert.Equal(OperationStatus.Done, r.Records.Single().Status);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!File.Exists(marker) && DateTime.UtcNow < deadline) Thread.Sleep(100);
        Thread.Sleep(200);
        Assert.Equal("hello.txt", File.ReadAllText(marker).Trim());
    }

    [Fact]
    public void Action_chain_runs_in_order_on_the_same_file()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt");
        Run(bin, One(new AllFilesCondition(), new CopyAction { Destination = t.Combine("bak") }, new RenameAction { NewName = "b.{ext}" }, new MoveAction { Destination = t.Combine("out") }), new[] { src });
        Assert.True(File.Exists(t.Combine("bak", "a.txt")));
        Assert.True(File.Exists(t.Combine("out", "b.txt")));
    }

    // ------------------------------------------------------------------ tokens

    private static FileContext TokenFile(TempDir t, FakeMetadata? meta = null)
    {
        var path = t.File(@"Sub\Report Q3.Txt", "abc", modified: new DateTime(2021, 3, 9, 8, 5, 0));
        File.SetCreationTime(path, new DateTime(2019, 1, 2));
        File.SetLastAccessTime(path, new DateTime(2020, 12, 31));
        return Build.Context(path, meta, root: t.Path);
    }

    [Theory]
    [InlineData("{name}", "Report Q3")]
    [InlineData("{ext}", "Txt")]
    [InlineData("{fullname}", "Report Q3.Txt")]
    [InlineData("{parent}", "Sub")]
    [InlineData("{date}", "2026-10-14")]
    [InlineData("{date:dd.MM.yy}", "14.10.26")]
    [InlineData("{now:yyyy}", "2026")]
    [InlineData("{time}", "12.00.00")]
    [InlineData("{time:HHmm}", "1200")]
    [InlineData("{created:yyyy-MM-dd}", "2019-01-02")]
    [InlineData("{modified:yyyy-MM-dd HH.mm}", "2021-03-09 08.05")]
    [InlineData("{accessed:yyyy}", "2020")]
    [InlineData("{taken:yyyy}", "2021")]
    [InlineData("{counter}", "1")]
    [InlineData("{counter:4}", "0001")]
    [InlineData("{size}", "3 B")]
    [InlineData("{bytes}", "3")]
    [InlineData("{hash:md5}", "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData("{hash:sha1}", "a9993e364706816aba3e25717850c26c9cd0d89d")]
    [InlineData("{hash:sha256}", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("{name|upper}", "REPORT Q3")]
    [InlineData("{name|lower}", "report q3")]
    [InlineData("{name|lower|title}", "Report Q3")]
    [InlineData("{name|left:3}", "Rep")]
    [InlineData("{name|trim}", "Report Q3")]
    [InlineData("{id3:Album|default:Sconosciuto}", "Sconosciuto")]
    [InlineData("{{literal}}", "{literal}")]
    public void Token_renders_in_file_names(string template, string expected)
    {
        using var t = new TempDir();
        Assert.Equal(expected, TokenRenderer.RenderFileName(template, TokenFile(t), new RunState(), Build.Now));
    }

    [Fact]
    public void Token_category()
    {
        using var t = new TempDir();
        Assert.Equal("Documents", TokenRenderer.RenderFileName("{category}", TokenFile(t), new RunState(), Build.Now));
    }

    [Theory]
    [InlineData("exif:CameraModel", "System.Photo.CameraModel", "NEX-7")]
    [InlineData("id3:Artist", "System.Music.Artist", "Queen")]
    [InlineData("doc:Author", "System.Author", "Ada")]
    [InlineData("prop:System.Photo.FNumber", "System.Photo.FNumber", "5.6")]
    public void Token_metadata(string token, string canonical, string value)
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values[canonical] = value;
        Assert.Equal(value, TokenRenderer.RenderFileName("{" + token + "}", TokenFile(t, meta), new RunState(), Build.Now));
    }

    [Fact]
    public void Token_match_from_regex_condition()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("INV-2026-0042.pdf"));
        Assert.True(Eval(new NameCondition { Operator = TextOperator.Regex, Value = @"INV-(\d{4})-(?<num>\d+)" }, f));
        Assert.Equal("2026_0042", TokenRenderer.RenderFileName("{match:1}_{match:num}", f, new RunState(), Build.Now));
    }

    [Fact]
    public void Token_env_and_percent_variables()
    {
        using var t = new TempDir();
        Environment.SetEnvironmentVariable("EO_MATRIX_VAR", "valore");
        var f = TokenFile(t);
        Assert.Equal("valore", TokenRenderer.RenderFileName("{env:EO_MATRIX_VAR}", f, new RunState(), Build.Now));
        Assert.Equal(Path.Combine(t.Path, "valore"), TokenRenderer.RenderPath(Path.Combine(t.Path, "%EO_MATRIX_VAR%"), f, new RunState(), Build.Now));
    }

    [Theory]
    [InlineData("folder")]
    [InlineData("path")]
    [InlineData("drive")]
    [InlineData("subdir")]
    public void Token_path_tokens(string token)
    {
        using var t = new TempDir();
        var f = TokenFile(t);
        var rendered = TokenRenderer.RenderPath("{" + token + "}", f, new RunState(), Build.Now);
        var expected = token switch
        {
            "folder" => t.Combine("Sub"),
            "path" => t.Combine("Sub", "Report Q3.Txt"),
            "drive" => Path.GetPathRoot(t.Path)!.TrimEnd('\\'),
            _ => t.Combine("Sub"), // {subdir} = "Sub", relative => resolved against the file folder's parent root
        };
        if (token == "subdir")
            Assert.Equal(Path.Combine(@"D:\X", "Sub"), TokenRenderer.RenderPath(@"D:\X\{subdir}", f, new RunState(), Build.Now));
        else if (token == "drive")
            Assert.StartsWith(expected, rendered, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Equal(expected, rendered);
    }

    [Theory]
    [InlineData("desktop", Environment.SpecialFolder.DesktopDirectory)]
    [InlineData("documents", Environment.SpecialFolder.MyDocuments)]
    [InlineData("pictures", Environment.SpecialFolder.MyPictures)]
    [InlineData("music", Environment.SpecialFolder.MyMusic)]
    [InlineData("videos", Environment.SpecialFolder.MyVideos)]
    [InlineData("home", Environment.SpecialFolder.UserProfile)]
    public void Token_system_folders(string token, Environment.SpecialFolder folder)
    {
        using var t = new TempDir();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(folder), "X"),
            TokenRenderer.RenderPath("{" + token + "}\\X", TokenFile(t), new RunState(), Build.Now));
    }

    [Fact]
    public void Token_downloads_folder()
    {
        using var t = new TempDir();
        Assert.Equal(Path.Combine(SystemFolders.Downloads, "X"), TokenRenderer.RenderPath("{downloads}\\X", TokenFile(t), new RunState(), Build.Now));
    }

    // ------------------------------------------------------------------ conflicts

    [Fact]
    public void Conflict_keep_both_renames_new()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        t.File(@"out\a.txt", "old");
        var src = t.File("a.txt", "new");
        Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.RenameNew }), new[] { src });
        Assert.Equal("old", File.ReadAllText(t.Combine("out", "a.txt")));
        Assert.Equal("new", File.ReadAllText(t.Combine("out", "a (2).txt")));
    }

    [Fact]
    public void Conflict_overwrite_recycles_existing()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var existing = t.File(@"out\a.txt", "old");
        var src = t.File("a.txt", "new");
        Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Overwrite }), new[] { src });
        Assert.Equal("new", File.ReadAllText(existing));
        Assert.Contains(existing, bin.RecycledPaths);
    }

    [Fact]
    public void Conflict_skip_leaves_both_untouched()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        t.File(@"out\a.txt", "old");
        var src = t.File("a.txt", "new");
        var r = Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out"), OnConflict = ConflictPolicy.Skip }), new[] { src });
        Assert.Equal("new", File.ReadAllText(src));
        Assert.Equal("old", File.ReadAllText(t.Combine("out", "a.txt")));
        Assert.Equal(OperationStatus.Skipped, r.Records.Single().Status);
    }

    // ------------------------------------------------------------------ simulation / undo

    [Fact]
    public void Simulation_reports_planned_operations_without_touching_files()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("a.txt");
        var r = Run(bin, One(new AllFilesCondition(), new DeleteAction()), new[] { src }, simulate: true);
        Assert.True(File.Exists(src));
        Assert.Empty(bin.RecycledPaths);
        Assert.Equal(OperationStatus.Planned, r.Records.Single().Status);
    }

    [Fact]
    public void Undo_restores_moved_and_recycled_files()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var a = t.File("a.txt");
        var b = t.File("b.tmp");
        var p = Build.Profile(
            Build.Rule(new ExtensionCondition { Extensions = "txt" }, new MoveAction { Destination = t.Combine("out") }),
            Build.Rule(new ExtensionCondition { Extensions = "tmp" }, new DeleteAction()));
        var r = Run(bin, p, new[] { a, b });
        var journal = new UndoJournal(t.Combine("journal.json"));
        journal.Push(r.Undo!);
        var undo = new UndoJournal(t.Combine("journal.json")).UndoLast(bin); // survives a restart
        Assert.Empty(undo.Errors);
        Assert.True(File.Exists(a));
        Assert.True(File.Exists(b));
        Assert.False(Directory.Exists(t.Combine("out")));
    }

    // ------------------------------------------------------------------ robustness

    [Fact]
    public void Long_path_move_and_undo()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var deep = string.Join("\\", Enumerable.Repeat("cartella_molto_lunga_per_superare_MAX_PATH", 7));
        var src = t.File(Path.Combine(deep, "file.txt"));
        Assert.True(src.Length > 260);
        var r = Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine(deep, "dest") }), new[] { src });
        Assert.True(File.Exists(t.Combine(deep, "dest", "file.txt")));
        UndoJournal.Revert(r.Undo!, bin);
        Assert.True(File.Exists(src));
    }

    [Fact]
    public void File_in_use_is_skipped_and_reported()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("busy.docx");
        RunResult r;
        using (new FileStream(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            r = Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = t.Combine("out") }), new[] { src });
        Assert.True(File.Exists(src));
        var rec = r.Records.Single(x => x.FilePath == src);
        Assert.Equal(OperationStatus.Failed, rec.Status);
        Assert.False(string.IsNullOrWhiteSpace(rec.Message));
    }

    [Fact]
    public void Permission_denied_destination_is_reported_and_source_kept()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var locked = Directory.CreateDirectory(t.Combine("locked"));
        var src = t.File("a.txt", "keep me");
        var me = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(me, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.WriteData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny);
        var acl = locked.GetAccessControl();
        acl.AddAccessRule(deny);
        locked.SetAccessControl(acl);
        try
        {
            var r = Run(bin, One(new AllFilesCondition(), new MoveAction { Destination = Path.Combine(locked.FullName, "sub") }), new[] { src });
            Assert.Equal("keep me", File.ReadAllText(src));
            Assert.Equal(OperationStatus.Failed, r.Records.Single().Status);
        }
        finally
        {
            acl = locked.GetAccessControl();
            acl.RemoveAccessRule(deny);
            locked.SetAccessControl(acl);
        }
    }

    [Fact]
    public void Permission_denied_source_file_is_reported()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File("secret.txt", "x");
        var me = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(me, FileSystemRights.Delete | FileSystemRights.ReadData, AccessControlType.Deny);
        var info = new FileInfo(src);
        var original = info.GetAccessControl();
        var acl = info.GetAccessControl();
        acl.AddAccessRule(deny);
        info.SetAccessControl(acl);
        try
        {
            // Note: a rename/move would still succeed (Windows allows it through the parent's "delete child" right),
            // but reading the content (copy) is denied and must be reported without side effects.
            var r = Run(bin, One(new AllFilesCondition(), new CopyAction { Destination = t.Combine("out") }), new[] { src });
            Assert.Contains(r.Records, x => x.Status == OperationStatus.Failed && !string.IsNullOrEmpty(x.Message));
            Assert.True(File.Exists(src));
            Assert.False(File.Exists(t.Combine("out", "secret.txt")));
        }
        finally
        {
            info.SetAccessControl(original); // the owner can always rewrite the DACL
        }
    }

    [Fact]
    public void Unicode_paths_move_rename_and_undo()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var src = t.File(@"Città\Ελληνικά 日本語 😀.txt", "ü");
        var r = Run(bin, One(new AllFilesCondition(), new RenameAction { NewName = "{name|upper} – ok.{ext}" }, new MoveAction { Destination = t.Combine("Résumé") }), new[] { src });
        var target = t.Combine("Résumé", "ΕΛΛΗΝΙΚΆ 日本語 😀 – ok.txt");
        Assert.True(File.Exists(target), string.Join("; ", r.Records.Select(x => x.Target + " " + x.Message)));
        UndoJournal.Revert(r.Undo!, bin);
        Assert.Equal("ü", File.ReadAllText(src));
    }

    // ------------------------------------------------------------------ triggers

    [Fact]
    public void Trigger_manual_scan_lists_folder_files()
    {
        using var t = new TempDir();
        t.File("a.txt");
        t.File("b.crdownload");
        var files = FolderMonitor.Scan(new WatchedFolder { Path = t.Path, Trigger = TriggerMode.Manual }, new AppSettings().IgnorePatterns);
        Assert.Equal("a.txt", Path.GetFileName(files.Single().Path));
    }

    [Fact]
    public void Trigger_realtime_watcher_raises_files_ready_after_file_is_stable()
    {
        using var t = new TempDir();
        var settings = new AppSettings { StabilitySeconds = 1 };
        using var monitor = new FolderMonitor(() => settings);
        var profile = new Profile { Name = "W" };
        profile.Folders.Add(new WatchedFolder { Path = t.Path, Trigger = TriggerMode.Realtime });
        var got = new TaskCompletionSource<FilesReadyEventArgs>();
        monitor.FilesReady += (_, e) => got.TrySetResult(e);
        monitor.Configure(new[] { profile });
        Assert.Equal(1, monitor.ActiveWatchers);
        t.File("new.txt", "content");
        Assert.True(got.Task.Wait(TimeSpan.FromSeconds(20)), "watcher did not fire");
        Assert.Equal("watch", got.Task.Result.Trigger);
        Assert.Equal(t.Combine("new.txt"), got.Task.Result.Files.Single().Path);
    }

    [Fact]
    public void Trigger_realtime_ignores_partial_downloads_and_waits_for_unlock()
    {
        using var t = new TempDir();
        var settings = new AppSettings { StabilitySeconds = 1 };
        using var monitor = new FolderMonitor(() => settings);
        var profile = new Profile { Name = "W" };
        profile.Folders.Add(new WatchedFolder { Path = t.Path, Trigger = TriggerMode.Realtime });
        var events = new List<string>();
        monitor.FilesReady += (_, e) => { lock (events) events.AddRange(e.Files.Select(f => Path.GetFileName(f.Path))); };
        monitor.Configure(new[] { profile });
        t.File("movie.mkv.crdownload", "partial");
        var busy = t.Combine("busy.bin");
        using (var fs = new FileStream(busy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            fs.WriteByte(1);
            Thread.Sleep(3500); // locked for longer than the stability window
            lock (events) Assert.Empty(events);
        }
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline) { lock (events) if (events.Contains("busy.bin")) break; Thread.Sleep(200); }
        lock (events)
        {
            Assert.Contains("busy.bin", events);
            Assert.DoesNotContain("movie.mkv.crdownload", events);
        }
    }

    [Theory]
    [InlineData(TriggerMode.Interval)]
    [InlineData(TriggerMode.Daily)]
    public void Trigger_scheduled_runs_due_folders(TriggerMode mode)
    {
        using var t = new TempDir();
        t.File("a.txt");
        using var monitor = new FolderMonitor(() => new AppSettings());
        var folder = new WatchedFolder { Path = t.Path, Trigger = mode, IntervalMinutes = 30, DailyTime = "09:00" };
        var profile = new Profile { Name = "S" };
        profile.Folders.Add(folder);
        FilesReadyEventArgs? got = null;
        monitor.FilesReady += (_, e) => got = e;
        monitor.Configure(new[] { profile });
        var now = new DateTime(2026, 10, 14, 10, 0, 0);
        monitor.RunDueSchedules(now);
        Assert.NotNull(got);
        Assert.Equal("schedule", got!.Trigger);
        Assert.Equal(now, folder.LastRun);
        got = null;
        monitor.RunDueSchedules(now.AddMinutes(1)); // not due again
        Assert.Null(got);
    }

    [Fact]
    public void Trigger_paused_monitor_does_nothing()
    {
        using var t = new TempDir();
        t.File("a.txt");
        using var monitor = new FolderMonitor(() => new AppSettings()) { Paused = true };
        var profile = new Profile { Name = "S" };
        profile.Folders.Add(new WatchedFolder { Path = t.Path, Trigger = TriggerMode.Interval });
        bool fired = false;
        monitor.FilesReady += (_, _) => fired = true;
        monitor.Configure(new[] { profile });
        monitor.RunDueSchedules(DateTime.Now);
        Assert.False(fired);
    }

    // ------------------------------------------------------------------ presets / localization

    [Fact]
    public void Preset_export_import_roundtrip_for_every_preset()
    {
        using var t = new TempDir();
        foreach (var kind in Enum.GetValues<PresetKind>())
        {
            var p = Presets.Create(kind);
            var file = t.Combine(kind + ".json");
            JsonStore.ExportProfile(p, file);
            var back = JsonStore.ImportProfile(file);
            Assert.NotEqual(p.Id, back.Id);
            Assert.Equal(p.Name, back.Name);
            Assert.Equal(p.Rules.Count, back.Rules.Count);
            Assert.Equal(Json.Clone(p.Rules[0].Condition).Children.Count, back.Rules[0].Condition.Children.Count);
            Assert.Equal(p.Rules[0].Actions[0].GetType(), back.Rules[0].Actions[0].GetType());
        }
    }

    [Fact]
    public void Preset_import_rejects_invalid_json()
    {
        using var t = new TempDir();
        var bad = t.File("bad.json", "{ not json");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => JsonStore.ImportProfile(bad));
    }

    [Fact]
    public void Localization_core_strings_exist_in_both_languages()
    {
        foreach (var lang in new[] { "it", "en" })
        {
            Loc.SetLanguage(lang);
            foreach (var key in Loc.Keys) Assert.False(string.IsNullOrWhiteSpace(Loc.T(key)), $"{lang}:{key}");
        }
        Loc.SetLanguage("it");
        Assert.Equal("Immagini", FileCategories.DisplayName(FileCategory.Image));
        Loc.SetLanguage("en");
        Assert.Equal("Images", FileCategories.DisplayName(FileCategory.Image));
    }

    [Fact]
    public void Activity_log_keeps_memory_and_daily_file()
    {
        using var t = new TempDir();
        var log = new ActivityLog(t.Combine("logs"));
        log.Info("hello", "manual");
        Assert.Contains(log.Snapshot(), r => r.Message == "hello");
        var file = Directory.GetFiles(t.Combine("logs"), "activity-*.log").Single(); // one file per day, kept 60 days
        Assert.Contains("hello", File.ReadAllText(file));
    }
}
