using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.Tests;

public class ServicesTests
{
    [Fact]
    public void Profile_export_import_roundtrip_keeps_polymorphic_rules()
    {
        using var t = new TempDir();
        var p = Presets.Create(PresetKind.Downloads, TriggerMode.Realtime);
        p.Rules[0].Condition.Children.Add(new ConditionGroup
        {
            Operator = GroupOperator.Any,
            Children = { new NameCondition { Operator = TextOperator.Regex, Value = "^x" }, new SizeCondition { Unit = SizeUnit.GB } },
        });
        p.Rules[0].Actions.Add(new RenameAction { NewName = "{date} {name}.{ext}", OnConflict = ConflictPolicy.Skip });
        var file = t.Combine("p.json");
        JsonStore.ExportProfile(p, file);
        var back = JsonStore.ImportProfile(file);

        Assert.NotEqual(p.Id, back.Id);
        Assert.Equal(p.Rules.Count, back.Rules.Count);
        Assert.Equal(TriggerMode.Realtime, back.Folders[0].Trigger);
        var group = Assert.IsType<ConditionGroup>(back.Rules[0].Condition.Children[1]);
        Assert.Equal(GroupOperator.Any, group.Operator);
        Assert.IsType<NameCondition>(group.Children[0]);
        var rename = Assert.IsType<RenameAction>(back.Rules[0].Actions[1]);
        Assert.Equal(ConflictPolicy.Skip, rename.OnConflict);
        Assert.Contains("\"type\": \"group\"", File.ReadAllText(file));
    }

    [Fact]
    public void Default_settings_and_library_roundtrip()
    {
        using var t = new TempDir();
        var file = t.Combine("settings.json");
        JsonStore.Save(file, new AppSettings()); // NaN drop-target coordinates must be writable
        var back = JsonStore.Load(file, () => new AppSettings { PreviewBeforeApply = false });
        Assert.True(double.IsNaN(back.DropTargetLeft));
        Assert.True(back.PreviewBeforeApply);

        var lib = new ProfileLibrary();
        foreach (var kind in Enum.GetValues<PresetKind>()) lib.Profiles.Add(Presets.Create(kind));
        JsonStore.Save(t.Combine("profiles.json"), lib);
        Assert.Equal(4, JsonStore.Load(t.Combine("profiles.json"), () => new ProfileLibrary()).Profiles.Count);
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults_and_are_kept()
    {
        using var t = new TempDir();
        var file = t.File("settings.json", "{ not json");
        var s = JsonStore.Load(file, () => new AppSettings());
        Assert.True(s.PreviewBeforeApply);
        Assert.Single(Directory.GetFiles(t.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public void All_presets_build_and_match_expected_files()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var engine = Build.Engine(bin);
        foreach (var kind in Enum.GetValues<PresetKind>())
        {
            var p = Presets.Create(kind);
            Assert.NotEmpty(p.Rules);
            Assert.Single(p.Folders);
        }
        var downloads = Presets.Create(PresetKind.Downloads);
        Assert.Equal("Images to Images folder", engine.FindRule(downloads, t.File("a.png"), out _)!.Name);
        Assert.Null(engine.FindRule(downloads, t.File("a.unknown"), out _));
    }

    [Fact]
    public void Music_preset_uses_id3_with_defaults()
    {
        using var t = new TempDir();
        var bin = new FakeRecycleBin(t.Combine("_trash"));
        var meta = new FakeMetadata();
        meta.Values["System.Music.Artist"] = "Queen";
        var src = t.File("song.mp3");
        Build.Engine(bin, meta).Run(new[] { new FileInput(src) }, Presets.Create(PresetKind.Music), new RunOptions());
        Assert.True(File.Exists(t.Combine("Queen", "Unknown album", "song.mp3")));
    }

    [Fact]
    public void Folder_path_tokens_and_ignore_patterns()
    {
        Assert.Equal(SystemFolders.Downloads, FolderMonitor.ResolveFolderPath("{downloads}"));
        Assert.True(FolderMonitor.IsIgnored("movie.mkv.crdownload", new AppSettings().IgnorePatterns));
        Assert.True(FolderMonitor.IsIgnored("~$report.docx", new AppSettings().IgnorePatterns));
        Assert.False(FolderMonitor.IsIgnored("report.docx", new AppSettings().IgnorePatterns));
    }

    [Fact]
    public void Scan_skips_ignored_and_respects_subfolders()
    {
        using var t = new TempDir();
        t.File("a.txt");
        t.File("b.part");
        t.File(@"sub\c.txt");
        var top = FolderMonitor.Scan(new WatchedFolder { Path = t.Path }, "*.part");
        Assert.Single(top);
        var deep = FolderMonitor.Scan(new WatchedFolder { Path = t.Path, IncludeSubfolders = true }, "*.part");
        Assert.Equal(2, deep.Count);
        Assert.All(deep, f => Assert.Equal(t.Path, f.RootFolder));
    }

    [Fact]
    public void Schedule_due_logic()
    {
        var now = new DateTime(2026, 10, 14, 10, 0, 0);
        Assert.True(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Interval, IntervalMinutes = 30 }, now));
        Assert.False(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Interval, IntervalMinutes = 30, LastRun = now.AddMinutes(-10) }, now));
        Assert.True(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Daily, DailyTime = "09:30", LastRun = now.AddDays(-1) }, now));
        Assert.False(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Daily, DailyTime = "09:30", LastRun = now.AddMinutes(-5) }, now));
        Assert.False(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Daily, DailyTime = "11:00" }, now));
        Assert.False(FolderMonitor.IsDue(new WatchedFolder { Trigger = TriggerMode.Manual }, now));
    }

    [Fact]
    public void Activity_log_exports_csv_with_escaping()
    {
        using var t = new TempDir();
        var log = new ActivityLog(t.Combine("logs"));
        log.Add(new[] { new OperationRecord { Kind = OperationKind.Move, Status = OperationStatus.Done, Source = "a;b", Target = "c", Message = "say \"hi\"" } });
        var csv = t.Combine("out.csv");
        ActivityLog.Export(log.Snapshot(), csv);
        var text = File.ReadAllText(csv);
        Assert.Contains("\"a;b\"", text);
        Assert.Contains("\"say \"\"hi\"\"\"", text);
        Assert.Single(Directory.GetFiles(t.Combine("logs")));
    }

    [Fact]
    public void Content_reader_reads_docx_text()
    {
        using var t = new TempDir();
        var docx = t.Combine("d.docx");
        using (var z = System.IO.Compression.ZipFile.Open(docx, System.IO.Compression.ZipArchiveMode.Create))
        using (var w = new StreamWriter(z.CreateEntry("word/document.xml").Open()))
            w.Write("<w:document xmlns:w=\"x\"><w:body><w:p><w:r><w:t>Fattura ACME</w:t></w:r></w:p></w:body></w:document>");
        Assert.Contains("Fattura ACME", new ContentReader().Read(docx));
        Assert.Contains("plain", new ContentReader().Read(t.File("n.txt", "plain text")));
        Assert.Equal("", new ContentReader().Read(t.Combine("missing.txt")));
    }

    [Fact]
    public void Import_disables_folders_and_rules_that_launch_programs_or_delete_permanently()
    {
        using var t = new TempDir();
        var p = new Profile { Name = "shared" };
        p.Rules.Add(Build.Rule(new AllFilesCondition(), new MoveAction { Destination = "{folder}\\x" }));
        p.Rules.Add(Build.Rule(new AllFilesCondition(), new OpenWithAction { Application = "cmd.exe", Arguments = "/c calc" }));
        p.Rules.Add(Build.Rule(new AllFilesCondition(), new DeleteAction { Permanent = true }));
        p.Rules.Add(Build.Rule(new AllFilesCondition(), new DeleteAction()));
        p.Folders.Add(new WatchedFolder { Path = t.Path, Enabled = true });
        var file = t.Combine("p.json");
        JsonStore.ExportProfile(p, file);

        var back = JsonStore.ImportProfile(file, out int disabled);

        Assert.Equal(2, disabled);
        Assert.Equal(new[] { true, false, false, true }, back.Rules.Select(r => r.Enabled));
        Assert.All(back.Folders, f => Assert.False(f.Enabled));
    }
}
