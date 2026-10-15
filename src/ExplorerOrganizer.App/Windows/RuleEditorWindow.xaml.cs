using System.Text;
using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Engine;
using ExplorerOrganizer.Core.Localization;
using ExplorerOrganizer.Core.Model;
using Microsoft.Win32;
using Condition = ExplorerOrganizer.Core.Model.Condition;

namespace ExplorerOrganizer.App.Windows;

/// <summary>Visual "IF … THEN …" block editor. Works on a copy of the rule: Cancel discards everything.</summary>
public partial class RuleEditorWindow : Window
{
    private readonly Rule _working;
    private readonly Profile _profile;
    private readonly Guid _originalId;

    public RuleEditorWindow(Rule rule, Profile profile)
    {
        InitializeComponent();
        _originalId = rule.Id;
        _working = Json.Clone(rule);
        _profile = profile;
        DataContext = _working;
        TokenList.ItemsSource = TokenRenderer.Reference.Select(r => new { r.Token, Help = Loc.T(r.HelpKey) }).ToList();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    public Rule? Result { get; private set; }

    // ---------- conditions ----------

    private static readonly (string Key, Func<Condition> Create)[] ConditionKinds =
    {
        ("Cond.NameContains", () => new NameContainsCondition()),
        ("Cond.Name", () => new NameCondition { Operator = TextOperator.Contains }),
        ("Cond.Extension", () => new ExtensionCondition { Extensions = "jpg;png" }),
        ("Cond.ItemType", () => new ItemTypeCondition { Kind = ItemKind.Folder }),
        ("Cond.FolderContent", () => new FolderContentCondition { Extensions = "ttf;otf" }),
        ("Cond.FileType", () => new FileTypeCondition()),
        ("Cond.Size", () => new SizeCondition()),
        ("Cond.Date", () => new DateCondition()),
        ("Cond.Metadata", () => new MetadataCondition()),
        ("Cond.Content", () => new ContentCondition()),
        ("Cond.Attribute", () => new AttributeCondition()),
        ("Cond.AllFiles", () => new AllFilesCondition()),
    };

    private void AddCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ConditionGroup group } button) return;
        var menu = new ContextMenu();
        foreach (var (key, create) in ConditionKinds)
        {
            var item = new MenuItem { Header = Loc.T(key) };
            item.Click += (_, _) => group.Children.Add(create());
            menu.Items.Add(item);
        }
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ConditionGroup group }) return;
        var child = new ConditionGroup { Operator = group.Operator == GroupOperator.All ? GroupOperator.Any : GroupOperator.All };
        child.Children.Add(new ExtensionCondition { Extensions = "jpg" });
        child.Children.Add(new ExtensionCondition { Extensions = "png" });
        group.Children.Add(child);
    }

    private void RemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Condition c }) RemoveFrom(_working.Condition, c);
    }

    private static bool RemoveFrom(ConditionGroup group, Condition target)
    {
        if (group.Children.Remove(target)) return true;
        return group.Children.OfType<ConditionGroup>().Any(g => RemoveFrom(g, target));
    }

    // ---------- actions ----------

    private static readonly (string Key, Func<RuleAction> Create)[] ActionKinds =
    {
        ("Act.Move", () => new MoveAction { Destination = "{folder}\\" + Loc.T("Editor.DefaultFolder") }),
        ("Act.Copy", () => new CopyAction { Destination = "{documents}\\Backup" }),
        ("Act.Rename", () => new RenameAction { NewName = "{modified:yyyy-MM-dd} {name}.{ext}" }),
        ("Act.Sort", () => new SortIntoSubfoldersAction()),
        ("Act.Delete", () => new DeleteAction()),
        ("Act.Compress", () => new CompressAction()),
        ("Act.Extract", () => new ExtractAction()),
        ("Act.Shortcut", () => new CreateShortcutAction()),
        ("Act.OpenWith", () => new OpenWithAction { Application = "notepad.exe" }),
    };

    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var (key, create) in ActionKinds)
        {
            var item = new MenuItem { Header = Loc.T(key) };
            item.Click += (_, _) => _working.Actions.Add(create());
            menu.Items.Add(item);
        }
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private void ActionUp_Click(object sender, RoutedEventArgs e) => MoveAction(sender, -1);
    private void ActionDown_Click(object sender, RoutedEventArgs e) => MoveAction(sender, +1);

    private void MoveAction(object sender, int delta)
    {
        if (sender is not FrameworkElement { DataContext: RuleAction a }) return;
        int i = _working.Actions.IndexOf(a), j = i + delta;
        if (i >= 0 && j >= 0 && j < _working.Actions.Count) _working.Actions.Move(i, j);
    }

    private void RemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleAction a }) _working.Actions.Remove(a);
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RuleAction action, Tag: string property }) return;
        var dlg = new OpenFolderDialog { Title = Loc.T("Common.ChooseFolder") };
        if (dlg.ShowDialog(this) != true) return;
        action.GetType().GetProperty(property)?.SetValue(action, dlg.FolderName);
    }

    private void BrowseApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: OpenWithAction action }) return;
        var dlg = new OpenFileDialog { Filter = Loc.T("Filter.Programs") };
        if (dlg.ShowDialog(this) == true) action.Application = dlg.FileName;
    }

    private void Token_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string token })
        {
            try { Clipboard.SetText(token.Split(' ')[0]); } catch { /* clipboard busy */ }
        }
    }

    // ---------- test & save ----------

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = Loc.T("Editor.TestPick") };
        if (dlg.ShowDialog(this) != true) return;
        var host = AppHost.Current;
        var sb = new StringBuilder();
        try
        {
            var single = new Profile { Name = _profile.Name, Rules = { Json.Clone(_working) } };
            single.Rules[0].Enabled = true;
            var result = host.Engine.Run(new[] { new FileInput(dlg.FileName) }, single, new RunOptions { Simulate = true, Trigger = "manual" });
            if (result.Matched == 0)
            {
                sb.AppendLine(Loc.T("Editor.TestNoMatch"));
            }
            else
            {
                sb.AppendLine(Loc.T("Editor.TestMatch"));
                foreach (var r in result.Records)
                {
                    sb.Append("• ").Append(Loc.T("Enum.OperationKind." + r.Kind)).Append(": ");
                    sb.Append(r.Target.Length > 0 ? r.Target : r.Source);
                    if (r.Message.Length > 0) sb.Append("  (").Append(r.Message).Append(')');
                    sb.AppendLine();
                }
            }
            // warn when a higher-priority rule of the profile would take the file first
            var winner = host.Engine.FindRule(_profile, dlg.FileName, out _);
            if (winner != null && winner.Id != _originalId && result.Matched > 0)
            {
                int winnerIndex = _profile.Rules.IndexOf(winner);
                int mine = _profile.Rules.ToList().FindIndex(r => r.Id == _originalId);
                if (mine < 0 || winnerIndex < mine)
                    sb.AppendLine().AppendLine(string.Format(Loc.T("Editor.TestShadowed"), winner.Name));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine(ex.Message);
        }
        TestResult.Text = sb.ToString().TrimEnd();
        TestPanel.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_working.Name))
        {
            Dialogs.Error(Loc.T("Editor.ErrName"));
            NameBox.Focus();
            return;
        }
        if (_working.Condition.Children.Count == 0)
        {
            Dialogs.Error(Loc.T("Editor.ErrNoCondition"));
            return;
        }
        var badRegex = AllConditions(_working.Condition).OfType<TextCondition>()
            .Where(c => c.Operator == TextOperator.Regex)
            .Select(c => (c.Value, Error: TextMatcher.ValidateRegex(c.Value)))
            .FirstOrDefault(x => x.Error != null);
        if (badRegex.Error != null)
        {
            Dialogs.Error(string.Format(Loc.T("Editor.ErrRegex"), badRegex.Value, badRegex.Error));
            return;
        }
        if (_working.Actions.Count == 0 && !Dialogs.Confirm(Loc.T("Editor.ConfirmNoActions"))) return;
        if (_working.Actions.OfType<DeleteAction>().Any(d => d.Permanent) && !Dialogs.Confirm(Loc.T("Editor.ConfirmPermanent"), danger: true)) return;

        Result = _working;
        DialogResult = true;
    }

    private static IEnumerable<Condition> AllConditions(Condition c)
    {
        yield return c;
        if (c is ConditionGroup g)
            foreach (var child in g.Children)
                foreach (var x in AllConditions(child)) yield return x;
    }
}
