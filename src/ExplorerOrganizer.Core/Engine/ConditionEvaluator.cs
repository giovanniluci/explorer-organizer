using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

public static class ConditionEvaluator
{
    public static bool Evaluate(Condition condition, FileContext file, DateTime now) => condition switch
    {
        ConditionGroup g => EvaluateGroup(g, file, now),
        AllFilesCondition => true,
        NameCondition n => TextMatcher.IsMatch(NameSubject(n.Target, file), n.Operator, n.Value, n.CaseSensitive, file.Captures),
        NameContainsCondition n => MatchNameContains(n, file),
        ExtensionCondition e => MatchExtension(e, file),
        SizeCondition s => MatchSize(s, file.Length),
        DateCondition d => MatchDate(d, file, now),
        FileTypeCondition t => !file.IsFolder && FileCategories.FromExtension(file.Extension) == t.Category,
        AttributeCondition a => MatchAttribute(a.Attribute, file.Info.Attributes),
        MetadataCondition m => MatchMetadata(m, file),
        ContentCondition c => TextMatcher.IsMatch(file.Content, c.Operator, c.Value, c.CaseSensitive, file.Captures),
        ItemTypeCondition t => t.Kind == (file.IsFolder ? ItemKind.Folder : ItemKind.File),
        FolderContentCondition f => MatchFolderContent(f, file),
        _ => false,
    };

    /// <summary>
    /// Folders are handed only to rules that ask for them explicitly ("item type is folder" or "folder contains files…"),
    /// so existing rules such as "all files" or "older than 30 days" never move whole folders.
    /// </summary>
    public static bool AppliesTo(Rule rule, FileContext file) => !file.IsFolder || TargetsFolders(rule.Condition);

    public static bool TargetsFolders(Condition c) => c switch
    {
        ItemTypeCondition t => t.Kind == ItemKind.Folder,
        FolderContentCondition => true,
        ConditionGroup g => g.Children.Any(TargetsFolders),
        _ => false,
    };

    private static bool EvaluateGroup(ConditionGroup g, FileContext file, DateTime now)
    {
        // An empty group matches nothing: a rule must say explicitly that it wants "all files".
        if (g.Children.Count == 0) return false;
        return g.Operator == GroupOperator.All
            ? g.Children.All(c => Evaluate(c, file, now))
            : g.Children.Any(c => Evaluate(c, file, now));
    }

    private static string NameSubject(NameTarget target, FileContext f) => target switch
    {
        NameTarget.Name => f.NameWithoutExtension,
        NameTarget.NameWithExtension => f.FileName,
        NameTarget.FullPath => f.LogicalPath,
        NameTarget.FolderPath => f.Folder,
        _ => f.FileName,
    };

    public static IEnumerable<string> ParseExtensions(string list) =>
        list.Split(new[] { ';', ',', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimStart('*').TrimStart('.'))
            .Where(x => x.Length > 0);

    public static IEnumerable<string> ParseWords(string list) =>
        list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchNameContains(NameContainsCondition n, FileContext f)
    {
        var name = f.FileName;
        return ParseWords(n.Words).Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchFolderContent(FolderContentCondition c, FileContext f)
    {
        if (!f.IsFolder) return false;
        var content = f.FolderContent;
        if (content.FileCount == 0) return false;
        int n = content.CountWithExtensions(ParseExtensions(c.Extensions));
        return c.Match switch
        {
            FolderContentMatch.Any => n > 0,
            FolderContentMatch.All => n == content.FileCount,
            _ => n * 2 > content.FileCount,
        };
    }

    private static bool MatchExtension(ExtensionCondition e, FileContext f)
    {
        var ext = f.Extension;
        return ParseExtensions(e.Extensions).Any(x => string.Equals(x, ext, StringComparison.OrdinalIgnoreCase));
    }

    public static long ToBytes(double value, SizeUnit unit) => (long)(value * unit switch
    {
        SizeUnit.KB => 1024d,
        SizeUnit.MB => 1024d * 1024,
        SizeUnit.GB => 1024d * 1024 * 1024,
        _ => 1d,
    });

    private static bool MatchSize(SizeCondition s, long size)
    {
        long a = ToBytes(s.Value, s.Unit), b = ToBytes(s.Value2, s.Unit);
        return s.Comparison switch
        {
            SizeComparison.GreaterThan => size > a,
            SizeComparison.LessThan => size < a,
            SizeComparison.EqualTo => size == a,
            SizeComparison.Between => size >= Math.Min(a, b) && size <= Math.Max(a, b),
            _ => false,
        };
    }

    public static DateTime? GetDate(DateField field, FileContext f) => field switch
    {
        DateField.Created => f.Info.CreationTime,
        DateField.Modified => f.Info.LastWriteTime,
        DateField.Accessed => f.Info.LastAccessTime,
        DateField.Taken => f.DateTaken,
        _ => null,
    };

    private static bool MatchDate(DateCondition d, FileContext f, DateTime now)
    {
        var value = GetDate(d.Field, f);
        if (value == null) return false;
        var v = value.Value;
        switch (d.Mode)
        {
            case DateMode.Before: return v < d.Date;
            case DateMode.After: return v > d.Date;
        }
        var threshold = Subtract(now, d.Amount, d.Unit);
        return d.Mode == DateMode.OlderThan ? v < threshold : v >= threshold;
    }

    private static DateTime Subtract(DateTime now, int amount, TimeUnit unit) => unit switch
    {
        TimeUnit.Minutes => now.AddMinutes(-amount),
        TimeUnit.Hours => now.AddHours(-amount),
        TimeUnit.Days => now.AddDays(-amount),
        TimeUnit.Weeks => now.AddDays(-7 * amount),
        TimeUnit.Months => now.AddMonths(-amount),
        TimeUnit.Years => now.AddYears(-amount),
        _ => now,
    };

    private static bool MatchAttribute(FileAttributeKind kind, FileAttributes attrs) => kind switch
    {
        FileAttributeKind.Hidden => attrs.HasFlag(FileAttributes.Hidden),
        FileAttributeKind.ReadOnly => attrs.HasFlag(FileAttributes.ReadOnly),
        FileAttributeKind.System => attrs.HasFlag(FileAttributes.System),
        FileAttributeKind.Archive => attrs.HasFlag(FileAttributes.Archive),
        _ => false,
    };

    private static bool MatchMetadata(MetadataCondition m, FileContext f)
    {
        var key = MetadataKeys.Resolve(m.Property);
        if (key == null) return false;
        var value = f.GetMetadata(key);
        string? text = value switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString(),
        };
        return TextMatcher.IsMatch(text, m.Operator, m.Value, m.CaseSensitive, f.Captures);
    }
}
