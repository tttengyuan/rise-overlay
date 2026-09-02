using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.Core.Domain.Dialog;
using HunterPie.Core.Observability.Logging;
using HunterPie.DI;
using HunterPie.Features.Debug.Mocks;
using HunterPie.Features.Overlay.Services;
using HunterPie.Internal;
using HunterPie.Internal.Tray;
using HunterPie.Platforms;
using HunterPie.UI.Main.Views;
using HunterPie.Usecases;
using RiseOverlay.UI.Shell;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HunterPie;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly ILogger _logger = LoggerFactory.Create();
    private RiseShellWindow? _shell;
    private ToolStripMenuItem? _trayShowParts;
    private ToolStripMenuItem? _trayShowAilments;
    private ToolStripMenuItem? _trayShowDps;
    private bool _syncingTrayHudToggles;

    private static MainView MainViewWindow => DependencyContainer.Get<MainView>();
    private static MainApplication MainApplication => DependencyContainer.Get<MainApplication>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        CheckForRunningInstances();

        base.OnStartup(e);

        await InitializerManager.InitializeCore();

        SupportedPlatformUseCase.Execute();

        DependencyProvider.LoadModules();
        await InitializerManager.InitializeAsync();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        CheckIfHunterPiePathIsSafe();
        SetupLanguage();
        SetupFrameRate();
        SetupRenderingMode();
        InitializeMainView();

        InitializerManager.InitializeGUI();

        DependencyContainer.Get<WidgetMocksProvider>()
            .MockEnabled();

        SetUiThreadPriority();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        InitializerManager.Unload();
        MainApplication.Dispose();
    }

    private void CheckIfHunterPiePathIsSafe()
    {
        bool isSafe = VerifyHunterPiePathUseCase.Invoke();

        if (isSafe)
            return;

        DialogManager.Warn(
            "Unsafe path",
            "It looks like you're executing HunterPie directly from the zip file. Please extract it first before running the client.",
            NativeDialogButtons.Accept
        );

        Shutdown();
    }

    private void SetupLanguage()
    {
        string fileName = ClientConfig.Config.Client.Language.Current;
        string language = fileName[..^".xml".Length];
        Thread.CurrentThread.CurrentCulture = new CultureInfo(language);
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(language);
        FrameworkElement.LanguageProperty.OverrideMetadata(
            forType: typeof(FrameworkElement),
            typeMetadata: new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(language))
        );
    }

    private void SetupFrameRate()
    {
        if (!ClientConfig.Config.Client.IsFramePerSecondLimitEnabled)
            return;

        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            forType: typeof(Timeline),
            typeMetadata: new FrameworkPropertyMetadata { DefaultValue = (int)ClientConfig.Config.Client.RenderFramePerSecond.Current }
        );
    }

    private void SetupRenderingMode()
    {
        RenderOptions.ProcessRenderMode = ClientConfig.Config.Client.Render == RenderingStrategy.Hardware
            ? RenderMode.Default
            : RenderMode.SoftwareOnly;
    }

    private async void InitializeMainView()
    {
        _logger.Info("Initializing Rise血条 shell UI");

        bool shouldStart = await MainApplication.Start();

        if (!shouldStart)
            return;

        // Keep MainView constructed (hotkeys / theme DI) but never show the old HunterPie chrome.
        _ = MainViewWindow;

        _shell = new RiseShellWindow();
        MainWindow = _shell;
        _shell.DesignModeChanged += enabled =>
        {
            DependencyContainer.Get<OverlayManager>().IsDesignModeEnabled = enabled;
        };
        _shell.DesignModeQuery = () => DependencyContainer.Get<OverlayManager>().IsDesignModeEnabled;

        SetupTrayIcon();
        SyncTrayHudTogglesFromConfig();
        RiseHudDisplaySettings.Subscribe(SyncTrayHudTogglesFromConfig);

        if (ClientConfig.Config.Client.EnableSeamlessStartup)
            return;

        _shell.Show();
    }

    private void CheckForRunningInstances()
    {
        Process[] processes = Process.GetProcessesByName("HunterPie")
            .Where(p => p.Id != Environment.ProcessId
                    && p.MainModule?.FileName == ClientInfo.ClientFileName)
            .ToArray();

        foreach (Process process in processes)
            process.Kill();
    }

    private void SetUiThreadPriority() => Dispatcher.Thread.Priority = ThreadPriority.Highest;

    private void OnTrayShowClick(object? sender, EventArgs e)
    {
        if (_shell is null)
            return;

        _shell.Show();
        _shell.WindowState = WindowState.Normal;
        _shell.Focus();
    }

    private void OnTrayExitClick(object? sender, EventArgs e)
    {
        _shell?.ForceExit();
    }

    private void SetupTrayIcon()
    {
        TrayService.AddDoubleClickHandler(OnTrayShowClick);

        if (TrayService.AddItem("显示窗口") is { } showButton)
            showButton.Click += OnTrayShowClick;

        _trayShowParts = CreateTrayHudToggle("显示部位", cfg => cfg.ShowParts);
        _trayShowAilments = CreateTrayHudToggle("显示异常", cfg => cfg.ShowAilments);
        _trayShowDps = CreateTrayHudToggle("显示 DPS", cfg => cfg.ShowDps);

        if (TrayService.AddItem("复位叠层位置") is { } resetPos)
            resetPos.Click += OnTrayResetPositionClick;

        if (TrayService.AddItem("退出程序") is { } closeButton)
            closeButton.Click += OnTrayExitClick;
    }

    private static ToolStripMenuItem? CreateTrayHudToggle(
        string label,
        Func<RiseCompactMonsterWidgetConfig, HunterPie.Core.Architecture.Observable<bool>> selector)
    {
        if (TrayService.AddItem(label) is not ToolStripMenuItem item)
            return null;

        item.CheckOnClick = true;
        item.Click += (_, _) =>
        {
            if (System.Windows.Application.Current is not App app || app._syncingTrayHudToggles)
                return;

            var cfg = RiseHudDisplaySettings.Config;
            selector(cfg).Value = item.Checked;
            app._shell?.RefreshHudToggles();
        };
        return item;
    }

    private void SyncTrayHudTogglesFromConfig()
    {
        if (_trayShowParts is null)
            return;

        var cfg = RiseHudDisplaySettings.Config;
        _syncingTrayHudToggles = true;
        try
        {
            _trayShowParts.Checked = cfg.ShowParts.Value;
            _trayShowAilments!.Checked = cfg.ShowAilments.Value;
            _trayShowDps!.Checked = cfg.ShowDps.Value;
        }
        finally
        {
            _syncingTrayHudToggles = false;
        }

        _shell?.RefreshHudToggles();
    }

    private void OnTrayResetPositionClick(object? sender, EventArgs e)
    {
        _shell?.Dispatcher.Invoke(() =>
        {
            RiseHudDisplaySettings.Config.Position.X = 20;
            RiseHudDisplaySettings.Config.Position.Y = 20;
        });
    }

    private async void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        await MainApplication.SendUiException(e.Exception);
    }

    public static async void Restart()
    {
        var app = (App)Current;
        if (app._shell is { } shell)
            await shell.Dispatcher.InvokeAsync(shell.Hide);

        await MainApplication.Restart();

        Current.Shutdown();
    }
}
