namespace ExplorerOrganizer.Core.Services;

/// <summary>
/// Where data lives. Installed: %AppData%\ExplorerOrganizer. Portable: a "portable.txt" file next to the exe
/// switches to a "data" folder beside it.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    /// <summary>Overrides the data folder (used by the automated UI tests to never touch the user's real data).</summary>
    public const string DataDirectoryVariable = "EXPLORER_ORGANIZER_DATA";

    public static AppPaths Detect()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(overridden)) return new AppPaths(overridden);
        var exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, "portable.txt")))
            return new AppPaths(Path.Combine(exeDir, "data"));
        return new AppPaths(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExplorerOrganizer"));
    }

    public string DataDirectory { get; }
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string ProfilesFile => Path.Combine(DataDirectory, "profiles.json");
    public string JournalFile => Path.Combine(DataDirectory, "undo-journal.json");
    public string StatisticsFile => Path.Combine(DataDirectory, "statistics.json");
    public string LogDirectory => Path.Combine(DataDirectory, "logs");
}
