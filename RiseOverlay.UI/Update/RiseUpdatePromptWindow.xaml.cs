using System.Windows;

namespace RiseOverlay.UI.Update;

public enum RiseUpdatePromptChoice
{
    Later,
    UpdateNow,
}

public partial class RiseUpdatePromptWindow : Window
{
    public RiseUpdatePromptChoice Choice { get; private set; } = RiseUpdatePromptChoice.Later;

    public RiseUpdatePromptWindow(string localVersion, string remoteVersion, string? releaseNotes)
    {
        InitializeComponent();
        VersionText.Text = $"当前 v{localVersion}  →  最新 v{remoteVersion}";
        NotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "（无发行说明）"
            : releaseNotes.Trim();
    }

    private void OnLaterClick(object sender, RoutedEventArgs e)
    {
        Choice = RiseUpdatePromptChoice.Later;
        DialogResult = false;
        Close();
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        Choice = RiseUpdatePromptChoice.UpdateNow;
        DialogResult = true;
        Close();
    }
}
