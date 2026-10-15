using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Services;

public enum PresetKind { Downloads, Photos, Documents, Music }

/// <summary>Ready-made profiles offered by the first-run wizard.</summary>
public static class Presets
{
    public static Profile Create(PresetKind kind, TriggerMode trigger = TriggerMode.Manual) => kind switch
    {
        PresetKind.Downloads => Downloads(trigger),
        PresetKind.Photos => Photos(trigger),
        PresetKind.Documents => Documents(trigger),
        _ => Music(trigger),
    };

    public static string DefaultFolder(PresetKind kind) => kind switch
    {
        PresetKind.Downloads => "{downloads}",
        PresetKind.Photos => "{pictures}",
        PresetKind.Documents => "{documents}",
        _ => "{music}",
    };

    private static Profile Downloads(TriggerMode trigger)
    {
        var p = NewProfile(Loc.T("Preset.Downloads"), PresetKind.Downloads, trigger);
        p.Rules.Add(MoveByCategory("Preset.Rule.Images", FileCategory.Image));
        p.Rules.Add(MoveByCategory("Preset.Rule.Documents", FileCategory.Document));
        p.Rules.Add(MoveByCategory("Preset.Rule.Archives", FileCategory.Archive));
        p.Rules.Add(MoveByCategory("Preset.Rule.Programs", FileCategory.Executable));
        var media = new Rule { Name = Loc.T("Preset.Rule.Media") };
        media.Condition.Operator = GroupOperator.Any;
        media.Condition.Children.Add(new FileTypeCondition { Category = FileCategory.Video });
        media.Condition.Children.Add(new FileTypeCondition { Category = FileCategory.Audio });
        media.Actions.Add(new SortIntoSubfoldersAction { BaseFolder = "{folder}", Mode = SortMode.ByType });
        p.Rules.Add(media);
        return p;
    }

    private static Profile Photos(TriggerMode trigger)
    {
        var p = NewProfile(Loc.T("Preset.Photos"), PresetKind.Photos, trigger);
        var r = new Rule { Name = Loc.T("Preset.Rule.PhotosByDate") };
        r.Condition.Children.Add(new FileTypeCondition { Category = FileCategory.Image });
        r.Actions.Add(new SortIntoSubfoldersAction { BaseFolder = "{folder}", Mode = SortMode.ByDate, DateSource = DateField.Taken, DateFormat = "yyyy/MM" });
        p.Rules.Add(r);
        return p;
    }

    private static Profile Documents(TriggerMode trigger)
    {
        var p = NewProfile(Loc.T("Preset.Documents"), PresetKind.Documents, trigger);
        var pdf = new Rule { Name = Loc.T("Preset.Rule.Pdf") };
        pdf.Condition.Children.Add(new ExtensionCondition { Extensions = "pdf" });
        pdf.Actions.Add(new MoveAction { Destination = "{folder}\\PDF\\{modified:yyyy}" });
        p.Rules.Add(pdf);
        var office = new Rule { Name = Loc.T("Preset.Rule.Office") };
        office.Condition.Children.Add(new ExtensionCondition { Extensions = "doc;docx;rtf;odt;xls;xlsx;csv;ods;ppt;pptx;odp" });
        office.Actions.Add(new SortIntoSubfoldersAction { BaseFolder = "{folder}\\Office", Mode = SortMode.ByExtension });
        p.Rules.Add(office);
        return p;
    }

    private static Profile Music(TriggerMode trigger)
    {
        var p = NewProfile(Loc.T("Preset.Music"), PresetKind.Music, trigger);
        var r = new Rule { Name = Loc.T("Preset.Rule.MusicByArtist") };
        r.Condition.Children.Add(new FileTypeCondition { Category = FileCategory.Audio });
        r.Actions.Add(new MoveAction
        {
            Destination = "{folder}\\{id3:Artist|default:" + Loc.T("Preset.UnknownArtist") + "}\\{id3:Album|default:" + Loc.T("Preset.UnknownAlbum") + "}",
        });
        p.Rules.Add(r);
        return p;
    }

    private static Profile NewProfile(string name, PresetKind kind, TriggerMode trigger)
    {
        var p = new Profile { Name = name };
        p.Folders.Add(new WatchedFolder { Path = DefaultFolder(kind), Trigger = trigger, IncludeSubfolders = false });
        return p;
    }

    private static Rule MoveByCategory(string nameKey, FileCategory category)
    {
        var r = new Rule { Name = Loc.T(nameKey) };
        r.Condition.Children.Add(new FileTypeCondition { Category = category });
        r.Actions.Add(new MoveAction { Destination = "{folder}\\{category}" });
        return r;
    }
}
