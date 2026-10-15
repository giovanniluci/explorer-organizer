using System.Globalization;

namespace ExplorerOrganizer.Core.Localization;

/// <summary>Tiny two-language (Italian / English) string table shared by the engine and the UI.</summary>
public static class Loc
{
    private static readonly Dictionary<string, (string En, string It)> Table = new(StringComparer.Ordinal);

    static Loc()
    {
        Register(CoreStrings.All);
        SetLanguage("");
    }

    public static string Language { get; private set; } = "en";
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");
    public static event EventHandler? LanguageChanged;

    public static IEnumerable<string> Keys => Table.Keys;

    /// <summary>"it", "en", or "" for the Windows display language (Italian when it is Italian, English otherwise).</summary>
    public static void SetLanguage(string? language)
    {
        var lang = string.IsNullOrWhiteSpace(language)
            ? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en")
            : language.ToLowerInvariant() == "it" ? "it" : "en";
        Language = lang;
        Culture = CultureInfo.GetCultureInfo(lang == "it" ? "it-IT" : "en-US");
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Register(IEnumerable<KeyValuePair<string, (string En, string It)>> strings)
    {
        foreach (var kv in strings) Table[kv.Key] = kv.Value;
    }

    public static string T(string key) =>
        Table.TryGetValue(key, out var v) ? (Language == "it" ? v.It : v.En) : key;
}
