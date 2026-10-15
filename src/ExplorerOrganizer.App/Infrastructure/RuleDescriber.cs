using System.Globalization;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Condition = ExplorerOrganizer.Core.Model.Condition;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>Human-readable summaries shown in rule and folder lists.</summary>
public static class RuleDescriber
{
    private static string E(object v) => Loc.T("Enum." + v.GetType().Name + "." + v);

    public static string Describe(Rule r)
    {
        var cond = Describe(r.Condition, root: true);
        var acts = r.Actions.Count == 0 ? Loc.T("Describe.NoActions") : string.Join(", ", r.Actions.Select(Describe));
        return $"{Loc.T("Editor.If").ToUpper(Loc.Culture)} {cond}  {Loc.T("Editor.Then").ToUpper(Loc.Culture)} {acts}";
    }

    public static string Describe(Condition c, bool root = false) => c switch
    {
        ConditionGroup g when g.Children.Count == 0 => Loc.T("Describe.Nothing"),
        ConditionGroup g when g.Children.Count == 1 && root => Describe(g.Children[0]),
        ConditionGroup g => "(" + string.Join(g.Operator == GroupOperator.All ? " " + Loc.T("Describe.And") + " " : " " + Loc.T("Describe.Or") + " ",
            g.Children.Select(x => Describe(x))) + ")",
        AllFilesCondition => Loc.T("Cond.AllFiles"),
        NameCondition n => $"{E(n.Target)} {E(n.Operator)} \"{n.Value}\"",
        ExtensionCondition e => $"{Loc.T("Cond.Extension")} = {e.Extensions}",
        SizeCondition z => z.Comparison == SizeComparison.Between
            ? $"{Loc.T("Cond.Size")} {E(z.Comparison)} {z.Value}–{z.Value2} {z.Unit}"
            : $"{Loc.T("Cond.Size")} {E(z.Comparison)} {z.Value} {z.Unit}",
        DateCondition d => d.Mode is DateMode.Before or DateMode.After
            ? $"{E(d.Field)} {E(d.Mode)} {d.Date.ToString("d", Loc.Culture)}"
            : $"{E(d.Field)} {E(d.Mode)} {d.Amount} {E(d.Unit)}",
        FileTypeCondition t => $"{Loc.T("Cond.FileType")} = {FileCategories.DisplayName(t.Category)}",
        AttributeCondition a => $"{Loc.T("Cond.Attribute")} {E(a.Attribute)}",
        MetadataCondition m => $"{m.Property} {E(m.Operator)} \"{m.Value}\"",
        ContentCondition t => $"{Loc.T("Cond.Content")} {E(t.Operator)} \"{t.Value}\"",
        NameContainsCondition n => $"{Loc.T("Cond.NameContains")} \"{n.Words}\"",
        ItemTypeCondition i => $"{Loc.T("Cond.ItemTypeIs")} {E(i.Kind)}",
        FolderContentCondition f => $"{Loc.T("Cond.FolderContentPrefix")} {E(f.Match)} {Loc.T("Cond.FolderContentSuffix")} {f.Extensions}",
        _ => "?",
    };

    public static string Describe(RuleAction a) => a switch
    {
        MoveAction m => $"{Loc.T("Act.Move")} → {m.Destination}",
        CopyAction c => $"{Loc.T("Act.Copy")} → {c.Destination}",
        RenameAction r => $"{Loc.T("Act.Rename")} → {r.NewName}",
        DeleteAction d => d.Permanent ? Loc.T("Act.DeletePermanent") : Loc.T("Act.Delete"),
        CompressAction z => $"{Loc.T("Act.Compress")} → {z.ArchivePath}",
        ExtractAction x => $"{Loc.T("Act.Extract")} → {x.Destination}",
        CreateShortcutAction l => $"{Loc.T("Act.Shortcut")} → {l.Destination}",
        OpenWithAction o => $"{Loc.T("Act.OpenWith")} {System.IO.Path.GetFileName(o.Application)}",
        SortIntoSubfoldersAction s => $"{Loc.T("Act.Sort")} ({E(s.Mode)})",
        _ => "?",
    };

    public static string Describe(WatchedFolder f)
    {
        var trigger = f.Trigger switch
        {
            TriggerMode.Interval => string.Format(Loc.T("Describe.Every"), f.IntervalMinutes),
            TriggerMode.Daily => string.Format(Loc.T("Describe.DailyAt"), f.DailyTime),
            _ => E(f.Trigger),
        };
        var parts = new List<string> { trigger };
        if (f.IncludeSubfolders) parts.Add(Loc.T("Folder.IncludeSubfolders"));
        if (!f.Enabled) parts.Add(Loc.T("Describe.Disabled"));
        var resolved = FolderMonitor.ResolveFolderPath(f.Path);
        if (!string.Equals(resolved, f.Path, StringComparison.OrdinalIgnoreCase)) parts.Add(resolved);
        if (f.LastRun != null) parts.Add(Loc.T("Describe.LastRun") + " " + f.LastRun.Value.ToString("g", Loc.Culture));
        return string.Join(" · ", parts);
    }
}
