using HunterPie.Core.Architecture;
using HunterPie.Core.Settings;
using HunterPie.Core.Settings.Annotations;
using HunterPie.Core.Settings.Common;
using HunterPie.Core.Settings.Types;

namespace HunterPie.Core.Client.Configuration.Overlay;

[Configuration(
    name: "OVERLAY_STRING",
    icon: "ICON_OVERLAY",
    group: CommonConfigurationGroups.OVERLAY)]
public class OverlayClientConfig : ISettings
{
    #region General Settings
    [ConfigurationProperty("OVERLAY_ENABLED_STRING", group: CommonConfigurationGroups.GENERAL)]
    public Observable<bool> IsEnabled { get; set; } = true;

    /// <summary>
    /// Rise Overlay default: hide widgets when the game window is not focused
    /// (attach/follow-style visibility tied to the attached game process).
    /// </summary>
    [ConfigurationProperty("OVERLAY_HIDE_WHEN_GAME_UNFOCUS_STRING", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(IsEnabled), withValue: true)]
    public Observable<bool> HideWhenUnfocus { get; set; } = true;
    #endregion

    #region Hotkeys Settings
    /// <summary>
    /// Global overlay show/hide. Rise Overlay reuses this (no separate RiseOverlay.Toggle).
    /// Default: Ctrl+Alt+O.
    /// </summary>
    [ConfigurationProperty("OVERLAY_KEYBINDING_TOGGLE_VISIBILITY_STRING", requiresRestart: true, group: CommonConfigurationGroups.HOTKEYS)]
    public Keybinding ToggleVisibility { get; set; } = "Ctrl+Alt+O";

    [ConfigurationProperty("OVERLAY_KEYBINDING_TOGGLE_DESIGN_MODE", requiresRestart: true, group: CommonConfigurationGroups.HOTKEYS)]
    [ConfigurationConditional(name: nameof(IsEnabled), withValue: true)]
    public Keybinding ToggleDesignMode { get; set; } = "ScrollLock";
    #endregion
}