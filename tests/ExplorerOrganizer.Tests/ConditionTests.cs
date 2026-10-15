using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Tests;

public class ConditionTests
{
    private static bool Eval(Condition c, FileContext f) => ConditionEvaluator.Evaluate(c, f, Build.Now);

    [Theory]
    [InlineData(TextOperator.Contains, "port", true)]
    [InlineData(TextOperator.Contains, "PORT", true)]
    [InlineData(TextOperator.StartsWith, "Rep", true)]
    [InlineData(TextOperator.EndsWith, ".pdf", true)]
    [InlineData(TextOperator.Is, "Report 2024.pdf", true)]
    [InlineData(TextOperator.Is, "Report", false)]
    [InlineData(TextOperator.Wildcard, "rep*.pdf", true)]
    [InlineData(TextOperator.Wildcard, "*.doc;*.pdf", true)]
    [InlineData(TextOperator.Wildcard, "rep?.pdf", false)]
    [InlineData(TextOperator.Regex, @"^Report \d{4}\.pdf$", true)]
    [InlineData(TextOperator.Regex, @"^\d+$", false)]
    public void Name_operators(TextOperator op, string value, bool expected)
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("Report 2024.pdf"));
        Assert.Equal(expected, Eval(new NameCondition { Operator = op, Value = value }, f));
    }

    [Fact]
    public void Name_case_sensitive_is_respected()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("Invoice.txt"));
        Assert.False(Eval(new NameCondition { Operator = TextOperator.Contains, Value = "invoice", CaseSensitive = true }, f));
        Assert.True(Eval(new NameCondition { Operator = TextOperator.Contains, Value = "Invoice", CaseSensitive = true }, f));
    }

    [Fact]
    public void Regex_captures_are_available_for_tokens()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("INV-0042_acme.pdf"));
        Assert.True(Eval(new NameCondition { Target = NameTarget.Name, Operator = TextOperator.Regex, Value = @"INV-(\d+)_(?<client>\w+)" }, f));
        Assert.Equal("0042", f.Captures["1"]);
        Assert.Equal("acme", f.Captures["client"]);
    }

    [Fact]
    public void Invalid_regex_never_matches_and_is_reported()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt"));
        Assert.False(Eval(new NameCondition { Operator = TextOperator.Regex, Value = "([" }, f));
        Assert.NotNull(TextMatcher.ValidateRegex("(["));
        Assert.Null(TextMatcher.ValidateRegex("a+"));
    }

    [Fact]
    public void Extension_list_ignores_dots_case_and_spaces()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("photo.JPG"));
        Assert.True(Eval(new ExtensionCondition { Extensions = ".png; jpg ,gif" }, f));
        Assert.False(Eval(new ExtensionCondition { Extensions = "jpeg" }, f));
    }

    [Theory]
    [InlineData(SizeComparison.GreaterThan, 1, 0, true)]
    [InlineData(SizeComparison.LessThan, 1, 0, false)]
    [InlineData(SizeComparison.Between, 1, 3, true)]
    [InlineData(SizeComparison.Between, 3, 4, false)]
    public void Size_in_kilobytes(SizeComparison cmp, double v1, double v2, bool expected)
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("big.bin", new string('a', 2048))); // 2 KB
        Assert.Equal(expected, Eval(new SizeCondition { Comparison = cmp, Value = v1, Value2 = v2, Unit = SizeUnit.KB }, f));
    }

    [Fact]
    public void Date_relative_and_absolute()
    {
        using var t = new TempDir();
        var old = Build.Context(t.File("old.txt", modified: Build.Now.AddDays(-40)));
        var fresh = Build.Context(t.File("new.txt", modified: Build.Now.AddDays(-2)));
        var olderThan30 = new DateCondition { Field = DateField.Modified, Mode = DateMode.OlderThan, Amount = 30, Unit = TimeUnit.Days };
        Assert.True(Eval(olderThan30, old));
        Assert.False(Eval(olderThan30, fresh));
        var newer = new DateCondition { Field = DateField.Modified, Mode = DateMode.NewerThan, Amount = 1, Unit = TimeUnit.Weeks };
        Assert.True(Eval(newer, fresh));
        Assert.True(Eval(new DateCondition { Field = DateField.Modified, Mode = DateMode.Before, Date = Build.Now.AddDays(-10) }, old));
        Assert.True(Eval(new DateCondition { Field = DateField.Modified, Mode = DateMode.After, Date = Build.Now.AddDays(-10) }, fresh));
    }

    [Fact]
    public void Date_taken_uses_metadata_and_is_false_when_missing()
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values["System.Photo.DateTaken"] = new DateTime(2019, 5, 1);
        var cond = new DateCondition { Field = DateField.Taken, Mode = DateMode.Before, Date = new DateTime(2020, 1, 1) };
        Assert.True(Eval(cond, Build.Context(t.File("a.jpg"), meta)));
        Assert.False(Eval(cond, Build.Context(t.File("b.jpg"))));
    }

    [Fact]
    public void File_type_category()
    {
        using var t = new TempDir();
        Assert.True(Eval(new FileTypeCondition { Category = FileCategory.Image }, Build.Context(t.File("x.HEIC"))));
        Assert.True(Eval(new FileTypeCondition { Category = FileCategory.Archive }, Build.Context(t.File("x.7z"))));
        Assert.True(Eval(new FileTypeCondition { Category = FileCategory.Other }, Build.Context(t.File("x.unknownext"))));
    }

    [Fact]
    public void Attribute_condition()
    {
        using var t = new TempDir();
        var path = t.File("ro.txt");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.True(Eval(new AttributeCondition { Attribute = FileAttributeKind.ReadOnly }, Build.Context(path)));
            Assert.False(Eval(new AttributeCondition { Attribute = FileAttributeKind.Hidden }, Build.Context(path)));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }

    [Fact]
    public void Metadata_condition_with_alias_and_is_empty()
    {
        using var t = new TempDir();
        var meta = new FakeMetadata();
        meta.Values["System.Music.Artist"] = "Pink Floyd";
        var f = Build.Context(t.File("song.mp3"), meta);
        Assert.True(Eval(new MetadataCondition { Property = "id3:Artist", Operator = TextOperator.Contains, Value = "floyd" }, f));
        Assert.True(Eval(new MetadataCondition { Property = "id3:Album", Operator = TextOperator.IsEmpty }, f));
        Assert.False(Eval(new MetadataCondition { Property = "nope:Thing", Operator = TextOperator.IsEmpty }, f));
    }

    [Fact]
    public void Content_condition()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("note.txt", "Invoice number 123 from ACME"));
        Assert.True(Eval(new ContentCondition { Operator = TextOperator.Contains, Value = "acme" }, f));
        Assert.True(Eval(new ContentCondition { Operator = TextOperator.Regex, Value = @"number (\d+)" }, f));
        Assert.Equal("123", f.Captures["1"]);
    }

    [Fact]
    public void Groups_and_or_nested()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("holiday.jpg", new string('a', 2048)));
        var isJpg = new ExtensionCondition { Extensions = "jpg" };
        var isPdf = new ExtensionCondition { Extensions = "pdf" };
        var big = new SizeCondition { Comparison = SizeComparison.GreaterThan, Value = 1, Unit = SizeUnit.MB };

        var and = new ConditionGroup { Operator = GroupOperator.All, Children = { isJpg, big } };
        var or = new ConditionGroup { Operator = GroupOperator.Any, Children = { isPdf, isJpg } };
        var small = new SizeCondition { Comparison = SizeComparison.LessThan, Value = 1, Unit = SizeUnit.MB };
        var nested = new ConditionGroup
        {
            Operator = GroupOperator.All,
            Children = { or, small, new ConditionGroup { Operator = GroupOperator.Any, Children = { isJpg, isPdf } } },
        };

        Assert.False(Eval(and, f));
        Assert.True(Eval(or, f));
        Assert.True(Eval(nested, f));
        Assert.False(Eval(new ConditionGroup { Operator = GroupOperator.All, Children = { isJpg, isPdf } }, f));
    }

    [Fact]
    public void Empty_group_matches_nothing_but_all_files_matches_everything()
    {
        using var t = new TempDir();
        var f = Build.Context(t.File("a.txt"));
        Assert.False(Eval(new ConditionGroup(), f));
        Assert.True(Eval(new ConditionGroup { Children = { new AllFilesCondition() } }, f));
    }
}
