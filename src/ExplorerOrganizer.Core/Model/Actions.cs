using System.Text.Json.Serialization;

namespace ExplorerOrganizer.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveAction), "move")]
[JsonDerivedType(typeof(CopyAction), "copy")]
[JsonDerivedType(typeof(RenameAction), "rename")]
[JsonDerivedType(typeof(DeleteAction), "delete")]
[JsonDerivedType(typeof(CompressAction), "compress")]
[JsonDerivedType(typeof(ExtractAction), "extract")]
[JsonDerivedType(typeof(CreateShortcutAction), "shortcut")]
[JsonDerivedType(typeof(OpenWithAction), "openWith")]
[JsonDerivedType(typeof(SortIntoSubfoldersAction), "sort")]
public abstract class RuleAction : ObservableObject { }

public abstract class ConflictAwareAction : RuleAction
{
    private ConflictPolicy _onConflict = ConflictPolicy.RenameNew;
    public ConflictPolicy OnConflict { get => _onConflict; set => Set(ref _onConflict, value); }
}

public sealed class MoveAction : ConflictAwareAction
{
    private string _destination = "";
    private bool _removeEmptySourceFolder;
    /// <summary>Destination folder template, e.g. "{pictures}\{taken:yyyy}".</summary>
    public string Destination { get => _destination; set => Set(ref _destination, value ?? ""); }
    public bool RemoveEmptySourceFolder { get => _removeEmptySourceFolder; set => Set(ref _removeEmptySourceFolder, value); }
}

public sealed class CopyAction : ConflictAwareAction
{
    private string _destination = "";
    public string Destination { get => _destination; set => Set(ref _destination, value ?? ""); }
}

public sealed class RenameAction : ConflictAwareAction
{
    private string _newName = "{name}.{ext}";
    /// <summary>New file name template including the extension, e.g. "{modified:yyyy-MM-dd} {name}.{ext}".</summary>
    public string NewName { get => _newName; set => Set(ref _newName, value ?? ""); }
}

public sealed class DeleteAction : RuleAction
{
    private bool _permanent;
    private bool _removeEmptySourceFolder;
    /// <summary>When false (default) the file goes to the Recycle Bin. Permanent deletion always asks for confirmation.</summary>
    public bool Permanent { get => _permanent; set => Set(ref _permanent, value); }
    public bool RemoveEmptySourceFolder { get => _removeEmptySourceFolder; set => Set(ref _removeEmptySourceFolder, value); }
}

public sealed class CompressAction : ConflictAwareAction
{
    private string _archivePath = "{folder}\\{name}.zip";
    private bool _deleteSource;
    public string ArchivePath { get => _archivePath; set => Set(ref _archivePath, value ?? ""); }
    /// <summary>Sends the original to the Recycle Bin after a successful compression.</summary>
    public bool DeleteSource { get => _deleteSource; set => Set(ref _deleteSource, value); }
}

public sealed class ExtractAction : ConflictAwareAction
{
    private string _destination = "{folder}\\{name}";
    private bool _deleteArchive;
    public string Destination { get => _destination; set => Set(ref _destination, value ?? ""); }
    /// <summary>Sends the archive to the Recycle Bin after a successful extraction.</summary>
    public bool DeleteArchive { get => _deleteArchive; set => Set(ref _deleteArchive, value); }
}

public sealed class CreateShortcutAction : ConflictAwareAction
{
    private string _destination = "{desktop}";
    private string _shortcutName = "{name}";
    public string Destination { get => _destination; set => Set(ref _destination, value ?? ""); }
    public string ShortcutName { get => _shortcutName; set => Set(ref _shortcutName, value ?? ""); }
}

public sealed class OpenWithAction : RuleAction
{
    private string _application = "";
    private string _arguments = "\"{path}\"";
    public string Application { get => _application; set => Set(ref _application, value ?? ""); }
    public string Arguments { get => _arguments; set => Set(ref _arguments, value ?? ""); }
}

public sealed class SortIntoSubfoldersAction : ConflictAwareAction
{
    private string _baseFolder = "{folder}";
    private SortMode _mode = SortMode.ByType;
    private DateField _dateSource = DateField.Modified;
    private string _dateFormat = "yyyy/MM";
    public string BaseFolder { get => _baseFolder; set => Set(ref _baseFolder, value ?? ""); }
    public SortMode Mode { get => _mode; set => Set(ref _mode, value); }
    public DateField DateSource { get => _dateSource; set => Set(ref _dateSource, value); }
    /// <summary>"/" creates nested folders: "yyyy/MM" =&gt; 2026\10.</summary>
    public string DateFormat { get => _dateFormat; set => Set(ref _dateFormat, value ?? ""); }
}
