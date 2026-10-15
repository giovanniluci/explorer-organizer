using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Views;

/// <summary>Toolbox: profile management, ready-made presets, JSON import/export and the floating drop target.</summary>
public partial class ToolboxView : UserControl, IRefreshablePage
{
    public sealed record PresetItem(PresetKind Kind, string Title, string Description)
    {
        public string AccessibleName => Loc.T("Toolbox.Add") + " " + Title;
    }

    private bool _loading;

    public ToolboxView()
    {
        InitializeComponent();
        PresetList.ItemsSource = Enum.GetValues<PresetKind>()
            .Select(k => new PresetItem(k, Loc.T("Preset." + k), Loc.T("Wizard.Preset." + k))).ToList();
    }

    private static AppHost Host => AppHost.Current;
    private Profile? Selected => ProfileCombo.SelectedItem as Profile;
    private Window? Owner => Window.GetWindow(this);

    public void Refresh()
    {
        _loading = true;
        ProfileCombo.ItemsSource = null;
        ProfileCombo.ItemsSource = Host.Library.Profiles;
        ProfileCombo.SelectedItem = Host.ActiveProfile;
        NameBox.Text = Selected?.Name ?? "";
        EnabledSwitch.IsChecked = Selected?.Enabled ?? false;
        DropTargetSwitch.IsChecked = Host.Settings.ShowDropTarget;
        _loading = false;
    }

    private void Select(Profile p)
    {
        Host.ActiveProfile = p;
        Host.ProfilesChanged();
        Refresh();
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected == null) return;
        Select(Selected);
    }

    private void NameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Selected == null || string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == Selected.Name) return;
        Selected.Name = NameBox.Text.Trim();
        Select(Selected);
    }

    private void Enabled_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        Selected.Enabled = EnabledSwitch.IsChecked == true;
        Host.ProfilesChanged();
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = new Profile { Name = Loc.T("Profile.NewName") };
        Host.Library.Profiles.Add(p);
        Select(p);
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var copy = Json.Clone(Selected);
        copy.Id = Guid.NewGuid();
        copy.Name = Selected.Name + " " + Loc.T("Common.CopySuffix");
        foreach (var r in copy.Rules) r.Id = Guid.NewGuid();
        foreach (var f in copy.Folders) { f.Id = Guid.NewGuid(); f.Enabled = false; } // avoid two profiles watching the same folder
        Host.Library.Profiles.Add(copy);
        Select(copy);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        if (!Dialogs.Confirm(string.Format(Loc.T("Confirm.DeleteProfile"), Selected.Name), danger: true)) return;
        Host.Library.Profiles.Remove(Selected);
        if (Host.Library.Profiles.Count == 0) Host.Library.Profiles.Add(new Profile { Name = Loc.T("Profile.DefaultName") });
        Select(Host.Library.Profiles[0]);
    }

    private void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PresetKind kind) return;
        var p = Presets.Create(kind);
        Host.Library.Profiles.Add(p);
        Select(p);
        Dialogs.Info(string.Format(Loc.T("Toolbox.PresetAdded"), p.Name));
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = Loc.T("Filter.Json"), Title = Loc.T("Profiles.Import") };
        if (dlg.ShowDialog(Owner) != true) return;
        try
        {
            var p = JsonStore.ImportProfile(dlg.FileName);
            foreach (var f in p.Folders) f.Enabled = false; // imported folders start disabled: review them first
            Host.Library.Profiles.Add(p);
            Select(p);
            Dialogs.Info(string.Format(Loc.T("Msg.Imported"), p.Name, p.Rules.Count));
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            Dialogs.Error(Loc.T("Msg.ImportFailed") + "\n\n" + ex.Message);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = Loc.T("Filter.Json"),
            FileName = TokenRenderer.SanitizeSegment(Selected.Name) + ".eoprofile.json",
            Title = Loc.T("Profiles.Export"),
        };
        if (dlg.ShowDialog(Owner) != true) return;
        try
        {
            JsonStore.ExportProfile(Selected, dlg.FileName);
            Dialogs.Info(string.Format(Loc.T("Msg.Exported"), dlg.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Error(ex.Message); }
    }

    private void DropTarget_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).SetDropTargetVisible(DropTargetSwitch.IsChecked == true);
}
