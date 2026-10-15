namespace ExplorerOrganizer.Core.Model;

public enum GroupOperator { All, Any }

public enum TextOperator { Is, Contains, StartsWith, EndsWith, Wildcard, Regex, IsEmpty }

public enum NameTarget { Name, NameWithExtension, FullPath, FolderPath }

public enum SizeComparison { GreaterThan, LessThan, Between, EqualTo }

public enum SizeUnit { Bytes, KB, MB, GB }

public enum DateField { Created, Modified, Accessed, Taken }

public enum DateMode { OlderThan, NewerThan, Before, After }

public enum TimeUnit { Minutes, Hours, Days, Weeks, Months, Years }

public enum FileCategory { Image, Video, Audio, Document, Archive, Code, Executable, Other }

public enum ItemKind { File, Folder }

/// <summary>How many of the files in a folder must have the wanted extensions.</summary>
public enum FolderContentMatch { Any, Most, All }

public enum FileAttributeKind { Hidden, ReadOnly, System, Archive }

public enum ConflictPolicy { RenameNew, Overwrite, Skip }

public enum SortMode { ByDate, ByType, ByExtension, ByFirstLetter }

public enum TriggerMode { Realtime, Interval, Daily, Manual }

public enum ThemeMode { System, Light, Dark }
