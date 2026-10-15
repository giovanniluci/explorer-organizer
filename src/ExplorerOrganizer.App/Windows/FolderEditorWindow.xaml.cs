using System.Globalization;
using System.Windows;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Windows;

public partial class FolderEditorWindow : Window
{
    private readonly WatchedFolder _working;

    public FolderEditorWindow(WatchedFolder folder)
    {
        InitializeComponent();
        _working = Json.Clone(folder);
        DataContext = _working;
        _working.PropertyChanged += (_, _) => UpdateResolved();
        UpdateResolved();
        Loaded += (_, _) => PathBox.Focus();
    }

    public WatchedFolder? Result { get; private set; }

    private void UpdateResolved()
    {
        var resolved = FolderMonitor.ResolveFolderPath(_working.Path);
        Resolved.Text = string.IsNullOrWhiteSpace(_working.Path) ? "" :
            Directory.Exists(resolved) ? "→ " + resolved : "⚠ " + string.Format(Loc.T("Folder.Missing"), resolved);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("Common.ChooseFolder") };
        if (dlg.ShowDialog(this) == true) _working.Path = dlg.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var resolved = FolderMonitor.ResolveFolderPath(_working.Path);
        if (string.IsNullOrWhiteSpace(_working.Path) || !Directory.Exists(resolved))
        {
            Dialogs.Error(string.Format(Loc.T("Folder.Missing"), resolved));
            return;
        }
        if (_working.Trigger == TriggerMode.Daily &&
            !TimeSpan.TryParseExact(_working.DailyTime, @"hh\:mm", CultureInfo.InvariantCulture, out _))
        {
            Dialogs.Error(Loc.T("Folder.BadTime"));
            return;
        }
        var root = Path.GetPathRoot(resolved);
        if (string.Equals(Path.TrimEndingDirectorySeparator(resolved), Path.TrimEndingDirectorySeparator(root ?? ""), StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
        {
            if (!Dialogs.Confirm(Loc.T("Folder.ConfirmRisky"), danger: true)) return;
        }
        Result = _working;
        DialogResult = true;
    }
}
