using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.Core.Engine;

public static class TextMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly ConcurrentDictionary<(string, bool), Regex?> Cache = new();

    /// <summary>
    /// Tests <paramref name="input"/>. Wildcard accepts several patterns separated by ';'.
    /// Regex captures are written into <paramref name="captures"/> (1, 2, ... and named groups).
    /// </summary>
    public static bool IsMatch(string? input, TextOperator op, string value, bool caseSensitive,
        IDictionary<string, string>? captures = null)
    {
        input ??= "";
        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        switch (op)
        {
            case TextOperator.Is: return string.Equals(input, value, cmp);
            case TextOperator.Contains: return value.Length > 0 && input.Contains(value, cmp);
            case TextOperator.StartsWith: return value.Length > 0 && input.StartsWith(value, cmp);
            case TextOperator.EndsWith: return value.Length > 0 && input.EndsWith(value, cmp);
            case TextOperator.IsEmpty: return string.IsNullOrWhiteSpace(input);
            case TextOperator.Wildcard:
                foreach (var pattern in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var rx = GetRegex(WildcardToRegex(pattern), caseSensitive);
                    if (rx != null && SafeMatch(rx, input).Success) return true;
                }
                return false;
            case TextOperator.Regex:
                {
                    var rx = GetRegex(value, caseSensitive);
                    if (rx == null) return false;
                    var m = SafeMatch(rx, input);
                    if (!m.Success) return false;
                    if (captures != null)
                    {
                        foreach (var name in rx.GetGroupNames())
                        {
                            var g = m.Groups[name];
                            if (name != "0" && g.Success) captures[name] = g.Value;
                        }
                    }
                    return true;
                }
            default: return false;
        }
    }

    /// <summary>Returns an error message when the regex is invalid, otherwise null. Used by the editor.</summary>
    public static string? ValidateRegex(string pattern)
    {
        try { _ = new Regex(pattern, RegexOptions.None, RegexTimeout); return null; }
        catch (ArgumentException ex) { return ex.Message; }
    }

    public static string WildcardToRegex(string pattern) =>
        "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

    private static Regex? GetRegex(string pattern, bool caseSensitive) =>
        Cache.GetOrAdd((pattern, caseSensitive), key =>
        {
            try
            {
                var opts = RegexOptions.CultureInvariant | (key.Item2 ? RegexOptions.None : RegexOptions.IgnoreCase);
                return new Regex(key.Item1, opts, RegexTimeout);
            }
            catch (ArgumentException) { return null; }
        });

    private static Match SafeMatch(Regex rx, string input)
    {
        try { return rx.Match(input); }
        catch (RegexMatchTimeoutException) { return Match.Empty; }
    }
}
