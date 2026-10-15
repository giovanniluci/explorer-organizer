using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.Core.Localization;

namespace ExplorerOrganizer.App.Views;

/// <summary>Restore: the undo journal (newest first) and "undo last operation".</summary>
public partial class RestoreView : UserControl, IRefreshablePage
{
    public sealed record BatchItem(string Description, string TimeText, string Details);

    public RestoreView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        var journal = AppHost.Current.Journal;
        var batches = journal.Snapshot();
        BatchList.ItemsSource = batches.Select(b => new BatchItem(b.Description, b.Time.ToString("g", Loc.Culture),
            string.Format(Loc.T("Restore.Files"), b.FileCount, b.Steps.Count)
            + (b.HasIrreversibleSteps ? " · " + Loc.T("Restore.Irreversible") : ""))).ToList();
        EmptyText.Visibility = batches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UndoButton.IsEnabled = batches.Count > 0;
        var last = journal.Last;
        LastText.Text = last == null ? Loc.T("Dash.NothingToUndo")
            : string.Format(Loc.T("Dash.LastOperation"), last.Description, last.Time.ToString("g", Loc.Culture));
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).UndoLastWithConfirmation();
        Refresh();
    }
}
