using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace ExplorerOrganizer.Core.Model;

/// <summary>Base class of every rule condition.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConditionGroup), "group")]
[JsonDerivedType(typeof(AllFilesCondition), "allFiles")]
[JsonDerivedType(typeof(NameCondition), "name")]
[JsonDerivedType(typeof(ExtensionCondition), "extension")]
[JsonDerivedType(typeof(SizeCondition), "size")]
[JsonDerivedType(typeof(DateCondition), "date")]
[JsonDerivedType(typeof(FileTypeCondition), "fileType")]
[JsonDerivedType(typeof(AttributeCondition), "attribute")]
[JsonDerivedType(typeof(MetadataCondition), "metadata")]
[JsonDerivedType(typeof(ContentCondition), "content")]
[JsonDerivedType(typeof(NameContainsCondition), "nameContains")]
[JsonDerivedType(typeof(ItemTypeCondition), "itemType")]
[JsonDerivedType(typeof(FolderContentCondition), "folderContent")]
public abstract class Condition : ObservableObject { }

/// <summary>AND ("All") / OR ("Any") of child conditions. Groups can be nested.</summary>
public sealed class ConditionGroup : Condition
{
    private GroupOperator _operator = GroupOperator.All;
    public GroupOperator Operator { get => _operator; set => Set(ref _operator, value); }
    public ObservableCollection<Condition> Children { get; set; } = new();
}

public sealed class AllFilesCondition : Condition { }

public abstract class TextCondition : Condition
{
    private TextOperator _operator = TextOperator.Contains;
    private string _value = "";
    private bool _caseSensitive;
    public TextOperator Operator { get => _operator; set => Set(ref _operator, value); }
    public string Value { get => _value; set => Set(ref _value, value ?? ""); }
    public bool CaseSensitive { get => _caseSensitive; set => Set(ref _caseSensitive, value); }
}

public sealed class NameCondition : TextCondition
{
    private NameTarget _target = NameTarget.NameWithExtension;
    public NameTarget Target { get => _target; set => Set(ref _target, value); }
}

public sealed class ExtensionCondition : Condition
{
    private string _extensions = "";
    /// <summary>Semicolon/comma separated list, with or without dots: "jpg;png;.gif".</summary>
    public string Extensions { get => _extensions; set => Set(ref _extensions, value ?? ""); }
}

public sealed class SizeCondition : Condition
{
    private SizeComparison _comparison = SizeComparison.GreaterThan;
    private double _value = 10;
    private double _value2 = 100;
    private SizeUnit _unit = SizeUnit.MB;
    public SizeComparison Comparison { get => _comparison; set => Set(ref _comparison, value); }
    public double Value { get => _value; set => Set(ref _value, value); }
    public double Value2 { get => _value2; set => Set(ref _value2, value); }
    public SizeUnit Unit { get => _unit; set => Set(ref _unit, value); }
}

public sealed class DateCondition : Condition
{
    private DateField _field = DateField.Modified;
    private DateMode _mode = DateMode.OlderThan;
    private int _amount = 30;
    private TimeUnit _unit = TimeUnit.Days;
    private DateTime _date = DateTime.Today;
    public DateField Field { get => _field; set => Set(ref _field, value); }
    public DateMode Mode { get => _mode; set => Set(ref _mode, value); }
    public int Amount { get => _amount; set => Set(ref _amount, value); }
    public TimeUnit Unit { get => _unit; set => Set(ref _unit, value); }
    public DateTime Date { get => _date; set => Set(ref _date, value); }
}

public sealed class FileTypeCondition : Condition
{
    private FileCategory _category = FileCategory.Image;
    public FileCategory Category { get => _category; set => Set(ref _category, value); }
}

public sealed class AttributeCondition : Condition
{
    private FileAttributeKind _attribute = FileAttributeKind.ReadOnly;
    public FileAttributeKind Attribute { get => _attribute; set => Set(ref _attribute, value); }
}

/// <summary>Matches a Windows property (EXIF, ID3, document properties...). Property: "exif:CameraModel", "id3:Artist", "doc:Author" or "System.*".</summary>
public sealed class MetadataCondition : TextCondition
{
    private string _property = "id3:Artist";
    public string Property { get => _property; set => Set(ref _property, value ?? ""); }
}

public sealed class ContentCondition : TextCondition { }

/// <summary>The name (without path, extension included) contains at least one of the words separated by ";". Case-insensitive.</summary>
public sealed class NameContainsCondition : Condition
{
    private string _words = "";
    public string Words { get => _words; set => Set(ref _words, value ?? ""); }
}

/// <summary>The item is a file or a folder. A rule that asks for folders is the only kind that receives folders.</summary>
public sealed class ItemTypeCondition : Condition
{
    private ItemKind _kind = ItemKind.Folder;
    public ItemKind Kind { get => _kind; set => Set(ref _kind, value); }
}

/// <summary>Folder whose files (scanned recursively) have one of the extensions: e.g. "ttf;otf" recognizes a font folder.</summary>
public sealed class FolderContentCondition : Condition
{
    private string _extensions = "";
    private FolderContentMatch _match = FolderContentMatch.Most;
    public string Extensions { get => _extensions; set => Set(ref _extensions, value ?? ""); }
    public FolderContentMatch Match { get => _match; set => Set(ref _match, value); }
}
