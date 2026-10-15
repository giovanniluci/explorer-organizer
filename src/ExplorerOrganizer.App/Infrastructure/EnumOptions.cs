using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.App.Infrastructure;

public sealed record EnumOption(object Value, string Display)
{
    public override string ToString() => Display;
}

/// <summary>Localized item lists for the combo boxes ({x:Static} at window load).</summary>
public static class EnumOptions
{
    private static IReadOnlyList<EnumOption> Of<T>(string prefix) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new EnumOption(v, Loc.T(prefix + "." + v))).ToList();

    public static IReadOnlyList<EnumOption> GroupOperators => Of<GroupOperator>("Enum.GroupOperator");
    public static IReadOnlyList<EnumOption> TextOperators => Of<TextOperator>("Enum.TextOperator");
    public static IReadOnlyList<EnumOption> NameTargets => Of<NameTarget>("Enum.NameTarget");
    public static IReadOnlyList<EnumOption> SizeComparisons => Of<SizeComparison>("Enum.SizeComparison");
    public static IReadOnlyList<EnumOption> SizeUnits => Enum.GetValues<SizeUnit>().Select(v => new EnumOption(v, v.ToString())).ToList();
    public static IReadOnlyList<EnumOption> DateFields => Of<DateField>("Enum.DateField");
    public static IReadOnlyList<EnumOption> DateModes => Of<DateMode>("Enum.DateMode");
    public static IReadOnlyList<EnumOption> TimeUnits => Of<TimeUnit>("Enum.TimeUnit");
    public static IReadOnlyList<EnumOption> Categories => Enum.GetValues<FileCategory>().Select(v => new EnumOption(v, FileCategories.DisplayName(v))).ToList();
    public static IReadOnlyList<EnumOption> ItemKinds => Of<ItemKind>("Enum.ItemKind");
    public static IReadOnlyList<EnumOption> FolderContentMatches => Of<FolderContentMatch>("Enum.FolderContentMatch");
    public static IReadOnlyList<EnumOption> Attributes => Of<FileAttributeKind>("Enum.FileAttributeKind");
    public static IReadOnlyList<EnumOption> ConflictPolicies => Of<ConflictPolicy>("Enum.ConflictPolicy");
    public static IReadOnlyList<EnumOption> SortModes => Of<SortMode>("Enum.SortMode");
    public static IReadOnlyList<EnumOption> Triggers => Of<TriggerMode>("Enum.TriggerMode");
    public static IReadOnlyList<EnumOption> Themes => Of<ThemeMode>("Enum.ThemeMode");

    public static IReadOnlyList<EnumOption> Languages => new[]
    {
        new EnumOption("", Loc.T("Settings.LanguageSystem")),
        new EnumOption("it", "Italiano"),
        new EnumOption("en", "English"),
    };
}
