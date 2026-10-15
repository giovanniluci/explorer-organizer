using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Tests;

public class TokenTests
{
    private static string Name(string template, FileContext f, RunState? run = null) =>
        TokenRenderer.RenderFileName(template, f, run ?? new RunState(), Build.Now);

    [Fact]
    public void Basic_tokens()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File(@"sub\My File.TXT"));
        Assert.Equal("My File.TXT", Name("{name}.{ext}", f));
        Assert.Equal("My File.TXT", Name("{fullname}", f));
        Assert.Equal("sub - My File", Name("{parent} - {name}", f));
        Assert.Equal("2026-10-14 My File.TXT", Name("{date} {fullname}", f));
        Assert.Equal("20261014.txt", Name("{date:yyyyMMdd}.{ext|lower}", f));
    }

    [Fact]
    public void File_dates_with_format()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt", modified: new DateTime(2021, 3, 9, 8, 5, 0)));
        Assert.Equal("2021-03-09 a.txt", Name("{modified:yyyy-MM-dd} {fullname}", f));
        Assert.Equal("08.05", Name("{modified:HH.mm}", f));
    }

    [Fact]
    public void Date_format_slash_creates_subfolders_in_paths()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt", modified: new DateTime(2021, 3, 9)));
        var p = TokenRenderer.RenderPath(@"{folder}\{modified:yyyy/MM}", f, new RunState(), Build.Now);
        Assert.Equal(System.IO.Path.Combine(t.Path, "2021", "03"), p);
    }

    [Fact]
    public void Taken_falls_back_to_modified_and_uses_exif_when_present()
    {
        using var t = new TempDir();
        var path = t.File("p.jpg", modified: new DateTime(2022, 1, 2));
        Assert.Equal("2022", Name("{taken:yyyy}", Build.Context(path)));
        var meta = new FakeMetadata();
        meta.Values["System.Photo.DateTaken"] = new DateTime(2018, 7, 1);
        Assert.Equal("2018-07", Name("{taken:yyyy-MM}", Build.Context(path, meta)));
    }

    [Fact]
    public void Counter_increments_per_file_and_pads()
    {
        using var t = new TempDir();
        var run = new RunState();
        var a = Build.Context(t.File("a.txt"));
        var b = Build.Context(t.File("b.txt"));
        Assert.Equal("img_001.txt", Name("img_{counter:3}.{ext}", a, run));
        Assert.Equal("img_002.txt", Name("img_{counter:3}.{ext}", b, run));
        // each template has its own sequence; the same counter used twice in one name has the same value
        Assert.Equal("1-1", Name("{counter}-{counter}", a, run));
        Assert.Equal("2-2", Name("{counter}-{counter}", b, run));
    }

    [Fact]
    public void Metadata_tokens_exif_id3_doc_prop_and_default()
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values["System.Photo.CameraModel"] = "NEX-7";
        meta.Values["System.Music.Artist"] = "AC/DC";
        meta.Values["System.Author"] = "Ada";
        meta.Values["System.Photo.FNumber"] = "5.6";
        var f = Build.Context(t.File("x.jpg"), meta);
        Assert.Equal("NEX-7", Name("{exif:CameraModel}", f));
        Assert.Equal("AC_DC", Name("{id3:Artist}", f)); // value sanitized: no sub-folder injection
        Assert.Equal("Ada", Name("{doc:Author}", f));
        Assert.Equal("5.6", Name("{prop:System.Photo.FNumber}", f));
        Assert.Equal("Unknown", Name("{id3:Album|default:Unknown}", f));
        Assert.Equal("ADA", Name("{doc:Author|upper}", f));
    }

    [Fact]
    public void Match_size_category_hash_and_env()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("photo.png", "abc"));
        f.Captures["1"] = "42";
        Assert.Equal("42", Name("{match:1}", f));
        Assert.Equal("3 B", Name("{size}", f));
        Assert.Equal("3", Name("{bytes}", f));
        Assert.Equal("Images", Name("{category}", f));
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Name("{hash:md5}", f));
        Environment.SetEnvironmentVariable("EO_TEST_VAR", "hello");
        Assert.Equal("hello", Name("{env:EO_TEST_VAR}", f));
    }

    [Fact]
    public void Category_is_localized()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("photo.png"));
        Loc.SetLanguage("it");
        try { Assert.Equal("Immagini", Name("{category}", f)); }
        finally { Loc.SetLanguage("en"); }
    }

    [Fact]
    public void Unicode_names_are_preserved()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("città_日本語_émoji😀.txt"));
        Assert.Equal("città_日本語_émoji😀.txt", Name("{name}.{ext}", f));
    }

    [Fact]
    public void Invalid_characters_and_trailing_dots_are_sanitized()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt"));
        Assert.Equal("a_b_c_d", Name("a:b*c?d", f));
        Assert.Equal("name", Name("name...", f));
        Assert.Equal("x_y.txt", Name(@"x\y.{ext}", f));
    }

    [Fact]
    public void Escaped_braces()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt"));
        Assert.Equal("{a}.txt", Name("{{{name}}}.{ext}", f));
    }

    [Fact]
    public void Errors_for_unknown_tokens_filters_and_unclosed()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt"));
        Assert.Throws<TemplateException>(() => Name("{nope}", f));
        Assert.Throws<TemplateException>(() => Name("{name|wat}", f));
        Assert.Throws<TemplateException>(() => Name("{name", f));
        Assert.Throws<TemplateException>(() => Name("{exif:NotAKey}", f));
        Assert.Throws<TemplateException>(() => Name("", f));
    }

    [Fact]
    public void Relative_paths_resolve_against_file_folder_and_system_folders_expand()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File(@"in\a.txt"));
        Assert.Equal(t.Combine("in", "Sorted"), TokenRenderer.RenderPath("Sorted", f, new RunState(), Build.Now));
        Assert.Equal(t.Combine("Up"), TokenRenderer.RenderPath(@"..\Up", f, new RunState(), Build.Now));
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Assert.Equal(System.IO.Path.Combine(desktop, "X"), TokenRenderer.RenderPath(@"{desktop}\X", f, new RunState(), Build.Now));
    }

    [Fact]
    public void Subdir_is_relative_to_root()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File(@"a\b\c.txt"), root: t.Path);
        Assert.Equal(System.IO.Path.Combine("D:\\Backup", "a", "b"), TokenRenderer.RenderPath(@"D:\Backup\{subdir}", f, new RunState(), Build.Now));
    }
}
