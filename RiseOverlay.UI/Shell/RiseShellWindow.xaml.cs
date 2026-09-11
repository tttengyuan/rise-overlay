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
    private static readonly Brush DotIdle = Freeze(Color.FromRgb(0x6B, 0x72, 0x80));
    private static readonly Brush DotOk = Freeze(Color.FromRgb(0x4A, 0x9B, 0x9B));
    private static readonly Brush DotWarn = Freeze(Color.FromRgb(0xB0, 0x9A, 0x4A));
    private static readonly Brush TextMuted = Freeze(Color.FromRgb(0x8B, 0x93, 0xA1));
    private static readonly Brush TextMain = Freeze(Colors.White);
    private static readonly Brush TextAccentSoft = Freeze(Color.FromRgb(0x5E, 0xCF, 0xC4));
    private static readonly Brush PillOkBg = Freeze(Color.FromArgb(0x1A, 0x4A, 0x9B, 0x9B));
    private static readonly Brush PillOkBorder = Freeze(Color.FromArgb(0x48, 0x4A, 0x9B, 0x9B));
    private static readonly Brush PillWarnBg = Freeze(Color.FromArgb(0x22, 0xB0, 0x9A, 0x4A));
    private static readonly Brush PillWarnBorder = Freeze(Color.FromArgb(0x55, 0xB0, 0x9A, 0x4A));
    private static readonly Brush PillIdleBg = Freeze(Color.FromArgb(0x18, 0x6B, 0x72, 0x80));
    private static readonly Brush PillIdleBorder = Freeze(Color.FromArgb(0x40, 0x6B, 0x72, 0x80));
    private static readonly Brush ThemeCardBg = Freeze(Color.FromRgb(0x0D, 0x11, 0x18));
    private static readonly Brush ThemeBorder = Freeze(Color.FromRgb(0x2A, 0x30, 0x3C));
    private static readonly Brush ThemeSelectedFill = Freeze(Color.FromRgb(0x16, 0x28, 0x2A));
    private static readonly Brush ThemeSelectedBorder = Freeze(Color.FromRgb(0x5E, 0xCF, 0xC4));
    private static readonly Brush ThemeSelectedLabelBg = Freeze(Color.FromRgb(0x1A, 0x3A, 0x3A));
    private static readonly Brush ThemeSelectedLabel = Freeze(Color.FromRgb(0x5E, 0xCF, 0xC4));

    private readonly DispatcherTimer _pollTimer;
    private readonly RiseGitHubUpdateService _updateService = new();
    private bool _forceClose;
    private bool _syncingDesignMode;
    private bool _syncingHudToggles;
    private bool _checkingUpdate;
    private bool _startupUpdatePrompted;

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
        Loaded += (_, _) => _ = CheckForUpdateOnStartupAsync();
    }

    private void BuildThemeButtons()
    {
        ThemeGrid.Children.Clear();
        foreach (string id in RiseThemeIds.All)
        {
            var preview = new Border
            {
                Height = 30,
                Background = ThemePreviewBrush(id),
            };
            var checkBadge = new Border
            {
                Tag = "SelectedBadge",
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = Freeze(Color.FromRgb(0x4A, 0x9B, 0x9B)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -1, 0, 0),
                },
            };
            var previewHost = new Grid();
            previewHost.Children.Add(preview);
            previewHost.Children.Add(checkBadge);

            var label = new TextBlock
            {
                Text = RiseThemeIds.DisplayName(id),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = TextMain,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var labelBar = new Border
            {
                Padding = new Thickness(0, 7, 0, 7),
                Background = Brushes.Transparent,
                Child = label,
            };

            var stack = new StackPanel();
            stack.Children.Add(previewHost);
            stack.Children.Add(labelBar);

            var btn = new Button
            {
                Tag = id,
                Content = stack,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(0),
                Background = ThemeCardBg,
                BorderBrush = ThemeBorder,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                FocusVisualStyle = null,
                Template = CreateThemeCardTemplate(),
            };
            btn.Click += OnThemeButtonClick;
            ThemeGrid.Children.Add(btn);
        }

        HighlightSelectedTheme();
    }

    private static ControlTemplate CreateThemeCardTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
        borderFactory.SetValue(Border.ClipToBoundsProperty, true);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        borderFactory.AppendChild(presenter);
        template.VisualTree = borderFactory;
        return template;
    }

    private static Brush ThemePreviewBrush(string themeId)
    {
        LinearGradientBrush brush = themeId switch
        {
            RiseThemeIds.Glass => Gradient(
                Color.FromRgb(0x1E, 0x28, 0x36),
                Color.FromRgb(0x8E, 0xB4, 0xC8)),
            RiseThemeIds.Parchment => Gradient(
                Color.FromRgb(0xF3, 0xEF, 0xE6),
                Color.FromRgb(0xC4, 0xA5, 0x74)),
            RiseThemeIds.Soft => Gradient(
                Color.FromRgb(0xF7, 0xF5, 0xF2),
                Color.FromRgb(0x7A, 0x9E, 0x9A)),
            RiseThemeIds.OledCoral => Gradient(
                Color.FromRgb(0x0A, 0x0A, 0x0A),
                Color.FromRgb(0xFF, 0x4D, 0x6D),
                Color.FromRgb(0x5E, 0xCF, 0xC4)),
            RiseThemeIds.GlassCoral => Gradient(
                Color.FromRgb(0x1A, 0x15, 0x20),
                Color.FromRgb(0xFF, 0x6B, 0x8A),
                Color.FromRgb(0x5E, 0xCF, 0xC4)),
            RiseThemeIds.Transparent => Gradient(
                Color.FromArgb(0x40, 0x14, 0x18, 0x20),
                Color.FromArgb(0x55, 0x50, 0xC8, 0xBE)),
            _ => Gradient(
                Color.FromRgb(0x1A, 0x24, 0x33),
                Color.FromRgb(0xC9, 0xA2, 0x27),
                Color.FromRgb(0x4A, 0x9B, 0x9B)),
        };
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(params Color[] colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
        };
        if (colors.Length == 1)
        {
            brush.GradientStops.Add(new GradientStop(colors[0], 0));
            brush.GradientStops.Add(new GradientStop(colors[0], 1));
            return brush;
        }

        for (int i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(colors[i], i / (double)(colors.Length - 1)));
        return brush;
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
            btn.BorderBrush = on ? ThemeSelectedBorder : ThemeBorder;
            btn.BorderThickness = new Thickness(on ? 2.5 : 1);
            btn.Background = on ? ThemeSelectedFill : ThemeCardBg;

            if (btn.Content is not StackPanel stack || stack.Children.Count < 2)
                continue;

            if (stack.Children[0] is Grid previewHost)
            {
                foreach (Border child in previewHost.Children.OfType<Border>())
                {
                    if (Equals(child.Tag, "SelectedBadge"))
                        child.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            if (stack.Children[1] is Border labelBar && labelBar.Child is TextBlock label)
            {
                labelBar.Background = on ? ThemeSelectedLabelBg : Brushes.Transparent;
                label.Foreground = on ? ThemeSelectedLabel : TextMain;
            }
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
                StatusText.Foreground = TextAccentSoft;
                StatusDot.Fill = DotOk;
                SetStatusPill(PillOkBg, PillOkBorder);
            }
            else
            {
                StatusText.Text = "已检测到游戏 · 叠层已关闭";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;
                SetStatusPill(PillWarnBg, PillWarnBorder);
            }
        }
        else
        {
            StatusText.Text = "等待游戏启动";
            StatusText.Foreground = TextMuted;
            StatusDot.Fill = DotIdle;
            SetStatusPill(PillIdleBg, PillIdleBorder);
        }

        if (DesignModeQuery is { } query)
        {
            bool design = query();
            if (DesignModeToggle.IsChecked != design)
                SetDesignModeChecked(design);
        }
    }

    private void SetStatusPill(Brush background, Brush border)
    {
        StatusPill.Background = background;
        StatusPill.BorderBrush = border;
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
        StatusText.Foreground = TextAccentSoft;
        StatusDot.Fill = DotOk;
        SetStatusPill(PillOkBg, PillOkBorder);
    }

    private async Task CheckForUpdateOnStartupAsync()
    {
        if (_startupUpdatePrompted || _checkingUpdate)
            return;

        try
        {
            RiseUpdateCheckResult result = await _updateService.CheckForUpdateAsync();
            if (!result.HasUpdate || string.IsNullOrWhiteSpace(result.DownloadUrl))
                return;

            _startupUpdatePrompted = true;
            var prompt = new RiseUpdatePromptWindow(
                result.LocalVersion,
                result.RemoteVersion ?? "?",
                result.ReleaseNotes)
            {
                Owner = this,
            };
            bool? accepted = prompt.ShowDialog();
            if (accepted != true || prompt.Choice != RiseUpdatePromptChoice.UpdateNow)
            {
                StatusText.Text = $"有新版本 v{result.RemoteVersion}，建议尽快更新";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;
                SetStatusPill(PillWarnBg, PillWarnBorder);
                return;
            }

            await DownloadAndApplyUpdateAsync(result);
        }
        catch
        {
            // Startup check is best-effort; manual「检查更新」仍可用。
        }
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
        SetStatusPill(PillWarnBg, PillWarnBorder);

        try
        {
            RiseUpdateCheckResult result = await _updateService.CheckForUpdateAsync();
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage) && !result.HasUpdate)
            {
                StatusText.Text = result.ErrorMessage.Split('\n')[0];
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;
                SetStatusPill(PillWarnBg, PillWarnBorder);

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
                StatusText.Foreground = TextAccentSoft;
                StatusDot.Fill = DotOk;
                SetStatusPill(PillOkBg, PillOkBorder);
                return;
            }

            var prompt = new RiseUpdatePromptWindow(
                result.LocalVersion,
                result.RemoteVersion ?? "?",
                result.ReleaseNotes)
            {
                Owner = this,
            };
            bool? accepted = prompt.ShowDialog();
            if (accepted != true || prompt.Choice != RiseUpdatePromptChoice.UpdateNow)
            {
                StatusText.Text = "已取消更新";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotIdle;
                SetStatusPill(PillIdleBg, PillIdleBorder);
                return;
            }

            await DownloadAndApplyUpdateAsync(result);
        }
        catch (Exception ex)
        {
            if (StatusText.Text != "更新失败")
            {
                StatusText.Text = "更新失败";
                StatusText.Foreground = TextMuted;
                StatusDot.Fill = DotWarn;
                SetStatusPill(PillWarnBg, PillWarnBorder);
                MessageBox.Show(
                    $"更新失败：{ex.Message}\n\n可打开 {RiseUpdateEndpoints.ReleasesPage} 手动下载。",
                    "检查更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            _checkingUpdate = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async Task DownloadAndApplyUpdateAsync(RiseUpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
        {
            StatusText.Text = "没有可用的下载地址";
            StatusDot.Fill = DotWarn;
            SetStatusPill(PillWarnBg, PillWarnBorder);
            return;
        }

        bool ownedGate = !_checkingUpdate;
        if (ownedGate)
        {
            _checkingUpdate = true;
            CheckUpdateButton.IsEnabled = false;
        }

        try
        {
            var progress = new Progress<double>(p =>
            {
                StatusText.Text = $"正在下载… {(int)(p * 100)}%";
                StatusText.Foreground = TextMain;
                StatusDot.Fill = DotOk;
                SetStatusPill(PillOkBg, PillOkBorder);
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
            SetStatusPill(PillWarnBg, PillWarnBorder);
            MessageBox.Show(
                $"更新失败：{ex.Message}\n\n可打开 {RiseUpdateEndpoints.ReleasesPage} 手动下载。",
                "检查更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            throw;
        }
        finally
        {
            if (ownedGate)
            {
                _checkingUpdate = false;
                CheckUpdateButton.IsEnabled = true;
            }
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => ForceExit();

    /// <summary>Tray "退出" / Exit button — really quit (bypass hide-to-tray).</summary>
    public void ForceExit()
    {
        _forceClose = true;
        Application.Current.Shutdown();
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
