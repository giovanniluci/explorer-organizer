using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Services;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Views;

public partial class ActivityView : UserControl, IRefreshablePage
{
    private readonly ObservableCollection<OperationRecord> _items = new();
    private readonly ICollectionView _view;

    public ActivityView()
    {
        InitializeComponent();
        foreach (var r in AppHost.Current.Log.Snapshot().AsEnumerable().Reverse()) _items.Add(r);
        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = Matches;
        LogList.ItemsSource = _view;
        _items.CollectionChanged += (_, _) => UpdateEmpty();
        UpdateEmpty();
        Loaded += (_, _) => AppHost.Current.Log.Added += OnLogAdded;
        Unloaded += (_, _) => AppHost.Current.Log.Added -= OnLogAdded;
    }

    public void Refresh() => UpdateEmpty();

    private void UpdateEmpty() => EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnLogAdded(object? sender, IReadOnlyList<OperationRecord> records) => Dispatcher.BeginInvoke(() =>
    {
        foreach (var r in records) _items.Insert(0, r);
        while (_items.Count > ActivityLog.MemoryLimit) _items.RemoveAt(_items.Count - 1);
    });

    private bool Matches(object o)
    {
        var f = FilterBox.Text;
        if (string.IsNullOrWhiteSpace(f) || o is not OperationRecord r) return true;
        return r.Source.Contains(f, StringComparison.OrdinalIgnoreCase) || r.Target.Contains(f, StringComparison.OrdinalIgnoreCase)
            || r.RuleName.Contains(f, StringComparison.OrdinalIgnoreCase) || r.Message.Contains(f, StringComparison.OrdinalIgnoreCase)
            || Loc.T("Enum.OperationStatus." + r.Status).Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => _view.Refresh();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = Loc.T("Filter.Log"), FileName = $"explorer-organizer-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            ActivityLog.Export(_view.Cast<OperationRecord>().Reverse(), dlg.FileName);
            Dialogs.Info(string.Format(Loc.T("Msg.Exported"), dlg.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Error(ex.Message); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppHost.Current.Paths.LogDirectory}\"") { UseShellExecute = true });

    private void Clear_Click(object sender, RoutedEventArgs e) => _items.Clear();
}
