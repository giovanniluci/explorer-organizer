using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Services;

namespace ExplorerOrganizer.App.Views;

/// <summary>Home: suggestion banner, "Organize" card with two gauges, and a 2x2 grid of shortcut cards.</summary>
public partial class DashboardView : UserControl, IRefreshablePage
{
    /// <summary>Above this many files the "waiting" gauge is full and the count shows "N+".</summary>
    public const int PendingCap = 9999;
    private const int PendingFullScale = 100;

    private Action? _bannerAction;
    private int _pendingVersion;
    private int? _pending;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) => AppHost.Current.Log.Added += OnLogAdded;
        Unloaded += (_, _) => AppHost.Current.Log.Added -= OnLogAdded;
    }

    private void OnLogAdded(object? sender, IReadOnlyList<OperationRecord> e) => Dispatcher.BeginInvoke(Refresh);

    private static App App => (App)Application.Current;
    private static AppHost Host => AppHost.Current;

    private void Navigate(string page) => (Window.GetWindow(this) as MainWindow)?.Navigate(page);

    public void Refresh()
    {
        var host = Host;
        var profile = host.ActiveProfile;
        int rulesTotal = profile?.Rules.Count ?? 0;
        int rulesOn = profile?.Rules.Count(r => r.Enabled) ?? 0;
        RulesValue.Text = rulesOn.ToString(Loc.Culture);
        RulesBar.Value = rulesTotal == 0 ? 0 : (double)rulesOn / rulesTotal;

        RulesCardLabel.Text = string.Format(Loc.T("Home.CardRulesLabel"), profile?.Name ?? "--");
        RulesCardValue.Text = string.Format(Loc.T("Home.CardRulesValue"), rulesOn);

        var folders = host.Library.Profiles.Where(p => p.Enabled).SelectMany(p => p.Folders.Where(f => f.Enabled)).ToList();
        int realtime = folders.Count(f => f.Trigger == Core.Model.TriggerMode.Realtime);
        MonitorCardLabel.Text = host.Settings.MonitoringPaused
            ? Loc.T("Home.CardMonitorPaused")
            : string.Format(Loc.T("Home.CardMonitorLive"), realtime);
        MonitorCardValue.Text = string.Format(Loc.T("Home.CardMonitorValue"), folders.Count);

        PreviewCardLabel.Text = host.Settings.SimulationMode ? Loc.T("Home.CardPreviewOn") : Loc.T("Home.CardPreviewOff");

        var last = host.Journal.Last;
        HistoryCardValue.Text = last == null ? Loc.T("Home.CardHistoryNone") : last.Time.ToString("g", Loc.Culture);

        var s = host.Stats;
        long today = s.TodayDate == DateTime.Now.ToString("yyyy-MM-dd") ? s.Today : 0;
        FooterText.Text = string.Format(Loc.T("Home.Footer"), today.ToString("N0", Loc.Culture), s.FilesOrganized.ToString("N0", Loc.Culture));

        UpdateBanner();
        CountPendingAsync(folders);
    }

    /// <summary>Counts the files currently in the enabled monitored folders, off the UI thread.</summary>
    private async void CountPendingAsync(List<Core.Model.WatchedFolder> folders)
    {
        int version = ++_pendingVersion;
        var ignore = Host.Settings.IgnorePatterns;
        int count = await Task.Run(() =>
        {
            int n = 0;
            foreach (var f in folders.DistinctBy(f => FolderMonitor.ResolveFolderPath(f.Path), StringComparer.OrdinalIgnoreCase))
            {
                try { n += FolderMonitor.Scan(f, ignore).Count; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                if (n > PendingCap) break;
            }
            return Math.Min(n, PendingCap + 1);
        });
        if (version != _pendingVersion) return; // a newer refresh is running
        _pending = count;
        PendingValue.Text = count > PendingCap ? PendingCap.ToString("N0", Loc.Culture) + "+" : count.ToString("N0", Loc.Culture);
        PendingBar.Value = Math.Min(1.0, (double)count / PendingFullScale);
        UpdateBanner();
    }

    private void UpdateBanner()
    {
        var host = Host;
        bool anyFolder = host.Library.Profiles.Any(p => p.Enabled && p.Folders.Any(f => f.Enabled));
        if (host.Settings.MonitoringPaused)
            SetBanner(Loc.T("Home.BannerPaused"), Loc.T("Home.BannerPausedHint"), Loc.T("Home.BannerResume"), () => host.SetPaused(false));
        else if (_pending > 0)
            SetBanner(string.Format(Loc.T("Home.BannerPending"), _pending > PendingCap ? PendingCap + "+" : _pending.Value.ToString("N0", Loc.Culture)),
                Loc.T("Home.BannerPendingHint"), Loc.T("Home.OrganizeNow"), () => _ = OrganizeAsync(), Loc.T("Home.BannerPendingTail"));
        else if (host.Settings.SimulationMode)
            SetBanner(Loc.T("Home.BannerSimulation"), Loc.T("Home.BannerSimulationHint"), Loc.T("Nav.Settings"), () => Navigate("settings"));
        else if (!anyFolder)
            SetBanner(Loc.T("Home.BannerNoFolders"), Loc.T("Home.BannerNoFoldersHint"), Loc.T("Nav.Folders"), () => Navigate("folders"));
        else
            SetBanner(Loc.T("Home.BannerTidy"), Loc.T("Home.BannerTidyHint"), null, null);
    }

    private void SetBanner(string title, string hint, string? link, Action? action, string tail = "")
    {
        BannerTail.Text = tail;
        BannerTitle.Text = title;
        BannerHint.Text = link == null ? hint : hint + " ";
        BannerLinkText.Text = link ?? "";
        BannerLink.IsEnabled = action != null;
        _bannerAction = action;
    }

    private void BannerAction_Click(object sender, RoutedEventArgs e) => _bannerAction?.Invoke();

    private async Task OrganizeAsync()
    {
        await App.ScanAllAsync();
        Refresh();
    }

    private async void Organize_Click(object sender, RoutedEventArgs e) => await OrganizeAsync();

    private void SetAuto_Click(object sender, RoutedEventArgs e) => Navigate("folders");

    private void RulesCard_Click(object sender, RoutedEventArgs e) => Navigate("rules");

    private void FoldersCard_Click(object sender, RoutedEventArgs e) => Navigate("folders");

    private async void PreviewCard_Click(object sender, RoutedEventArgs e)
    {
        await App.ScanAllAsync(forcePreview: true);
        Refresh();
    }

    private void HistoryCard_Click(object sender, RoutedEventArgs e) => Navigate("restore");
}
