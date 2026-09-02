using System.ComponentModel;
using HunterPie.Core.Client;
using HunterPie.Core.Client.Configuration.Overlay;

namespace RiseOverlay.UI.Shell;

/// <summary>Shared access to compact HUD display toggles (shell + tray).</summary>
public static class RiseHudDisplaySettings
{
    public static RiseCompactMonsterWidgetConfig Config =>
        ((MHROverlayConfig)ClientConfig.Config.Rise.Overlay).RiseCompactMonsterWidget;

    public static void Subscribe(Action syncUi)
    {
        var cfg = Config;
        cfg.ShowParts.PropertyChanged += OnChanged;
        cfg.ShowAilments.PropertyChanged += OnChanged;
        cfg.ShowDps.PropertyChanged += OnChanged;

        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Value" or null)
                syncUi();
        }
    }
}
