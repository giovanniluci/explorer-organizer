using System.Text.RegularExpressions;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Services;

/// <summary>
/// Ready-made rules (one per category) added once, at the first start: they move files into numbered subfolders of
/// Downloads ("2. Video", "3. Foto"...). Numbers already used by existing folders or rules ("1. PDF", "9. Fonts") are
/// never reused, an existing folder of the same category is reused, and a category the user already handles is skipped.
/// </summary>
public static class DefaultRules
{
    public sealed record Category(string Name, string Extensions);

    public static readonly IReadOnlyList<Category> Categories = new Category[]
    {
        new("Video", "mp4; mkv; avi; mov; wmv; flv; webm; m4v; mpg; mpeg"),
        new("Foto", "jpg; jpeg; heic; heif; raw; cr2; nef; arw; dng"),
        new("Immagini", "png; gif; bmp; svg; webp; ico; tif; tiff; psd; ai"),
        new("PDF", "pdf"),
        new("Archivi", "zip; rar; 7z; tar; gz; bz2; xz; iso"),
        new("Programmi", "exe; msi; msix; appx; bat; apk"),
    };

    private static readonly Regex Numbered = new(@"^\s*(\d+)\.\s*(.+?)\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Adds the missing default rules to the profile that watches <paramref name="downloads"/> (or the active/first one;
    /// a new "Download" profile when there is none). Returns the rules added.
    /// </summary>
    public static IReadOnlyList<Rule> Seed(ProfileLibrary library, string downloads)
    {
        downloads = Path.TrimEndingDirectorySeparator(Path.GetFullPath(downloads));
        var profile = library.Profiles.FirstOrDefault(p => p.Folders.Any(f => SamePath(FolderMonitor.ResolveFolderPath(f.Path), downloads)))
                      ?? library.Profiles.FirstOrDefault(p => p.Id == library.ActiveProfileId)
                      ?? library.Profiles.FirstOrDefault();
        if (profile == null)
        {
            profile = new Profile { Name = Loc.T("Preset.Downloads") };
            profile.Folders.Add(new WatchedFolder { Path = "{downloads}", Trigger = TriggerMode.Manual });
            library.Profiles.Add(profile);
            library.ActiveProfileId = profile.Id;
        }

        // numbered folders already used: real subfolders of Downloads and destinations of existing rules
        var used = new HashSet<int>();
        var folderByLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(downloads))
                Register(Path.GetFileName(dir), used, folderByLabel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var handledLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var move in library.Profiles.SelectMany(p => p.Rules).SelectMany(r => r.Actions).OfType<MoveAction>())
        {
            var dest = Path.TrimEndingDirectorySeparator(move.Destination.Trim());
            var label = Register(Path.GetFileName(dest), used, folderByLabel);
            if (label != null) handledLabels.Add(label);
        }

        var added = new List<Rule>();
        foreach (var cat in Categories)
        {
            bool exists = profile.Rules.Any(r => string.Equals(r.Name.Trim(), cat.Name, StringComparison.OrdinalIgnoreCase))
                          || handledLabels.Contains(cat.Name);
            if (exists) continue;
            if (!folderByLabel.TryGetValue(cat.Name, out var folderName))
            {
                int n = 1;
                while (used.Contains(n)) n++;
                used.Add(n);
                folderName = $"{n}. {cat.Name}";
                folderByLabel[cat.Name] = folderName;
            }
            var rule = new Rule { Name = cat.Name };
            rule.Condition.Children.Add(new ExtensionCondition { Extensions = cat.Extensions });
            rule.Actions.Add(new MoveAction { Destination = Path.Combine(downloads, folderName), OnConflict = ConflictPolicy.RenameNew });
            profile.Rules.Add(rule);
            added.Add(rule);
        }
        return added;
    }

    /// <summary>"9. Fonts" → marks 9 as used and returns "Fonts"; other names return null.</summary>
    private static string? Register(string folderName, HashSet<int> used, Dictionary<string, string> folderByLabel)
    {
        var m = Numbered.Match(folderName);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var n)) return null;
        used.Add(n);
        var label = m.Groups[2].Value;
        folderByLabel.TryAdd(label, folderName);
        return label;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
}
