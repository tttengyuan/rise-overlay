using HunterPie.Core.Architecture;
using HunterPie.Core.Domain.Enums;
using HunterPie.Core.Settings;
using HunterPie.Core.Settings.Annotations;
using HunterPie.Core.Settings.Common;
using HunterPie.Core.Settings.Types;

namespace HunterPie.Core.Client.Configuration.Overlay;

/// <summary>
/// Compact Rise Overlay monster HUD (replaces legacy BossesWidget for Rise).
/// Hosted via the same HunterPie <c>Widget</c> / <c>WidgetView</c> / <c>OverlayManager</c>
/// path as other overlays (process attach + always-on-top window).
/// </summary>
[Configuration(
    name: "RISE_COMPACT_MONSTER_WIDGET_STRING",
    icon: "ICON_SKULL",
    group: CommonConfigurationGroups.OVERLAY,
    availableGames: GameProcessType.MonsterHunterRise)]
public class RiseCompactMonsterWidgetConfig : IWidgetSettings, ISettings
{
    [ConfigurationProperty("INITIALIZE_WIDGET_STRING", requiresRestart: true, group: CommonConfigurationGroups.GENERAL)]
    public Observable<bool> Initialize { get; set; } = true;

    [ConfigurationProperty("ENABLE_WIDGET_STRING", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> Enabled { get; set; } = true;

    [ConfigurationProperty("HIDE_WHEN_UI_VISIBLE_STRING", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> HideWhenUiOpen { get; set; } = false;

    [ConfigurationProperty("WIDGET_OPACITY", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Range Opacity { get; set; } = new(1, 1, 0.1, 0.1);

    [ConfigurationProperty("WIDGET_SCALE", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Range Scale { get; set; } = new(1, 2, 0.1, 0.1);

    /// <summary>0 = game-client left, 1 = right. Default hugs the top-right combat HUD corner.</summary>
    [ConfigurationProperty("RISE_HUD_HORIZONTAL_POSITION", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Range HorizontalPosition { get; set; } = new(1, 1, 0, 0.01);

    /// <summary>0 = game-client top, 1 = bottom. Default sits under the lock-on monster icons.</summary>
    [ConfigurationProperty("RISE_HUD_VERTICAL_POSITION", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Range VerticalPosition { get; set; } = new(0.18, 1, 0, 0.01);

    /// <summary>
    /// Absolute screen position written by <see cref="HorizontalPosition"/> /
    /// <see cref="VerticalPosition"/> against the live game client bounds.
    /// </summary>
    [ConfigurationProperty("WIDGET_POSITION", group: CommonConfigurationGroups.GENERAL)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Position Position { get; set; } = new(20, 20);

    [ConfigurationProperty("SHOW_PARTS_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> ShowParts { get; set; } = true;

    [ConfigurationProperty("SHOW_AILMENTS_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> ShowAilments { get; set; } = true;

    [ConfigurationProperty("SHOW_DPS_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> ShowDps { get; set; } = true;

    /// <summary>HUD theme pack id: Classic / Glass / Parchment / Soft / OledCoral / GlassCoral / Transparent.</summary>
    [ConfigurationProperty("RISE_HUD_THEME_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<string> ThemeId { get; set; } = "Classic";

    /// <summary>Master switch for weaken/stun/break/recommend pulse animations.</summary>
    [ConfigurationProperty("RISE_COMBAT_MOTION_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<bool> EnableCombatMotion { get; set; } = true;

    /// <summary>Parts row layout: <c>Capsule</c> (default) or <c>Bar</c> (legacy dual-rail).</summary>
    [ConfigurationProperty("RISE_PARTS_LAYOUT_STRING", group: CommonConfigurationGroups.CUSTOMIZATIONS)]
    [ConfigurationConditional(name: nameof(Initialize), withValue: true)]
    public Observable<string> PartsLayout { get; set; } = "Capsule";
}
