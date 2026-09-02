using System.Windows;
using HunterPie.Core.Client.Configuration.Overlay;

namespace RiseOverlay.UI.Themes;

/// <summary>Swaps the shared Rise HUD theme ResourceDictionary at application scope.</summary>
public static class RiseThemeService
{
    private static ResourceDictionary? _current;
    private static string _themeId = RiseThemeIds.Classic;
    private static bool _motionEnabled = true;
    private static bool _configBound;

    public static string CurrentThemeId => _themeId;

    public static bool MotionEnabled => _motionEnabled;

    public static event Action? ThemeChanged;

    public static event Action? MotionChanged;

    public static void InitializeFromConfig(RiseCompactMonsterWidgetConfig config)
    {
        ApplyTheme(config.ThemeId.Value);
        SetMotionEnabled(config.EnableCombatMotion.Value);

        if (_configBound)
            return;
        _configBound = true;

        config.ThemeId.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "Value" or null)
                ApplyTheme(config.ThemeId.Value);
        };
        config.EnableCombatMotion.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "Value" or null)
                SetMotionEnabled(config.EnableCombatMotion.Value);
        };
    }

    public static void ApplyTheme(string? themeId)
    {
        string id = RiseThemeIds.Normalize(themeId);
        var app = Application.Current;
        if (app is null)
        {
            _themeId = id;
            return;
        }

        var next = new ResourceDictionary
        {
            Source = new Uri(RiseThemeIds.PackUri(id), UriKind.Absolute),
        };

        if (_current is not null)
            app.Resources.MergedDictionaries.Remove(_current);

        // Also strip legacy CompactTeal / older Theme.* merges so DynamicResource resolves to one pack.
        for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            string? src = app.Resources.MergedDictionaries[i].Source?.OriginalString;
            if (src is null)
                continue;
            if (src.Contains("/Themes/Theme.", StringComparison.OrdinalIgnoreCase)
                || src.Contains("/Themes/CompactTeal.xaml", StringComparison.OrdinalIgnoreCase))
            {
                app.Resources.MergedDictionaries.RemoveAt(i);
            }
        }

        app.Resources.MergedDictionaries.Insert(0, next);
        _current = next;
        _themeId = id;
        ThemeChanged?.Invoke();
    }

    public static void SetMotionEnabled(bool enabled)
    {
        if (_motionEnabled == enabled)
            return;
        _motionEnabled = enabled;
        MotionChanged?.Invoke();
    }
}
