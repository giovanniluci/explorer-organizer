using System.Text.Json;
using System.Text.Json.Nodes;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Services;

public static class JsonStore
{
    public static T Load<T>(string file, Func<T> fallback)
    {
        try
        {
            if (File.Exists(file))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Json.Options) ?? fallback();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            // keep the unreadable file for the user, start with defaults
            try { File.Copy(file, file + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: false); } catch { }
        }
        return fallback();
    }

    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// Loads the profile library, migrating rules saved by older versions (NOT checkbox). When something is migrated
    /// the original file is kept as "profiles.json.bak-…" and the migrated library is saved.
    /// </summary>
    public static ProfileLibrary LoadProfiles(string file, Func<ProfileLibrary> fallback, List<MigrationNote> notes)
    {
        try
        {
            if (File.Exists(file))
            {
                var node = JsonNode.Parse(File.ReadAllText(file), documentOptions: DocumentOptions);
                if (node == null) return fallback();
                bool migrated = LegacyNotMigration.Migrate(node, notes);
                var library = node.Deserialize<ProfileLibrary>(Json.Options) ?? fallback();
                if (migrated)
                {
                    try { File.Copy(file, file + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: false); } catch (IOException) { }
                    Save(file, library);
                }
                return library;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or InvalidOperationException)
        {
            try { File.Copy(file, file + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: false); } catch { }
        }
        return fallback();
    }

    /// <summary>Atomic write: temp file + replace, so a crash never leaves a half-written configuration.</summary>
    public static void Save<T>(string file, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json.Options));
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>Export format: a single profile in JSON.</summary>
    public static void ExportProfile(Profile profile, string file) => Save(file, profile);

    /// <summary>Imports a profile. New ids are generated so an import never clashes with existing profiles.</summary>
    public static Profile ImportProfile(string file) => ImportProfile(file, out _);

    /// <summary>
    /// Imports a profile. Folders start disabled, and so do rules that launch programs ("Open with") or delete
    /// permanently: a profile from someone else must be reviewed before it can run anything.
    /// </summary>
    public static Profile ImportProfile(string file, out int disabledRules)
    {
        var node = JsonNode.Parse(File.ReadAllText(file), documentOptions: DocumentOptions) ?? throw new InvalidDataException("Empty profile");
        LegacyNotMigration.Migrate(node, new List<MigrationNote>());
        var profile = node.Deserialize<Profile>(Json.Options) ?? throw new InvalidDataException("Empty profile");
        profile.Id = Guid.NewGuid();
        foreach (var r in profile.Rules) r.Id = Guid.NewGuid();
        foreach (var f in profile.Folders) { f.Id = Guid.NewGuid(); f.LastRun = null; f.Enabled = false; }
        disabledRules = 0;
        foreach (var r in profile.Rules.Where(r => r.Enabled && r.Actions.Any(a => a is OpenWithAction or DeleteAction { Permanent: true })))
        {
            r.Enabled = false;
            disabledRules++;
        }
        return profile;
    }
}
