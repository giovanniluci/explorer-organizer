using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.App.Windows;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;

namespace ExplorerOrganizer.App.Views;

/// <summary>Rules page: the rules of the active profile, in priority order. Profiles themselves are managed in the Toolbox.</summary>
public partial class ProfilesView : UserControl, IRefreshablePage
{
    private bool _loading;

    public ProfilesView()
    {
        InitializeComponent();
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
        _loading = false;
        ShowRules();
    }

    private void ShowRules()
    {
        RuleList.ItemsSource = Selected?.Rules;
        if (RuleList.SelectedIndex < 0 && Selected?.Rules.Count > 0) RuleList.SelectedIndex = 0;
        EmptyText.Visibility = Selected == null || Selected.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected == null) return;
        Host.ActiveProfile = Selected; // the page always shows (and edits) the active profile
        Host.RaiseStateChanged();
        ShowRules();
    }

    private void Persist(object sender, RoutedEventArgs e) => Host.ProfilesChanged();

    private Rule? SelectedRule => RuleList.SelectedItem as Rule;

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) { Dialogs.Info(Loc.T("Msg.NoProfile")); return; }
        var rule = new Rule { Name = Loc.T("Rule.NewName") };
        rule.Condition.Children.Add(new ExtensionCondition { Extensions = "pdf" });
        rule.Actions.Add(new MoveAction { Destination = "{documents}\\PDF" });
        var editor = new RuleEditorWindow(rule, Selected) { Owner = Owner };
        if (editor.ShowDialog() != true) return;
        Selected.Rules.Add(editor.Result!);
        RuleList.SelectedItem = editor.Result;
        Host.ProfilesChanged();
        ShowRules();
    }

    private void EditRule_Click(object sender, RoutedEventArgs e) => EditSelectedRule();

    private void RuleList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedRule();

    private void RuleList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { EditSelectedRule(); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteRule_Click(sender, e); e.Handled = true; }
    }

    private void EditSelectedRule()
    {
        if (Selected == null || SelectedRule == null) return;
        var rule = SelectedRule;
        var editor = new RuleEditorWindow(rule, Selected) { Owner = Owner };
        if (editor.ShowDialog() != true) return;
        int index = Selected.Rules.IndexOf(rule);
        Selected.Rules[index] = editor.Result!;
        RuleList.SelectedItem = editor.Result;
        Host.ProfilesChanged();
    }

    private void DuplicateRule_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null || SelectedRule == null) return;
        var copy = Json.Clone(SelectedRule);
        copy.Id = Guid.NewGuid();
        copy.Name += " " + Loc.T("Common.CopySuffix");
        Selected.Rules.Insert(Selected.Rules.IndexOf(SelectedRule) + 1, copy);
        RuleList.SelectedItem = copy;
        Host.ProfilesChanged();
    }

    private void RuleUp_Click(object sender, RoutedEventArgs e) => MoveRule(-1);
    private void RuleDown_Click(object sender, RoutedEventArgs e) => MoveRule(+1);

    private void MoveRule(int delta)
    {
        if (Selected == null || SelectedRule == null) return;
        int i = Selected.Rules.IndexOf(SelectedRule), j = i + delta;
        if (j < 0 || j >= Selected.Rules.Count) return;
        var rule = SelectedRule;
        Selected.Rules.Move(i, j);
        RuleList.SelectedItem = rule;
        (RuleList.ItemContainerGenerator.ContainerFromItem(rule) as ListBoxItem)?.Focus();
        Host.ProfilesChanged();
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null || SelectedRule == null) return;
        if (!Dialogs.Confirm(string.Format(Loc.T("Confirm.DeleteRule"), SelectedRule.Name), danger: true)) return;
        Selected.Rules.Remove(SelectedRule);
        Host.ProfilesChanged();
        ShowRules();
    }
}
