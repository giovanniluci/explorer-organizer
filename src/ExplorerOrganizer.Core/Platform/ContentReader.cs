using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ExplorerOrganizer.Core.Platform;

public interface IContentReader
{
    /// <summary>Extracts searchable text (best effort). Returns "" when the format is not supported.</summary>
    string Read(string path);
}

/// <summary>Text extraction for plain text, Office Open XML, OpenDocument, RTF and simple PDFs. No external libraries.</summary>
public sealed class ContentReader : IContentReader
{
    public const int MaxChars = 2_000_000;
    private const long MaxFileBytes = 50L * 1024 * 1024;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "md", "csv", "tsv", "log", "json", "xml", "html", "htm", "ini", "cfg", "conf", "yaml", "yml",
        "cs", "js", "ts", "py", "java", "c", "cpp", "h", "hpp", "sql", "ps1", "bat", "cmd", "sh", "css", "eml", "srt",
    };

    public string Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes) return "";
            var ext = info.Extension.TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "docx" or "docm" or "dotx" => ReadZipXml(path, e => e.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)),
                "xlsx" or "xlsm" => ReadZipXml(path, e => e.FullName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase)),
                "pptx" or "pptm" => ReadZipXml(path, e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)),
                "odt" or "ods" or "odp" => ReadZipXml(path, e => e.FullName.Equals("content.xml", StringComparison.OrdinalIgnoreCase)),
                "rtf" => StripRtf(ReadText(path)),
                "pdf" => PdfText.Extract(path),
                _ when TextExtensions.Contains(ext) => ReadText(path),
                _ => LooksLikeText(path) ? ReadText(path) : "",
            };
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string ReadText(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaxChars];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private static bool LooksLikeText(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buf = new byte[8192];
        int n = fs.Read(buf, 0, buf.Length);
        if (n == 0) return false;
        for (int i = 0; i < n; i++) if (buf[i] == 0) return false;
        return true;
    }

    private static string ReadZipXml(string path, Func<ZipArchiveEntry, bool> select)
    {
        using var zip = ZipFile.OpenRead(path);
        var sb = new StringBuilder();
        foreach (var entry in zip.Entries.Where(select).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var s = entry.Open();
            using var xr = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            while (xr.Read() && sb.Length < MaxChars)
            {
                if (xr.NodeType is XmlNodeType.Text or XmlNodeType.CDATA) sb.Append(xr.Value);
                else if (xr.NodeType == XmlNodeType.EndElement && xr.LocalName is "p" or "si" or "tab" or "br") sb.Append(' ');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string StripRtf(string rtf)
    {
        var noGroups = Regex.Replace(rtf, @"\{\\\*[^{}]*\}", "", RegexOptions.None, TimeSpan.FromSeconds(2));
        var noControls = Regex.Replace(noGroups, @"\\[a-zA-Z]+-?\d* ?|\\'[0-9a-fA-F]{2}|[{}]", " ", RegexOptions.None, TimeSpan.FromSeconds(2));
        return Regex.Replace(noControls, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(2)).Trim();
    }
}

/// <summary>Minimal PDF text extractor: decompresses Flate content streams and collects Tj/TJ strings. Best effort.</summary>
internal static class PdfText
{
    public static string Extract(string path)
    {
        var data = File.ReadAllBytes(path);
        var latin = Encoding.Latin1.GetString(data);
        var sb = new StringBuilder();
        int pos = 0;
        while (sb.Length < ContentReader.MaxChars)
        {
            int s = latin.IndexOf("stream", pos, StringComparison.Ordinal);
            if (s < 0) break;
            int dictStart = latin.LastIndexOf("<<", s, Math.Min(s, 2048), StringComparison.Ordinal);
            string dict = dictStart >= 0 ? latin[dictStart..s] : "";
            int start = s + 6;
            if (start < latin.Length && latin[start] == '\r') start++;
            if (start < latin.Length && latin[start] == '\n') start++;
            int end = latin.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0) break;
            pos = end + 9;
            if (dict.Contains("/Image", StringComparison.Ordinal)) continue;

            byte[] raw = data.AsSpan(start, end - start).ToArray();
            string content;
            try
            {
                if (dict.Contains("/FlateDecode", StringComparison.Ordinal))
                {
                    using var input = new MemoryStream(raw);
                    using var z = new ZLibStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    z.CopyTo(output);
                    content = Encoding.Latin1.GetString(output.ToArray());
                }
                else if (dict.Contains("/Filter", StringComparison.Ordinal)) continue;
                else content = Encoding.Latin1.GetString(raw);
            }
            catch { continue; }
            ExtractStrings(content, sb);
        }
        return sb.ToString();
    }

    private static void ExtractStrings(string content, StringBuilder sb)
    {
        if (!content.Contains("BT", StringComparison.Ordinal)) return;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] != '(') continue;
            int depth = 1;
            var part = new StringBuilder();
            i++;
            for (; i < content.Length && depth > 0; i++)
            {
                char c = content[i];
                if (c == '\\' && i + 1 < content.Length)
                {
                    char n = content[++i];
                    switch (n)
                    {
                        case 'n': part.Append('\n'); break;
                        case 'r': part.Append(' '); break;
                        case 't': part.Append(' '); break;
                        case '(': case ')': case '\\': part.Append(n); break;
                        default:
                            if (n >= '0' && n <= '7')
                            {
                                int v = n - '0', k = 0;
                                while (k < 2 && i + 1 < content.Length && content[i + 1] >= '0' && content[i + 1] <= '7') { v = v * 8 + (content[++i] - '0'); k++; }
                                part.Append((char)v);
                            }
                            break;
                    }
                }
                else if (c == '(') { depth++; part.Append(c); }
                else if (c == ')') { depth--; if (depth > 0) part.Append(c); }
                else part.Append(c);
            }
            sb.Append(part).Append(' ');
            i--;
        }
    }
}
