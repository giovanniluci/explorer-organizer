using System.Windows;
using ExplorerOrganizer.Core.Localization;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>Standard Windows message boxes (screen-reader friendly, keyboard accessible).</summary>
public static class Dialogs
{
    private static Window? Owner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    public static void Info(string text) => Show(text, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string text) => Show(text, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static bool Confirm(string text, bool danger = false) =>
        Show(text, MessageBoxButton.YesNo, danger ? MessageBoxImage.Warning : MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    private static MessageBoxResult Show(string text, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult def = MessageBoxResult.OK)
    {
        var owner = Owner;
        return owner != null
            ? MessageBox.Show(owner, text, Loc.T("App.Title"), buttons, image, def)
            : MessageBox.Show(text, Loc.T("App.Title"), buttons, image, def);
    }
}
