using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>true =&gt; Visible. Parameter "invert" flips it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value switch { bool v => v, int n => n != 0, string s => s.Length > 0, null => false, _ => true };
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

/// <summary>Localizes an enum value through the "Enum.&lt;Type&gt;.&lt;Value&gt;" keys.</summary>
public sealed class EnumToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value == null ? "" : Loc.T("Enum." + value.GetType().Name + "." + value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Status =&gt; theme brush key.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is OperationStatus s ? s switch
        {
            OperationStatus.Done => "Success",
            OperationStatus.Planned => "Accent",
            OperationStatus.Failed => "Danger",
            OperationStatus.Skipped => "Warning",
            _ => "TextMuted",
        } : "TextMuted";
        return Application.Current.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Rule =&gt; one-line "IF … THEN …" summary.</summary>
public sealed class RuleSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Core.Model.Rule r ? RuleDescriber.Describe(r) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>WatchedFolder =&gt; "Real time · subfolders" style summary.</summary>
public sealed class FolderSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Core.Model.WatchedFolder f ? RuleDescriber.Describe(f) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
