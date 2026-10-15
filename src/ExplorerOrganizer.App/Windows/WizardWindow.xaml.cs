using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using ExplorerOrganizer.Core.Services;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Windows;

/// <summary>First-run wizard: language/theme, ready-made presets, automation level.</summary>
public partial class WizardWindow : Window
{
    public sealed class PresetChoice : ObservableObject
    {
        private bool _selected;
        private string _folder = "";
        public PresetKind Kind { get; init; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Selected { get => _selected; set => Set(ref _selected, value); }
        public string Folder { get => _folder; set => Set(ref _folder, value); }
    }

    private int _step = 1;
    private bool _ready;
    private readonly List<PresetChoice> _presets;

    public WizardWindow()
    {
        InitializeComponent();
        var s = AppHost.Current.Settings;
        LanguageBox.SelectedValue = s.Language;
        ThemeBox.SelectedValue = s.Theme;
        _presets = Enum.GetValues<PresetKind>().Select(k => new PresetChoice
        {
            Kind = k,
            Selected = k == PresetKind.Downloads,
            Folder = FolderMonitor.ResolveFolderPath(Presets.DefaultFolder(k)),
        }).ToList();
        RefreshPresetTexts();
        PresetList.ItemsSource = _presets;
        _ready = true;
        ShowStep();
    }

    private void RefreshPresetTexts()
    {
        foreach (var p in _presets)
        {
            p.Title = Loc.T("Preset." + p.Kind);
            p.Description = Loc.T("Wizard.Preset." + p.Kind);
        }
        PresetList.ItemsSource = null;
        PresetList.ItemsSource = _presets;
    }

    private void ShowStep()
    {
        Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = _step is > 1 and < 4;
        SkipButton.Visibility = _step < 4 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = _step switch { 3 => Loc.T("Wizard.Create"), 4 => Loc.T("Wizard.Finish"), _ => Loc.T("Wizard.Next") };
        StepText.Text = string.Format(Loc.T("Wizard.Step"), Math.Min(_step, 4), 4);
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        AppHost.Current.Settings.Language = LanguageBox.SelectedValue as string ?? "";
        Loc.SetLanguage(AppHost.Current.Settings.Language);
        LocalizationBinder.Apply();
        RefreshPresetTexts();
        ShowStep();
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ThemeBox.SelectedValue is not ThemeMode t) return;
        AppHost.Current.Settings.Theme = t;
        ThemeManager.Apply(t);
    }

    private void BrowsePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PresetChoice p }) return;
        var dlg = new OpenFolderDialog { Title = Loc.T("Common.ChooseFolder"), InitialDirectory = p.Folder };
        if (dlg.ShowDialog(this) == true) p.Folder = dlg.FolderName;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 1) { _step--; ShowStep(); }
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => Close();

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 2)
        {
            var missing = _presets.FirstOrDefault(p => p.Selected && !Directory.Exists(p.Folder));
            if (missing != null) { Dialogs.Error(string.Format(Loc.T("Folder.Missing"), missing.Folder)); return; }
        }
        if (_step == 3) Create();
        if (_step == 4) { Close(); return; }
        _step++;
        ShowStep();
    }

    private void Create()
    {
        var host = AppHost.Current;
        var trigger = RealtimeMode.IsChecked == true ? TriggerMode.Realtime : TriggerMode.Manual;
        var created = new List<string>();
        foreach (var choice in _presets.Where(p => p.Selected))
        {
            var profile = Presets.Create(choice.Kind, trigger);
            profile.Folders[0].Path = choice.Folder;
            host.Library.Profiles.Add(profile);
            created.Add(profile.Name);
        }
        if (host.Library.Profiles.Count > 0 && host.Library.Profiles.All(p => p.Id != host.Library.ActiveProfileId))
            host.Library.ActiveProfileId = host.Library.Profiles[0].Id;

        host.Settings.SimulationMode = trigger == TriggerMode.Realtime && SimulateFirst.IsChecked == true;
        host.Settings.StartWithWindows = AutostartBox.IsChecked == true;
        host.Settings.ShowDropTarget = DropTargetBox.IsChecked == true;
        try
        {
            ShellIntegration.SetAutostart(host.Settings.StartWithWindows);
            if (ContextMenuBox.IsChecked == true) ShellIntegration.RegisterContextMenu();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Dialogs.Error(ex.Message);
        }
        host.SaveSettings();
        host.SaveProfiles();

        DoneText.Text = created.Count == 0
            ? Loc.T("Wizard.DoneNone")
            : string.Format(Loc.T("Wizard.DoneProfiles"), string.Join(", ", created))
              + (host.Settings.SimulationMode ? "\n" + Loc.T("Wizard.DoneSimulation") : "");
    }
}
