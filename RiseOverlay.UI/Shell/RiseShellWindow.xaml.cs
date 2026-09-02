using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Overlay;
using RiseOverlay.UI.Themes;
using RiseOverlay.UI.Update;

namespace RiseOverlay.UI.Shell;

public partial class RiseShellWindow : Window
{
    private static readonly Brush DotIdle = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private static readonly Brush DotOk = new SolidColorBrush(Color.FromRgb(0x4A, 0x9B, 0x9B));
    private static readonly Brush DotWarn = new SolidColorBrush(Color.FromRgb(0xB0, 0x9A, 0x4A));
    private static readonly Brush TextMuted = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2));
    private static readonly Brush TextMain = new SolidColorBrush(Colors.White);

    private readonly DispatcherTimer _pollTimer;
    private readonly RiseGitHubUpdateService _updateService = new();
    private bool _forceClose;
    private bool _syncingDesignMode;
    private bool _syncingHudToggles;
    private bool _checkingUpdate;

    /// <summary>Raised when the user toggles design/drag mode from the shell.</summary>
    public event Action<bool>? DesignModeChanged;

    /// <summary>Optional poll for ScrLk / external design-mode changes.</summary>
    public Func<bool>? DesignModeQuery { get; set; }

    private static RiseCompactMonsterWidgetConfig HudConfig => RiseHudDisplaySettings.Config;

    public RiseShellWindow()
    {
        InitializeComponent();
        SubtitleText.Text =
            $"Monster Hunter Rise · 紧凑战斗 HUD · v{RiseGitHubUpdateService.GetLocalVersionString()}";
        OverlayToggle.IsChecked = ClientConfig.Config.Overlay.IsEnabled;
        BuildThemeButtons();
        SyncHudTogglesFromConfig();
        RiseHudDisplaySettings.Subscribe(SyncHudTogglesFromConfig);
        UpdateAttachStatus();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _pollTimer.Tick += (_, _) => UpdateAttachStatus();
        _pollTimer.Start();

        Closing += OnWindowClosing;
        Closed += (_, _) => _pollTimer.Stop();
    }

    private void BuildThemeButtons()
    {
        ThemeGrid.Children.Clear();
        foreach (string id in RiseThemeIds.All)
        {
            var btn = new Button
            {
                Content = RiseThemeIds.DisplayName(id),
                Tag = id,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromRgb(0x15, 0x19, 0x22)),
                Foreground = TextMain,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x38)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            btn.Click += OnThemeButtonClick;
            ThemeGrid.Children.Add(btn);
        }

        HighlightSelectedTheme();
    }

    private void OnThemeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
            return;
        HudConfig.ThemeId.Value = id;
        RiseThemeService.ApplyTheme(id);
        HighlightSelectedTheme();
    }

    private void HighlightSelectedTheme()
    {
        string current = RiseThemeIds.Normalize(HudConfig.ThemeId.Value);
        foreach (Button btn in ThemeGrid.Children.OfType<Button>())
        {
            bool on = Equals(btn.Tag, current);
            btn.BorderBrush = new SolidColorBrush(on
                ? Color.FromRgb(0xFF, 0x4D, 0x6D)
                : Color.FromRgb(0x2A, 0x2F, 0x38));
            btn.BorderThickness = new Thickness(on ? 2 : 1);
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose)
            return;

        // Close button / Alt+F4 → tray (same as MinimizeToSystemTray).
        e.Cancel = true;
        Hide();
    }

    private void UpdateAttachStatus()
    {
        bool riseRunning = Process.GetProcessesByName("MonsterHunterRise").Length > 0;
        bool overlayOn = ClientConfig.Config.Overlay.IsEnabled;
        if (riseRunning)
        {
            if (overlayOn)
            {
                StatusText.Text = "已附加游戏";
                StatusText.Foreground = TextMain;
                StatusDot.Fill = DotOk;
            }
            else
            {
                StatusText.Text = "已检测到游戏 · 叠层已关闭";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;
            }
        }
        else
        {
            StatusText.Text = "等待游戏启动";
            StatusText.Foreground = TextMuted;
            StatusDot.Fill = DotIdle;
        }

        if (DesignModeQuery is { } query)
        {
            bool design = query();
            if (DesignModeToggle.IsChecked != design)
                SetDesignModeChecked(design);
        }
    }

    private void OnOverlayToggle(object sender, RoutedEventArgs e)
    {
        bool on = OverlayToggle.IsChecked == true;
        ClientConfig.Config.Overlay.IsEnabled.Value = on;
        UpdateAttachStatus();
    }

    private void OnDesignModeToggle(object sender, RoutedEventArgs e)
    {
        if (_syncingDesignMode)
            return;
        DesignModeChanged?.Invoke(DesignModeToggle.IsChecked == true);
    }

    private void OnHudDisplayToggle(object sender, RoutedEventArgs e)
    {
        if (_syncingHudToggles)
            return;

        var cfg = HudConfig;
        cfg.ShowParts.Value = ShowPartsToggle.IsChecked == true;
        cfg.ShowAilments.Value = ShowAilmentsToggle.IsChecked == true;
        cfg.ShowDps.Value = ShowDpsToggle.IsChecked == true;
        cfg.EnableCombatMotion.Value = CombatMotionToggle.IsChecked == true;
        RiseThemeService.SetMotionEnabled(cfg.EnableCombatMotion.Value);
    }

    private void SyncHudTogglesFromConfig()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(SyncHudTogglesFromConfig);
            return;
        }

        _syncingHudToggles = true;
        try
        {
            var cfg = HudConfig;
            ShowPartsToggle.IsChecked = cfg.ShowParts.Value;
            ShowAilmentsToggle.IsChecked = cfg.ShowAilments.Value;
            ShowDpsToggle.IsChecked = cfg.ShowDps.Value;
            CombatMotionToggle.IsChecked = cfg.EnableCombatMotion.Value;
            HighlightSelectedTheme();
        }
        finally
        {
            _syncingHudToggles = false;
        }
    }

    /// <summary>Tray menu keeps shell checkboxes in sync.</summary>
    public void RefreshHudToggles() => SyncHudTogglesFromConfig();

    /// <summary>Keep checkbox in sync when ScrLk toggles design mode.</summary>
    public void SetDesignModeChecked(bool enabled)
    {
        _syncingDesignMode = true;
        try
        {
            DesignModeToggle.IsChecked = enabled;
        }
        finally
        {
            _syncingDesignMode = false;
        }
    }

    private void OnResetPositionClick(object sender, RoutedEventArgs e)
    {
        var cfg = HudConfig;
        cfg.Position.X = 20;
        cfg.Position.Y = 20;
        StatusText.Text = "叠层位置已复位";
        StatusText.Foreground = TextMain;
        StatusDot.Fill = DotOk;
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_checkingUpdate)
            return;

        _checkingUpdate = true;
        CheckUpdateButton.IsEnabled = false;
        StatusText.Text = "正在检查更新…";
        StatusText.Foreground = TextMuted;
        StatusDot.Fill = DotWarn;

        try
        {
            RiseUpdateCheckResult result = await _updateService.CheckForUpdateAsync();
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage) && !result.HasUpdate)
            {
                StatusText.Text = result.ErrorMessage.Split('\n')[0];
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;

                MessageBoxResult open = MessageBox.Show(
                    result.ErrorMessage + "\n\n是否打开 Releases 页面？",
                    "检查更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.ReleasePageUrl))
                    Process.Start(new ProcessStartInfo(result.ReleasePageUrl) { UseShellExecute = true });
                return;
            }

            if (!result.HasUpdate)
            {
                StatusText.Text = $"已是最新版本 v{result.LocalVersion}";
                StatusText.Foreground = TextMain;
                StatusDot.Fill = DotOk;
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"发现新版本 v{result.RemoteVersion}（当前 v{result.LocalVersion}）\n\n下载并安装后将自动重启。是否继续？",
                "检查更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                StatusText.Text = "已取消更新";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotIdle;
                return;
            }

            if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                StatusText.Text = "没有可用的下载地址";
                StatusDot.Fill = DotWarn;
                return;
            }

            var progress = new Progress<double>(p =>
            {
                StatusText.Text = $"正在下载… {(int)(p * 100)}%";
                StatusText.Foreground = TextMain;
                StatusDot.Fill = DotOk;
            });

            StatusText.Text = "正在下载…";
            await _updateService.ApplyUpdateAsync(result.DownloadUrl, progress);
            StatusText.Text = "下载完成，正在重启…";
            ForceExit();
        }
        catch (Exception ex)
        {
            StatusText.Text = "更新失败";
            StatusText.Foreground = TextMuted;
            StatusDot.Fill = DotWarn;
            MessageBox.Show(
                $"更新失败：{ex.Message}\n\n可打开 {RiseUpdateEndpoints.ReleasesPage} 手动下载。",
                "检查更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _checkingUpdate = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => ForceExit();

    /// <summary>Tray "退出" / Exit button — really quit (bypass hide-to-tray).</summary>
    public void ForceExit()
    {
        _forceClose = true;
        Application.Current.Shutdown();
    }
}
