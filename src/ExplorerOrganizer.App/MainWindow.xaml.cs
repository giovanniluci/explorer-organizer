using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Views;

namespace ExplorerOrganizer.App;

public partial class MainWindow : Window
{
    /// <summary>Page keys in rail order (Ctrl+1 … Ctrl+8).</summary>
    public static readonly string[] PageKeys = { "home", "rules", "folders", "history", "toolbox", "restore", "settings", "feedback" };

    private readonly Dictionary<string, UserControl> _pages = new();
    private bool _switchingNav;

    public MainWindow()
    {
        InitializeComponent();
        var host = AppHost.Current;
        host.StateChanged += OnStateChanged;
        host.BusyChanged += OnBusyChanged;
        ThemeManager.Applied += OnThemeApplied;
        SourceInitialized += (_, _) => Backdrop.Apply(this, Root);
        for (int i = 0; i < PageKeys.Length; i++)
        {
            var key = PageKeys[i];
            InputBindings.Add(new KeyBinding(new RelayCommand(_ => Navigate(key)), Key.D1 + i, ModifierKeys.Control));
        }
        NavTop.SelectedIndex = 0;
    }

    /// <summary>Key of the page currently shown.</summary>
    public string CurrentPage { get; private set; } = "home";

    private void OnStateChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshState);

    private void OnThemeApplied(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => Backdrop.Apply(this, Root));

    private void OnBusyChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
        BusyOverlay.Visibility = AppHost.Current.IsBusy && IsActive ? Visibility.Visible : Visibility.Collapsed);

    protected override void OnClosed(EventArgs e)
    {
        AppHost.Current.StateChanged -= OnStateChanged;
        AppHost.Current.BusyChanged -= OnBusyChanged;
        ThemeManager.Applied -= OnThemeApplied;
        Page.Content = null; // unloads the current page so it detaches its own handlers
        _pages.Clear();
        base.OnClosed(e);
    }

    /// <summary>Accepts the current keys and the names used by older versions (dashboard, profiles, activity).</summary>
    public void Navigate(string page)
    {
        page = page switch { "dashboard" => "home", "profiles" => "rules", "activity" => "history", _ => page };
        foreach (var list in new[] { NavTop, NavBottom })
            foreach (ListBoxItem item in list.Items)
                if ((string)item.Tag == page) { list.SelectedItem = item; return; }
    }

    /// <summary>Pages read localized lists at creation: rebuild them after a language change.</summary>
    public void RebuildPages()
    {
        _pages.Clear();
        Page.Content = null;
        Show(CurrentPage);
        RefreshState();
    }

    public void RefreshState()
    {
        if (Page.Content is IRefreshablePage p) p.Refresh();
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switchingNav || sender is not ListBox list || list.SelectedItem is not ListBoxItem item) return;
        _switchingNav = true;
        (list == NavTop ? NavBottom : NavTop).SelectedIndex = -1; // one selection across both groups
        _switchingNav = false;
        Show((string)item.Tag);
    }

    private void Show(string page)
    {
        if (!_pages.TryGetValue(page, out var view))
        {
            view = page switch
            {
                "rules" => new ProfilesView(),
                "folders" => new FoldersView(),
                "history" => new ActivityView(),
                "toolbox" => new ToolboxView(),
                "restore" => new RestoreView(),
                "settings" => new SettingsView(),
                "feedback" => new FeedbackView(),
                _ => new DashboardView(),
            };
            _pages[page] = view;
        }
        CurrentPage = page;
        Page.Content = view;
        if (view is IRefreshablePage p) p.Refresh();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await ((App)Application.Current).ProcessPathsAsync(paths, "drop", owner: this);
    }
}

/// <summary>Pages that show live state (statistics, journal, profiles) and refresh when shown or when it changes.</summary>
public interface IRefreshablePage
{
    void Refresh();
}
