using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerOrganizer.Core.Localization;

namespace ExplorerOrganizer.App.Windows;

/// <summary>Small always-on-top floating target (DropIt style). Drag to move, right-click for the profile menu.</summary>
public partial class DropTargetWindow : Window
{
    public DropTargetWindow()
    {
        InitializeComponent();
        var s = AppHost.Current.Settings;
        if (double.IsNaN(s.DropTargetLeft) || double.IsNaN(s.DropTargetTop) || !IsOnScreen(s.DropTargetLeft, s.DropTargetTop))
        {
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Bottom - Height - 24;
        }
        else
        {
            Left = s.DropTargetLeft;
            Top = s.DropTargetTop;
        }
        RefreshText();
        AppHost.Current.StateChanged += (_, _) => Dispatcher.BeginInvoke(RefreshText);
    }

    private static App App => (App)Application.Current;

    public void RefreshText()
    {
        ProfileName.Text = AppHost.Current.ActiveProfile?.Name ?? "";
        Body.ToolTip = Loc.T("Drop.Tooltip");
    }

    private static bool IsOnScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft && top >= SystemParameters.VirtualScreenTop &&
        left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 40 &&
        top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1) return;
        DragMove();
        var s = AppHost.Current.Settings;
        s.DropTargetLeft = Left;
        s.DropTargetTop = Top;
        AppHost.Current.SaveSettings();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e) => App.ShowMainWindow();

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        Body.Background = (System.Windows.Media.Brush)FindResource("AccentSoft");
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => Body.SetResourceReference(Border.BackgroundProperty, "Surface");

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        Body.SetResourceReference(Border.BackgroundProperty, "Surface");
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await App.ProcessPathsAsync(paths, "drop");
    }

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
        Menu.Items.Clear();
        var host = AppHost.Current;
        foreach (var p in host.Library.Profiles)
        {
            var item = new MenuItem { Header = p.Name, IsCheckable = true, IsChecked = p == host.ActiveProfile };
            item.Click += (_, _) => { host.ActiveProfile = p; host.RaiseStateChanged(); };
            Menu.Items.Add(item);
        }
        Menu.Items.Add(new Separator());
        var open = new MenuItem { Header = Loc.T("Tray.Open") };
        open.Click += (_, _) => App.ShowMainWindow();
        Menu.Items.Add(open);
        var hide = new MenuItem { Header = Loc.T("Drop.Hide") };
        hide.Click += (_, _) => App.SetDropTargetVisible(false);
        Menu.Items.Add(hide);
    }
}
