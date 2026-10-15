using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.App.Views;

/// <summary>Settings apply immediately (no Save button).</summary>
public partial class SettingsView : UserControl
{
    private bool _ready;
    private string _language;
    private ThemeMode _theme;

    public SettingsView()
    {
        InitializeComponent();
        var s = AppHost.Current.Settings;
        s.StartWithWindows = ShellIntegration.IsAutostartEnabled(); // reflect reality (installer task / manual changes)
        _language = s.Language;
        _theme = s.Theme;
        DataContext = s;
        DataPath.Text = AppHost.Current.Paths.DataDirectory;
        VersionText.Text = "Explorer Organizer " + typeof(SettingsView).Assembly.GetName().Version?.ToString(3);
        UpdateContextMenuStatus();

        AddHandler(ToggleButton.ClickEvent, new RoutedEventHandler((_, _) => Defer()));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => Defer()));
        AddHandler(LostFocusEvent, new RoutedEventHandler((_, e) => { if (e.OriginalSource is TextBox) Defer(); }));
        Loaded += (_, _) => _ready = true;
    }

    /// <summary>Runs after the bindings have pushed the new value into the settings object.</summary>
    private void Defer() { if (_ready) Dispatcher.BeginInvoke(Apply); }

    private void Apply()
    {
        var host = AppHost.Current;
        var s = host.Settings;
        var app = (App)Application.Current;
        s.StabilitySeconds = Math.Clamp(s.StabilitySeconds, 1, 600);
        try { ShellIntegration.SetAutostart(s.StartWithWindows); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { Dialogs.Error(ex.Message); }
        if (host.Monitor.Paused != s.MonitoringPaused) host.Monitor.Paused = s.MonitoringPaused;
        app.SetDropTargetVisible(s.ShowDropTarget);
        host.SaveSettings();
        host.RaiseStateChanged();

        if (s.Language != _language || s.Theme != _theme)
        {
            _language = s.Language;
            _theme = s.Theme;
            _ready = false;
            app.ApplyAppearance(); // rebuilds the pages, this view included
        }
    }

    private void UpdateContextMenuStatus() =>
        ContextMenuStatus.Text = ShellIntegration.IsContextMenuRegistered() ? Loc.T("Settings.ContextMenuOn") : Loc.T("Settings.ContextMenuOff");

    private void AddContextMenu_Click(object sender, RoutedEventArgs e)
    {
        try { ShellIntegration.RegisterContextMenu(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { Dialogs.Error(ex.Message); }
        UpdateContextMenuStatus();
    }

    private void RemoveContextMenu_Click(object sender, RoutedEventArgs e)
    {
        try { ShellIntegration.UnregisterContextMenu(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { Dialogs.Error(ex.Message); }
        UpdateContextMenuStatus();
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppHost.Current.Paths.DataDirectory}\"") { UseShellExecute = true });

    private void Wizard_Click(object sender, RoutedEventArgs e)
    {
        new WizardWindow { Owner = Window.GetWindow(this) }.ShowDialog();
        AppHost.Current.ProfilesChanged();
        ((App)Application.Current).ApplyAppearance();
    }
}
