using System.Text.Json.Nodes;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.Tests;

/// <summary>"Name contains", folders organized as a whole, removal of NOT and the ready-made rules.</summary>
public class FolderAndNameTests
{
    private static bool Eval(Condition c, FileContext f) => ConditionEvaluator.Evaluate(c, f, Build.Now);

    private static Rule NamedRule(string name, ConditionGroup condition, params RuleAction[] actions)
    {
        var r = new Rule { Name = name, Condition = condition };
        foreach (var a in actions) r.Actions.Add(a);
        return r;
    }

    private static Rule FontFolderRule(string destination) => NamedRule("FONTS",
        new ConditionGroup
        {
            Operator = GroupOperator.All,
            Children = { new ItemTypeCondition { Kind = ItemKind.Folder }, new FolderContentCondition { Extensions = "ttf; otf" } },
        },
        new MoveAction { Destination = destination });

    // ------------------------------------------------------------------ name contains

    [Fact]
    public void Name_contains_is_case_insensitive_on_the_name_without_path_and_combines_with_extension()
    {
        using var t = new TempDir();
        var all = new ConditionGroup
        {
            Operator = GroupOperator.All,
            Children = { new NameContainsCondition { Words = "fattura" }, new ExtensionCondition { Extensions = "pdf" } },
        };
        Assert.True(Eval(all, Build.Context(t.File("FATTURA_2026-03.pdf"))));
        Assert.True(Eval(all, Build.Context(t.File("Copia fattura enel.PDF"))));
        Assert.False(Eval(all, Build.Context(t.File("fattura.docx"))));
        Assert.False(Eval(all, Build.Context(t.File("report.pdf"))));
        // the folder path is not part of the name
        Assert.False(Eval(all, Build.Context(t.File(Path.Combine("fatture", "report.pdf")))));

        var any = new ConditionGroup
        {
            Operator = GroupOperator.Any,
            Children = { new NameContainsCondition { Words = "fattura" }, new ExtensionCondition { Extensions = "pdf" } },
        };
        Assert.True(Eval(any, Build.Context(t.File("fattura.docx"))));
        Assert.True(Eval(any, Build.Context(t.File("report.pdf"))));
        Assert.False(Eval(any, Build.Context(t.File("note.txt"))));
    }

    [Fact]
    public void Name_contains_accepts_several_words_separated_by_semicolon()
    {
        using var t = new TempDir();
        var c = new NameContainsCondition { Words = " fattura ;ricevuta; ;" };
        Assert.True(Eval(c, Build.Context(t.File("Ricevuta bonifico.pdf"))));
        Assert.True(Eval(c, Build.Context(t.File("fattura.pdf"))));
        Assert.False(Eval(c, Build.Context(t.File("contratto.pdf"))));
        Assert.False(Eval(new NameContainsCondition { Words = "" }, Build.Context(t.File("x.pdf"))));
    }

    [Fact]
    public void Screenshots_go_to_the_screenshot_folder_and_other_jpg_to_images()
    {
        using var t = new TempDir();
        var shot = t.File(Path.Combine("Downloads", "Screenshot 2026-10-15 101010.jpg"));
        var photo = t.File(Path.Combine("Downloads", "vacanza.jpg"));
        var screenshotRule = NamedRule("Screenshot", new ConditionGroup
        {
            Operator = GroupOperator.All,
            Children = { new NameContainsCondition { Words = "screenshot" }, new ExtensionCondition { Extensions = "jpg" } },
        }, new MoveAction { Destination = t.Combine("Downloads", "Screenshot") });
        var images = NamedRule("Immagini", new ConditionGroup { Children = { new ExtensionCondition { Extensions = "jpg" } } },
            new MoveAction { Destination = t.Combine("Downloads", "4. Immagini") });

        var result = Build.Engine(new FakeRecycleBin(t.Combine("trash")))
            .Run(new[] { new FileInput(shot), new FileInput(photo) }, Build.Profile(screenshotRule, images), new RunOptions());

        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(t.Combine("Downloads", "Screenshot", "Screenshot 2026-10-15 101010.jpg")));
        Assert.True(File.Exists(t.Combine("Downloads", "4. Immagini", "vacanza.jpg")));
    }

    // ------------------------------------------------------------------ folders

    [Fact]
    public void Item_type_and_folder_content_conditions()
    {
        using var t = new TempDir();
        t.File(Path.Combine("MyFont", "a.ttf"));
        t.File(Path.Combine("MyFont", "b.otf"));
        t.File(Path.Combine("MyFont", "sub", "c.ttf"));
        t.File(Path.Combine("MyFont", "readme.txt"));
        t.File(Path.Combine("Project", "x.cs"));
        t.File(Path.Combine("Project", "y.cs"));
        t.File(Path.Combine("Project", "logo.ttf"));
        var fonts = Build.Context(t.Combine("MyFont"));
        var project = Build.Context(t.Combine("Project"));
        var file = Build.Context(t.Combine("MyFont", "a.ttf"));

        Assert.True(fonts.IsFolder);
        Assert.True(Eval(new ItemTypeCondition { Kind = ItemKind.Folder }, fonts));
        Assert.False(Eval(new ItemTypeCondition { Kind = ItemKind.Folder }, file));
        Assert.True(Eval(new ItemTypeCondition { Kind = ItemKind.File }, file));

        var most = new FolderContentCondition { Extensions = "ttf;otf", Match = FolderContentMatch.Most };
        Assert.True(Eval(most, fonts));    // 3 of 4 files
        Assert.False(Eval(most, project)); // 1 of 3 files
        Assert.False(Eval(most, file));    // a file is not a folder
        Assert.True(Eval(new FolderContentCondition { Extensions = "ttf", Match = FolderContentMatch.Any }, project));
        Assert.False(Eval(new FolderContentCondition { Extensions = "ttf;otf", Match = FolderContentMatch.All }, fonts));
        Assert.Equal("4 file(s): .ttf, .otf, .txt", fonts.FolderContent.Summary);
        Assert.Equal("", fonts.Extension);
        // file conditions never mistake a folder for a file
        Assert.False(Eval(new FileTypeCondition { Category = FileCategory.Other }, fonts));
    }

    [Fact]
    public void Font_folder_is_recognized_by_content_and_moved_as_a_whole()
    {
        using var t = new TempDir();
        var downloads = t.Combine("Downloads");
        t.File(Path.Combine("Downloads", "Roboto", "Roboto-Regular.ttf"));
        t.File(Path.Combine("Downloads", "Roboto", "Roboto-Bold.ttf"));
        t.File(Path.Combine("Downloads", "Roboto", "static", "Roboto-Italic.otf"));
        t.File(Path.Combine("Downloads", "Roboto", "OFL.txt"));
        t.File(Path.Combine("Downloads", "Progetto", "main.cs"));
        t.File(Path.Combine("Downloads", "Progetto", "app.cs"));
        t.File(Path.Combine("Downloads", "loose.ttf"));
        var fontsDir = Path.Combine(downloads, "9. Fonts");
        // a file rule for single fonts must not tear the folder apart
        var fileRule = NamedRule("TTF", new ConditionGroup { Children = { new ExtensionCondition { Extensions = "ttf;otf" } } },
            new MoveAction { Destination = fontsDir });
        var profile = Build.Profile(FontFolderRule(fontsDir), fileRule);
        var inputs = FolderMonitor.Scan(new WatchedFolder { Path = downloads, IncludeSubfolders = true }, null, includeFolders: true);
        var engine = Build.Engine(new FakeRecycleBin(t.Combine("trash")));

        var preview = engine.Run(inputs, profile, new RunOptions { Simulate = true });
        var folderOp = Assert.Single(preview.Records, r => r.IsFolder && r.Status == OperationStatus.Planned);
        Assert.Equal(Path.Combine(downloads, "Roboto"), folderOp.Source);
        Assert.Equal(Path.Combine(fontsDir, "Roboto"), folderOp.Target);
        Assert.Equal("FONTS", folderOp.RuleName);
        Assert.StartsWith("4 file(s): .ttf, .otf, .txt", folderOp.Message);
        Assert.DoesNotContain(preview.Records, r => r.Source.Contains(Path.Combine("Roboto", "Roboto-")));
        Assert.DoesNotContain(preview.Records, r => r.Source.Contains("Progetto"));
        Assert.Contains(preview.Records, r => !r.IsFolder && r.Source.EndsWith("loose.ttf"));

        var real = engine.Run(inputs, profile, new RunOptions());
        Assert.Equal(0, real.Failed);
        Assert.True(File.Exists(Path.Combine(fontsDir, "Roboto", "static", "Roboto-Italic.otf")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "Roboto", "OFL.txt")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "loose.ttf")));
        Assert.True(Directory.Exists(Path.Combine(downloads, "Progetto")));
        Assert.False(Directory.Exists(Path.Combine(downloads, "Roboto")));

        // undo puts the folder back
        var undo = new UndoJournal(null);
        undo.Push(real.Undo!);
        var back = undo.UndoLast(new FakeRecycleBin(t.Combine("trash2")));
        Assert.Empty(back.Errors);
        Assert.True(File.Exists(Path.Combine(downloads, "Roboto", "static", "Roboto-Italic.otf")));
    }

    [Fact]
    public void Folders_already_in_place_or_containing_the_destination_are_never_moved()
    {
        using var t = new TempDir();
        var downloads = t.Combine("Downloads");
        var fontsDir = Path.Combine(downloads, "9. Fonts");
        t.File(Path.Combine("Downloads", "9. Fonts", "Lato", "Lato.ttf"));
        t.File(Path.Combine("Downloads", "9. Fonts", "single.otf"));
        t.File(Path.Combine("Downloads", "Inter", "Inter.ttf"));
        var inputs = FolderMonitor.Scan(new WatchedFolder { Path = downloads, IncludeSubfolders = true }, null, includeFolders: true);

        // "9. Fonts" itself is a font folder: it is the destination, so it stays where it is (no move into itself)
        var result = Build.Engine(new FakeRecycleBin(t.Combine("trash"))).Run(inputs, Build.Profile(FontFolderRule(fontsDir)), new RunOptions());
        Assert.Equal(0, result.Failed);
        Assert.Contains(result.Records, r => r.Source == fontsDir && r.Status == OperationStatus.Unchanged);
        Assert.True(File.Exists(Path.Combine(fontsDir, "Lato", "Lato.ttf")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "single.otf")));
        Assert.True(File.Exists(Path.Combine(fontsDir, "Inter", "Inter.ttf")));
        Assert.False(Directory.Exists(Path.Combine(fontsDir, "9. Fonts")));

        // a second run finds everything in place
        var again = Build.Engine(new FakeRecycleBin(t.Combine("trash"))).Run(
            FolderMonitor.Scan(new WatchedFolder { Path = downloads, IncludeSubfolders = true }, null, includeFolders: true),
            Build.Profile(FontFolderRule(fontsDir)), new RunOptions { Simulate = true });
        Assert.DoesNotContain(again.Records, r => r.Status == OperationStatus.Planned);
    }

    [Fact]
    public void A_folder_is_never_moved_into_its_own_subfolder()
    {
        using var t = new TempDir();
        var folder = Path.GetDirectoryName(t.File(Path.Combine("Roboto", "a.ttf")))!;
        var rule = FontFolderRule(Path.Combine(folder, "inner"));
        var result = Build.Engine(new FakeRecycleBin(t.Combine("trash"))).Run(new[] { new FileInput(folder, t.Path) }, Build.Profile(rule), new RunOptions());
        var rec = Assert.Single(result.Records);
        Assert.Equal(OperationStatus.Skipped, rec.Status);
        Assert.True(File.Exists(Path.Combine(folder, "a.ttf")));
    }

    [Fact]
    public void Rules_without_folder_conditions_never_receive_folders()
    {
        using var t = new TempDir();
        t.File(Path.Combine("Downloads", "Stuff", "a.txt"));
        var all = NamedRule("All", new ConditionGroup { Children = { new AllFilesCondition() } }, new MoveAction { Destination = t.Combine("Out") });
        var inputs = FolderMonitor.Scan(new WatchedFolder { Path = t.Combine("Downloads") }, null, includeFolders: true);
        Assert.Contains(inputs, i => i.Path.EndsWith("Stuff"));

        var result = Build.Engine(new FakeRecycleBin(t.Combine("trash"))).Run(inputs, Build.Profile(all), new RunOptions());
        Assert.Empty(result.Records);
        Assert.True(File.Exists(t.Combine("Downloads", "Stuff", "a.txt")));
    }

    [Fact]
    public void Folder_name_conflict_keeps_the_whole_folder_name()
    {
        Assert.Equal(@"C:\x\Font.Family (2)",
            ConflictResolver.NextFreeName(@"C:\x\Font.Family", p => p == @"C:\x\Font.Family", isFolder: true));
    }

    // ------------------------------------------------------------------ NOT migration

    private const string LegacyLibrary = """
    {
      "schemaVersion": 1,
      "profiles": [ {
        "name": "Download",
        "rules": [
          { "name": "Small", "enabled": true,
            "condition": { "type": "group", "negate": false, "operator": "all", "children": [
              { "type": "size", "negate": true, "comparison": "greaterThan", "value": 1, "unit": "mb" } ] },
            "actions": [] },
          { "name": "Recent", "enabled": true,
            "condition": { "type": "group", "operator": "all", "children": [
              { "type": "group", "negate": true, "operator": "any", "children": [
                { "type": "date", "field": "modified", "mode": "olderThan", "amount": 30, "unit": "days" },
                { "type": "fileType", "category": "image" } ] } ] },
            "actions": [] },
          { "name": "Not PDF", "enabled": true,
            "condition": { "type": "group", "operator": "all", "children": [
              { "type": "extension", "negate": true, "extensions": "pdf" } ] },
            "actions": [] },
          { "name": "Plain", "enabled": true,
            "condition": { "type": "group", "operator": "all", "children": [ { "type": "extension", "negate": false, "extensions": "pdf" } ] },
            "actions": [] }
        ] } ]
    }
    """;

    [Fact]
    public void Old_rules_with_not_load_without_errors_and_are_converted_or_flagged()
    {
        using var t = new TempDir();
        var file = t.Combine("profiles.json");
        File.WriteAllText(file, LegacyLibrary);
        var notes = new List<MigrationNote>();
        var lib = JsonStore.LoadProfiles(file, () => new ProfileLibrary(), notes);

        var rules = lib.Profiles[0].Rules;
        Assert.Equal(4, rules.Count);
        Assert.Equal(3, notes.Count);
        Assert.DoesNotContain("negate", File.ReadAllText(file));
        Assert.Single(Directory.GetFiles(t.Path, "profiles.json.bak-*")); // original kept
        Assert.Contains("\"negate\": true", File.ReadAllText(Directory.GetFiles(t.Path, "profiles.json.bak-*")[0]));

        // NOT size > 1 MB  =>  size < 1 MB + 1 byte
        var small = Assert.IsType<SizeCondition>(rules[0].Condition.Children[0]);
        Assert.Equal(SizeComparison.LessThan, small.Comparison);
        Assert.Equal(SizeUnit.Bytes, small.Unit);
        Assert.Equal(1024 * 1024 + 1, small.Value);
        Assert.True(rules[0].Enabled);

        // NOT (older than 30 days OR image)  =>  newer than 30 days AND (any other category)
        var recent = Assert.IsType<ConditionGroup>(rules[1].Condition.Children[0]);
        Assert.Equal(GroupOperator.All, recent.Operator);
        Assert.Equal(DateMode.NewerThan, Assert.IsType<DateCondition>(recent.Children[0]).Mode);
        var otherTypes = Assert.IsType<ConditionGroup>(recent.Children[1]);
        Assert.DoesNotContain(otherTypes.Children.Cast<FileTypeCondition>(), c => c.Category == FileCategory.Image);
        Assert.True(rules[1].Enabled);

        // no equivalent: disabled and marked, never silently inverted
        Assert.False(rules[2].Enabled);
        Assert.Contains("NOT removed", rules[2].Name);
        Assert.Contains(notes, n => n.Rule == "Not PDF" && n.NeedsReview);

        Assert.True(rules[3].Enabled);
        Assert.Equal("Plain", rules[3].Name);

        // loading again changes nothing
        var notes2 = new List<MigrationNote>();
        JsonStore.LoadProfiles(file, () => new ProfileLibrary(), notes2);
        Assert.Empty(notes2);
        Assert.Single(Directory.GetFiles(t.Path, "profiles.json.bak-*"));
    }

    [Fact]
    public void Converted_conditions_match_exactly_the_files_the_not_matched()
    {
        using var t = new TempDir();
        var files = new[]
        {
            Build.Context(t.File("exact.bin", new string('a', 1024 * 1024))),
            Build.Context(t.File("bigger.bin", new string('a', 1024 * 1024 + 1))),
            Build.Context(t.File("tiny.jpg", "x", Build.Now.AddDays(-40))),
            Build.Context(t.File("new.pdf", "x", Build.Now.AddDays(-1))),
        };
        var originals = new Condition[]
        {
            new SizeCondition { Comparison = SizeComparison.GreaterThan, Value = 1, Unit = SizeUnit.MB },
            new SizeCondition { Comparison = SizeComparison.LessThan, Value = 1, Unit = SizeUnit.MB },
            new SizeCondition { Comparison = SizeComparison.EqualTo, Value = 1, Unit = SizeUnit.MB },
            new SizeCondition { Comparison = SizeComparison.Between, Value = 1, Value2 = 2, Unit = SizeUnit.KB },
            new DateCondition { Field = DateField.Modified, Mode = DateMode.OlderThan, Amount = 30, Unit = TimeUnit.Days },
            new DateCondition { Field = DateField.Modified, Mode = DateMode.Before, Date = Build.Now.AddDays(-10) },
            new FileTypeCondition { Category = FileCategory.Image },
            new AllFilesCondition(),
            new ConditionGroup(),
            new ConditionGroup { Operator = GroupOperator.Any, Children = { new FileTypeCondition { Category = FileCategory.Document }, new AllFilesCondition() } },
        };
        foreach (var c in originals)
        {
            var inverse = LegacyNotMigration.Invert(c);
            Assert.NotNull(inverse);
            foreach (var f in files) Assert.Equal(!Eval(c, f), Eval(inverse!, f));
        }
        Assert.Null(LegacyNotMigration.Invert(new ExtensionCondition { Extensions = "pdf" }));
        Assert.Null(LegacyNotMigration.Invert(new DateCondition { Field = DateField.Taken }));
    }

    [Fact]
    public void Imported_profile_with_not_is_migrated()
    {
        using var t = new TempDir();
        var lib = JsonNode.Parse(LegacyLibrary)!;
        var file = t.Combine("export.json");
        File.WriteAllText(file, lib["profiles"]![0]!.ToJsonString());
        var profile = JsonStore.ImportProfile(file);
        Assert.Equal(4, profile.Rules.Count);
        Assert.False(profile.Rules[2].Enabled);
    }

    // ------------------------------------------------------------------ ready-made rules

    [Fact]
    public void Default_rules_use_free_numbers_and_are_added_only_once()
    {
        using var t = new TempDir();
        var downloads = t.Combine("Downloads");
        Directory.CreateDirectory(Path.Combine(downloads, "1. PDF"));
        Directory.CreateDirectory(Path.Combine(downloads, "9. Fonts"));
        Directory.CreateDirectory(Path.Combine(downloads, "3. Foto"));
        var profile = new Profile { Name = "Download" };
        profile.Folders.Add(new WatchedFolder { Path = downloads });
        var pdf = new Rule { Name = "PDF" };
        pdf.Condition.Children.Add(new ExtensionCondition { Extensions = "pdf" });
        pdf.Actions.Add(new MoveAction { Destination = Path.Combine(downloads, "1. PDF") });
        profile.Rules.Add(pdf);
        var other = new Profile { Name = "Altro" };
        var lib = new ProfileLibrary { Profiles = { other, profile }, ActiveProfileId = other.Id };

        var added = DefaultRules.Seed(lib, downloads);

        Assert.Empty(other.Rules);
        Assert.Equal(new[] { "Video", "Foto", "Immagini", "Archivi", "Programmi" }, added.Select(r => r.Name));
        string Dest(string name) => ((MoveAction)profile.Rules.Single(r => r.Name == name).Actions[0]).Destination;
        Assert.Equal(Path.Combine(downloads, "2. Video"), Dest("Video"));
        Assert.Equal(Path.Combine(downloads, "3. Foto"), Dest("Foto")); // existing folder reused
        Assert.Equal(Path.Combine(downloads, "4. Immagini"), Dest("Immagini"));
        Assert.Equal(Path.Combine(downloads, "5. Archivi"), Dest("Archivi"));
        Assert.Equal(Path.Combine(downloads, "6. Programmi"), Dest("Programmi"));
        Assert.Single(profile.Rules, r => r.Name == "PDF");
        var video = profile.Rules.Single(r => r.Name == "Video");
        Assert.Equal("mp4; mkv; avi; mov; wmv; flv; webm; m4v; mpg; mpeg", Assert.IsType<ExtensionCondition>(video.Condition.Children[0]).Extensions);
        Assert.Equal(ConflictPolicy.RenameNew, ((MoveAction)video.Actions[0]).OnConflict);

        Assert.Empty(DefaultRules.Seed(lib, downloads)); // never duplicated
        Assert.Equal(6, profile.Rules.Count);
    }

    [Fact]
    public void Default_rules_on_an_empty_library_create_a_downloads_profile()
    {
        using var t = new TempDir();
        var lib = new ProfileLibrary();
        var added = DefaultRules.Seed(lib, t.Path);
        Assert.Equal(6, added.Count);
        var p = Assert.Single(lib.Profiles);
        Assert.Equal(Path.Combine(t.Path, "4. PDF"), ((MoveAction)p.Rules.Single(r => r.Name == "PDF").Actions[0]).Destination);
    }
}
