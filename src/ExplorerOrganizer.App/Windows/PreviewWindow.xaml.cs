using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.App.Windows;

/// <summary>Simulation of one profile and the items it would process.</summary>
public sealed record PreviewData(RunResult Result, IReadOnlyList<FileInput> Inputs);

/// <summary>
/// Simulation result: the user picks the profile in the sidebar (the preview is recomputed for it) and which files
/// really get processed. "Apply" works on the selected profile only.
/// </summary>
public partial class PreviewWindow : Window
{
    private sealed record ProfileItem(Profile Profile, string Name, string Detail);

    private readonly Func<Profile, Task<PreviewData>> _load;
    private List<OperationRecord> _records = new();
    private int _loadVersion;
    private bool _loading;

    public PreviewWindow(IEnumerable<Profile> profiles, Profile initial, Func<Profile, Task<PreviewData>> load)
    {
        InitializeComponent();
        _load = load;
        var items = profiles.Select(p => new ProfileItem(p, p.Name, Detail(p))).ToList();
        if (!items.Any(i => i.Profile == initial)) items.Insert(0, new ProfileItem(initial, initial.Name, Detail(initial)));
        ProfileList.ItemsSource = items;
        ProfileList.SelectedItem = items.First(i => i.Profile == initial); // starts the first simulation
    }

    /// <summary>The profile the user chose in the sidebar.</summary>
    public Profile Profile { get; private set; } = null!;

    /// <summary>Items of the last simulation (to be filtered with <see cref="SelectedFiles"/>).</summary>
    public IReadOnlyList<FileInput> Inputs { get; private set; } = Array.Empty<FileInput>();

    /// <summary>Files and folders (original paths) with at least one selected operation.</summary>
    public IReadOnlyList<string> SelectedFiles =>
        _records.Where(r => r.Selected && r.Status == OperationStatus.Planned).Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string Detail(Profile p)
    {
        var text = string.Format(Loc.T("Preview.ProfileDetail"), p.Rules.Count(r => r.Enabled), p.Folders.Count(f => f.Enabled));
        return p.Enabled ? text : text + " · " + Loc.T("Describe.Disabled");
    }

    private async void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileItem item) return;
        Profile = item.Profile;
        Title = Loc.T("Preview.Title") + " – " + item.Name;
        int version = ++_loadVersion;
        SetLoading(true);
        PreviewData data;
        try { data = await _load(item.Profile); }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            SetLoading(false);
            _records = new List<OperationRecord>();
            RecordsGrid.ItemsSource = _records;
            Summary.Text = ex.Message;
            UpdateApply();
            return;
        }
        if (version != _loadVersion) return; // the user already picked another profile
        Inputs = data.Inputs;
        _records = data.Result.Records;
        foreach (var r in _records) r.Selected = r.Status == OperationStatus.Planned;
        RecordsGrid.ItemsSource = _records;
        int files = _records.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Summary.Text = string.Format(Loc.T("Preview.Summary"), item.Name, data.Result.Scanned, files, _records.Count(r => r.Status == OperationStatus.Failed));
        SetLoading(false);
    }

    private void SetLoading(bool loading)
    {
        _loading = loading;
        Loading.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        RecordsGrid.Visibility = loading ? Visibility.Hidden : Visibility.Visible;
        Empty.Visibility = !loading && _records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (loading) Summary.Text = "";
        UpdateApply();
    }

    private void UpdateApply()
    {
        int n = _loading ? 0 : SelectedFiles.Count;
        ApplyButton.Content = string.Format(Loc.T("Preview.Apply"), n);
        ApplyButton.IsEnabled = n > 0;
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        // operations of the same file are applied together: keep their checkboxes in sync
        if (sender is FrameworkElement { DataContext: OperationRecord rec })
            foreach (var r in _records.Where(x => x.FilePath == rec.FilePath)) r.Selected = rec.Selected;
        UpdateApply();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _records) r.Selected = r.Status == OperationStatus.Planned;
        UpdateApply();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _records) r.Selected = false;
        UpdateApply();
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
