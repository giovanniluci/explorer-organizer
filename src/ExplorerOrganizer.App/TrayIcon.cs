using ExplorerOrganizer.Core.Localization;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace ExplorerOrganizer.App;

/// <summary>Notification-area icon (WinForms NotifyIcon, the lightest option available to WPF).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly App _app;

    public TrayIcon(App app)
    {
        _app = app;
        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Explorer Organizer",
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _app.ShowMainWindow();
        _icon.BalloonTipClicked += (_, _) => _app.ShowMainWindow("activity");
        BuildMenu();
    }

    public void BuildMenu()
    {
        var host = AppHost.Current;
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Loc.T("Tray.Open"), null, (_, _) => _app.ShowMainWindow());
        menu.Items.Add(Loc.T("Tray.ScanAll"), null, async (_, _) => await _app.ScanAllAsync());
        var pause = new WinForms.ToolStripMenuItem(host.Settings.MonitoringPaused ? Loc.T("Tray.Resume") : Loc.T("Tray.Pause"));
        pause.Click += (_, _) => { host.SetPaused(!host.Settings.MonitoringPaused); BuildMenu(); };
        menu.Items.Add(pause);
        var drop = new WinForms.ToolStripMenuItem(Loc.T("Tray.DropTarget")) { Checked = host.Settings.ShowDropTarget };
        drop.Click += (_, _) => _app.SetDropTargetVisible(!host.Settings.ShowDropTarget);
        menu.Items.Add(drop);
        var undo = new WinForms.ToolStripMenuItem(Loc.T("Tray.Undo")) { Enabled = host.Journal.Last != null };
        undo.Click += (_, _) => _app.UndoLastWithConfirmation();
        menu.Items.Add(undo);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(Loc.T("Tray.Exit"), null, (_, _) => _app.ExitApplication());
        menu.Opening += (_, _) => undo.Enabled = host.Journal.Last != null;
        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = menu;
        old?.Dispose();
    }

    /// <summary>Current context menu (inspected by the UI tests).</summary>
    internal WinForms.ContextMenuStrip? Menu => _icon.ContextMenuStrip;

    public void Notify(string title, string text, bool warning)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            _icon.ShowBalloonTip(4000, title, text, warning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info));
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path != null) return Drawing.Icon.ExtractAssociatedIcon(path) ?? Drawing.SystemIcons.Application;
        }
        catch { }
        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
