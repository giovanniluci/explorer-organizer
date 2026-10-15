using System.Windows;
using System.Windows.Controls;
using ExplorerOrganizer.App.Infrastructure;
using ExplorerOrganizer.Core.Localization;
using Microsoft.Win32;

namespace ExplorerOrganizer.App.Views;

/// <summary>
/// Offline feedback: nothing is ever sent. The user writes a note and saves it to a file (or copies it with the
/// diagnostic info) to share it however they like.
/// </summary>
public partial class FeedbackView : UserControl
{
    public FeedbackView()
    {
        InitializeComponent();
        InfoText.Text = DiagnosticInfo();
    }

    public static string DiagnosticInfo()
    {
        var host = AppHost.Current;
        return string.Join(Environment.NewLine,
            "Explorer Organizer " + typeof(FeedbackView).Assembly.GetName().Version?.ToString(3),
            "Windows " + Environment.OSVersion.Version + (Environment.Is64BitOperatingSystem ? " x64" : ""),
            ".NET " + Environment.Version,
            "UI: " + Loc.Culture.Name + " · " + (ThemeManager.IsDark ? "dark" : "light") + (Backdrop.MicaSupported ? " · Mica" : ""),
            "Profiles: " + host.Library.Profiles.Count + " · Rules: " + host.Library.Profiles.Sum(p => p.Rules.Count)
                + " · Folders: " + host.Library.Profiles.Sum(p => p.Folders.Count));
    }

    private string Compose() =>
        FeedbackBox.Text.Trim() + (IncludeInfo.IsChecked == true ? Environment.NewLine + Environment.NewLine + "---" + Environment.NewLine + DiagnosticInfo() : "");

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = Loc.T("Feedback.Filter"),
            FileName = $"explorer-organizer-feedback-{DateTime.Now:yyyyMMdd-HHmm}.txt",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, Compose());
            StatusText.Text = string.Format(Loc.T("Msg.Exported"), dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Dialogs.Error(ex.Message); }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Compose());
            StatusText.Text = Loc.T("Feedback.Copied");
        }
        catch (System.Runtime.InteropServices.COMException ex) { Dialogs.Error(ex.Message); } // clipboard busy
    }
}
