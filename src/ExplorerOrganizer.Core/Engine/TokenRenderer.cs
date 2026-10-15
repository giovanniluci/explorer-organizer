using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Platform;

namespace ExplorerOrganizer.Core.Engine;

public sealed class TemplateException : Exception
{
    public TemplateException(string message) : base(message) { }
}

/// <summary>Per-run state shared by all files of a run ({counter}).</summary>
public sealed class RunState
{
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);
    public int CounterStart { get; init; } = 1;

    public int NextCounter(string key)
    {
        _counters.TryGetValue(key, out var used);
        _counters[key] = used + 1;
        return CounterStart + used;
    }
}

public enum RenderMode
{
    /// <summary>A single file name: every invalid character (including \ and /) is replaced.</summary>
    FileName,
    /// <summary>A folder path: token values are sanitized, separators in the template are kept; relative paths are resolved against the file folder.</summary>
    Path,
    /// <summary>Command-line arguments: values are inserted as is, except for quotes and trailing backslashes (see EscapeArgument).</summary>
    Raw,
}

/// <summary>
/// Expands templates such as "{pictures}\{taken:yyyy/MM}\{name|lower}.{ext}".
/// Syntax: {token}, {token:argument}, {token|filter|filter:arg}. Use {{ and }} for literal braces.
/// </summary>
public static class TokenRenderer
{
    private static readonly char[] InvalidFileChars = System.IO.Path.GetInvalidFileNameChars();

    public static string RenderFileName(string template, FileContext f, RunState run, DateTime now)
    {
        var s = Render(template, f, run, now, RenderMode.FileName);
        s = SanitizeSegment(s);
        if (s.Length == 0) throw new TemplateException(Loc.T("Err.EmptyName"));
        return s;
    }

    public static string RenderPath(string template, FileContext f, RunState run, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new TemplateException(Loc.T("Err.EmptyDestination"));
        var expanded = Environment.ExpandEnvironmentVariables(template.Trim());
        var s = Render(expanded, f, run, now, RenderMode.Path).Replace('/', '\\');
        return NormalizePath(s, f.Folder);
    }

    public static string Render(string template, FileContext f, RunState run, DateTime now, RenderMode mode)
    {
        var sb = new StringBuilder(template.Length + 32);
        int? counter = null;
        for (int i = 0; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{') { sb.Append('{'); i++; continue; }
                int end = template.IndexOf('}', i + 1);
                if (end < 0) throw new TemplateException(string.Format(Loc.T("Err.UnclosedToken"), template));
                var token = template.Substring(i + 1, end - i - 1);
                sb.Append(Evaluate(token, f, run, now, mode, template, ref counter));
                i = end;
            }
            else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { sb.Append('}'); i++; }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Evaluate(string token, FileContext f, RunState run, DateTime now, RenderMode mode, string template, ref int? counter)
    {
        var parts = token.Split('|');
        var head = parts[0].Trim();
        int colon = head.IndexOf(':');
        var name = (colon < 0 ? head : head[..colon]).Trim().ToLowerInvariant();
        var arg = colon < 0 ? null : head[(colon + 1)..];

        bool isPath = false; // path values keep their separators in Path mode
        string? value;
        switch (name)
        {
            case "name": value = f.NameWithoutExtension; break;
            case "ext": value = f.Extension; break;
            case "fullname": value = f.FileName; break;
            case "path": value = f.LogicalPath; isPath = true; break;
            case "folder": value = f.Folder; isPath = true; break;
            case "parent": value = System.IO.Path.GetFileName(f.Folder.TrimEnd('\\')); break;
            case "drive": value = System.IO.Path.GetPathRoot(f.LogicalPath)?.TrimEnd('\\'); isPath = true; break;
            case "subdir": value = SubDir(f); isPath = true; break;
            case "date":
            case "now": value = FormatDate(now, arg ?? "yyyy-MM-dd"); isPath = true; break;
            case "time": value = FormatDate(now, arg ?? "HH.mm.ss"); isPath = true; break;
            case "created": value = FormatDate(f.Info.CreationTime, arg); isPath = true; break;
            case "modified": value = FormatDate(f.Info.LastWriteTime, arg); isPath = true; break;
            case "accessed": value = FormatDate(f.Info.LastAccessTime, arg); isPath = true; break;
            case "taken": value = FormatDate(f.DateTaken ?? f.Info.LastWriteTime, arg); isPath = true; break;
            case "counter":
                counter ??= run.NextCounter(template);
                int digits = int.TryParse(arg, out var d) ? Math.Clamp(d, 1, 10) : 1;
                value = counter.Value.ToString(new string('0', digits), CultureInfo.InvariantCulture);
                break;
            case "size": value = HumanSize(f.Length); break;
            case "bytes": value = f.Length.ToString(CultureInfo.InvariantCulture); break;
            case "category": value = FileCategories.DisplayName(FileCategories.FromExtension(f.Extension)); break;
            case "exif":
            case "id3":
            case "doc":
            case "prop":
                value = Metadata(name, arg, f, out isPath);
                break;
            case "match":
                if (string.IsNullOrEmpty(arg)) throw new TemplateException(string.Format(Loc.T("Err.TokenNeedsArgument"), name));
                f.Captures.TryGetValue(arg, out value);
                break;
            case "hash": value = Hash(f.PhysicalPath, arg ?? "md5"); break;
            case "env":
                if (string.IsNullOrEmpty(arg)) throw new TemplateException(string.Format(Loc.T("Err.TokenNeedsArgument"), name));
                value = Environment.GetEnvironmentVariable(arg); isPath = true;
                break;
            default:
                value = SystemFolders.Get(name);
                if (value == null) throw new TemplateException(string.Format(Loc.T("Err.UnknownToken"), "{" + token + "}"));
                isPath = true;
                break;
        }

        value = ApplyFilters(value ?? "", parts.Skip(1));
        return mode switch
        {
            RenderMode.Raw => EscapeArgument(value),
            RenderMode.Path when isPath => string.Join('\\', value.Split('\\').Select((seg, idx) => idx == 0 && seg.EndsWith(':') ? seg : SanitizeSegment(seg, trim: false))),
            _ => NoDotSegment(SanitizeSegment(value, trim: false)),
        };
    }

    /// <summary>
    /// A token value inside command-line arguments must stay one argument: double quotes (never valid in file names,
    /// but possible in metadata or {match}) are removed, and trailing backslashes are doubled so that a closing quote
    /// in the template is not escaped (CommandLineToArgvW rules).
    /// </summary>
    private static string EscapeArgument(string value)
    {
        value = value.Replace("\"", "");
        int trailing = value.Length - value.TrimEnd('\\').Length;
        return trailing == 0 ? value : value + new string('\\', trailing);
    }

    /// <summary>A token value made only of dots ("." or "..") would climb out of the destination folder: use underscores.</summary>
    private static string NoDotSegment(string value) =>
        value.Length > 0 && value.Trim().Trim('.').Length == 0 ? new string('_', value.Length) : value;

    private static string? Metadata(string prefix, string? arg, FileContext f, out bool isPath)
    {
        isPath = false;
        if (string.IsNullOrEmpty(arg)) throw new TemplateException(string.Format(Loc.T("Err.TokenNeedsArgument"), prefix));
        string key = arg, format = "";
        // prop:System.Photo.DateTaken:yyyy  /  exif:DateTaken:yyyy-MM
        int c = arg.IndexOf(':');
        if (c >= 0) { key = arg[..c]; format = arg[(c + 1)..]; }
        var canonical = MetadataKeys.Resolve(prefix + ":" + key)
            ?? throw new TemplateException(string.Format(Loc.T("Err.UnknownProperty"), prefix + ":" + key));
        var v = f.GetMetadata(canonical);
        if (v is DateTime dt) { isPath = true; return FormatDate(dt, string.IsNullOrEmpty(format) ? null : format); }
        return v?.ToString();
    }

    private static string ApplyFilters(string value, IEnumerable<string> filters)
    {
        foreach (var raw in filters)
        {
            var filter = raw.Trim();
            int c = filter.IndexOf(':');
            var fname = (c < 0 ? filter : filter[..c]).ToLowerInvariant();
            var farg = c < 0 ? "" : filter[(c + 1)..];
            value = fname switch
            {
                "upper" => value.ToUpper(Loc.Culture),
                "lower" => value.ToLower(Loc.Culture),
                "title" => Loc.Culture.TextInfo.ToTitleCase(value.ToLower(Loc.Culture)),
                "trim" => value.Trim(),
                "default" => string.IsNullOrWhiteSpace(value) ? farg : value,
                "left" => int.TryParse(farg, out var n) && n >= 0 && n < value.Length ? value[..n] : value,
                _ => throw new TemplateException(string.Format(Loc.T("Err.UnknownFilter"), fname)),
            };
        }
        return value;
    }

    /// <summary>Formats a date; "/" in the format creates nested folders ("yyyy/MM" =&gt; "2026\10").</summary>
    public static string FormatDate(DateTime dt, string? format)
    {
        if (string.IsNullOrEmpty(format)) format = "yyyy-MM-dd";
        return string.Join('\\', format.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => dt.ToString(p.Length == 1 ? "%" + p : p, Loc.Culture)));
    }

    private static string SubDir(FileContext f)
    {
        if (f.RootFolder == null) return "";
        var dir = System.IO.Path.GetDirectoryName(f.OriginalPath) ?? "";
        var rel = System.IO.Path.GetRelativePath(f.RootFolder, dir);
        return rel == "." || rel.StartsWith("..", StringComparison.Ordinal) ? "" : rel;
    }

    public static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return (u == 0 ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture)) + " " + units[u];
    }

    private static string Hash(string path, string algo)
    {
        using HashAlgorithm h = algo.ToLowerInvariant() switch
        {
            "md5" => MD5.Create(),
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            _ => throw new TemplateException(string.Format(Loc.T("Err.UnknownHash"), algo)),
        };
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
    }

    /// <summary>Replaces characters that are invalid in a file name and trims trailing dots/spaces (Windows strips them).</summary>
    public static string SanitizeSegment(string s, bool trim = true)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(Array.IndexOf(InvalidFileChars, ch) >= 0 ? '_' : ch);
        var r = sb.ToString();
        return trim ? r.Trim().TrimEnd('.') : r;
    }

    private static string NormalizePath(string s, string baseFolder)
    {
        bool unc = s.StartsWith(@"\\", StringComparison.Ordinal);
        var segments = s.Split('\\');
        var cleaned = new List<string>();
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (i == 0 && seg.Length == 2 && seg[1] == ':') { cleaned.Add(seg); continue; }
            var t = seg.Trim().TrimEnd('.');
            if (seg is "." or "..") t = seg;
            if (t.Length == 0) continue;
            cleaned.Add(t);
        }
        var joined = string.Join('\\', cleaned);
        if (unc) joined = @"\\" + joined;
        else if (cleaned.Count > 0 && cleaned[0].Length == 2 && cleaned[0][1] == ':') joined += cleaned.Count == 1 ? "\\" : "";
        if (!System.IO.Path.IsPathFullyQualified(joined)) joined = System.IO.Path.Combine(baseFolder, joined);
        return System.IO.Path.GetFullPath(joined);
    }

    /// <summary>Token reference shown in the editor (token, localization key of the description).</summary>
    public static IReadOnlyList<(string Token, string HelpKey)> Reference { get; } = new[]
    {
        ("{name}", "Tok.name"), ("{ext}", "Tok.ext"), ("{fullname}", "Tok.fullname"), ("{folder}", "Tok.folder"),
        ("{parent}", "Tok.parent"), ("{subdir}", "Tok.subdir"), ("{date:yyyy-MM-dd}", "Tok.date"),
        ("{created:yyyy-MM-dd}", "Tok.created"), ("{modified:yyyy/MM}", "Tok.modified"), ("{accessed:yyyy}", "Tok.accessed"),
        ("{taken:yyyy/MM}", "Tok.taken"), ("{counter:3}", "Tok.counter"), ("{size}", "Tok.size"), ("{category}", "Tok.category"),
        ("{exif:CameraModel}", "Tok.exif"), ("{id3:Artist}", "Tok.id3"), ("{doc:Author}", "Tok.doc"),
        ("{prop:System.Photo.FNumber}", "Tok.prop"), ("{match:1}", "Tok.match"), ("{hash:md5}", "Tok.hash"),
        ("{env:USERNAME}", "Tok.env"), ("{desktop} {documents} {downloads} {pictures} {music} {videos} {home}", "Tok.folders"),
        ("{name|upper}  {id3:Album|default:Unknown}", "Tok.filters"),
    };
}
