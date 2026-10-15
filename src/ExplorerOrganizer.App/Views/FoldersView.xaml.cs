using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.App.Views;

/// <summary>Monitored folders of the active profile, with an inline "when" (trigger) selector.</summary>
public partial class FoldersView : UserControl, IRefreshablePage
{
    private bool _loading;

    public FoldersView()
    {
        InitializeComponent();
    }

    private static AppHost Host => AppHost.Current;
    private Profile? Selected => ProfileCombo.SelectedItem as Profile;
    private WatchedFolder? SelectedFolder => FolderList.SelectedItem as WatchedFolder;
    private Window? Owner => Window.GetWindow(this);

    public void Refresh()
    {
        _loading = true;
        ProfileCombo.ItemsSource = null;
        ProfileCombo.ItemsSource = Host.Library.Profiles;
        ProfileCombo.SelectedItem = Host.ActiveProfile;
        PauseSwitch.IsChecked = !Host.Settings.MonitoringPaused;
        ShowFolders();
        _loading = false;
    }

    private void ShowFolders()
    {
        FolderList.ItemsSource = Selected?.Folders;
        if (FolderList.SelectedIndex < 0 && Selected?.Folders.Count > 0) FolderList.SelectedIndex = 0;
        EmptyText.Visibility = Selected == null || Selected.Folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected == null) return;
        Host.ActiveProfile = Selected;
        Host.RaiseStateChanged();
        ShowFolders();
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => Host.SetPaused(PauseSwitch.IsChecked != true);

    private void Persist(object sender, RoutedEventArgs e) => Host.ProfilesChanged();

    private void Trigger_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore the initial binding pass; persist only real user changes (watchers are reconfigured).
        if (_loading || e.RemovedItems.Count == 0 || !((FrameworkElement)sender).IsLoaded) return;
        Host.ProfilesChanged();
        Dispatcher.BeginInvoke(() => FolderList.Items.Refresh()); // updates the summary line
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) { Dialogs.Info(Loc.T("Msg.NoProfile")); return; }
        var editor = new FolderEditorWindow(new WatchedFolder()) { Owner = Owner };
        if (editor.ShowDialog() != true) return;
        Selected.Folders.Add(editor.Result!);
        FolderList.SelectedItem = editor.Result;
        Host.ProfilesChanged();
        ShowFolders();
    }

    private void EditFolder_Click(object sender, RoutedEventArgs e) => EditSelectedFolder();

    private void FolderList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindParent<ComboBox>(d) != null) return;
        EditSelectedFolder();
    }

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        for (var p = d; p != null; p = System.Windows.Media.VisualTreeHelper.GetParent(p))
            if (p is T t) return t;
        return null;
    }

    private void EditSelectedFolder()
    {
        if (Selected == null || SelectedFolder == null) return;
        var folder = SelectedFolder;
        var editor = new FolderEditorWindow(folder) { Owner = Owner };
        if (editor.ShowDialog() != true) return;
        Selected.Folders[Selected.Folders.IndexOf(folder)] = editor.Result!;
        Host.ProfilesChanged();
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null || SelectedFolder == null) return;
        if (!Dialogs.Confirm(string.Format(Loc.T("Confirm.RemoveFolder"), SelectedFolder.Path))) return;
        Selected.Folders.Remove(SelectedFolder);
        Host.ProfilesChanged();
        ShowFolders();
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        await ((App)Application.Current).ScanAllAsync(Selected, SelectedFolder);
    }
}
