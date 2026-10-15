namespace ExplorerOrganizer.Core.Platform;

public interface IMetadataProvider
{
    /// <summary>
    /// Returns the value of a Windows property (canonical name, e.g. "System.Photo.DateTaken")
    /// as <see cref="DateTime"/> (local time) for dates, otherwise as string. Null when missing.
    /// </summary>
    object? Get(string path, string canonicalName);
}

/// <summary>Friendly aliases used in tokens and conditions: exif:*, id3:*, doc:*, prop:System.*.</summary>
public static class MetadataKeys
{
    private static readonly Dictionary<string, string> Exif = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DateTaken"] = "System.Photo.DateTaken",
        ["CameraModel"] = "System.Photo.CameraModel",
        ["Model"] = "System.Photo.CameraModel",
        ["CameraMaker"] = "System.Photo.CameraManufacturer",
        ["Make"] = "System.Photo.CameraManufacturer",
        ["Width"] = "System.Image.HorizontalSize",
        ["Height"] = "System.Image.VerticalSize",
        ["Dimensions"] = "System.Image.Dimensions",
        ["ISO"] = "System.Photo.ISOSpeed",
        ["FNumber"] = "System.Photo.FNumber",
        ["ExposureTime"] = "System.Photo.ExposureTime",
        ["FocalLength"] = "System.Photo.FocalLength",
        ["Orientation"] = "System.Photo.Orientation",
        ["Flash"] = "System.Photo.Flash",
        ["Latitude"] = "System.GPS.Latitude",
        ["Longitude"] = "System.GPS.Longitude",
    };

    private static readonly Dictionary<string, string> Id3 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Artist"] = "System.Music.Artist",
        ["AlbumArtist"] = "System.Music.AlbumArtist",
        ["Album"] = "System.Music.AlbumTitle",
        ["Title"] = "System.Title",
        ["Genre"] = "System.Music.Genre",
        ["Track"] = "System.Music.TrackNumber",
        ["Year"] = "System.Media.Year",
        ["Duration"] = "System.Media.Duration",
        ["Bitrate"] = "System.Audio.EncodingBitrate",
        ["Composer"] = "System.Music.Composer",
    };

    private static readonly Dictionary<string, string> Doc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = "System.Title",
        ["Author"] = "System.Author",
        ["Subject"] = "System.Subject",
        ["Keywords"] = "System.Keywords",
        ["Company"] = "System.Company",
        ["Category"] = "System.Category",
        ["Comment"] = "System.Comment",
        ["Pages"] = "System.Document.PageCount",
        ["LastAuthor"] = "System.Document.LastAuthor",
        ["Created"] = "System.Document.DateCreated",
    };

    public static IReadOnlyDictionary<string, string> ExifAliases => Exif;
    public static IReadOnlyDictionary<string, string> Id3Aliases => Id3;
    public static IReadOnlyDictionary<string, string> DocAliases => Doc;

    /// <summary>Resolves "exif:Model", "id3:Artist", "doc:Author", "prop:System.X" or "System.X" to a canonical name.</summary>
    public static string? Resolve(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        spec = spec.Trim();
        int colon = spec.IndexOf(':');
        if (colon < 0)
            return spec.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ? spec : null;
        var prefix = spec[..colon];
        var key = spec[(colon + 1)..];
        return prefix.ToLowerInvariant() switch
        {
            "exif" => Exif.TryGetValue(key, out var e) ? e : null,
            "id3" => Id3.TryGetValue(key, out var i) ? i : null,
            "doc" => Doc.TryGetValue(key, out var d) ? d : null,
            "prop" => key.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ? key : null,
            _ => null,
        };
    }
}
