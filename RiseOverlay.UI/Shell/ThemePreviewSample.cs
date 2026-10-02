using System.Collections.ObjectModel;
using RiseOverlay.Domain;
using RiseOverlay.UI.ViewModels;

namespace RiseOverlay.UI.Shell;

/// <summary>Static sample HUD for theme hover preview in the shell.</summary>
internal static class ThemePreviewSample
{
    public static MonsterHudViewModel Create(bool useCapsuleParts = true)
    {
        var vm = new MonsterHudViewModel
        {
            Name = "雌火龙",
            HealthCurrent = 4200,
            HealthMax = 8500,
            HealthPercent = 49.4,
            CaptureState = CaptureDisplayState.Capturable,
            ShowParts = true,
            ShowAilments = true,
            MotionEnabled = false,
            UseCapsuleParts = useCapsuleParts,
        };

        vm.Recommended = new ObservableCollection<ElementId>
        {
            ElementId.Dragon,
            ElementId.Thunder,
        };

        vm.Parts = new ObservableCollection<PartRowViewModel>
        {
            Part("头", 180, 400, flinch: 92, maxFlinch: 100, qurio: true, weak: [ElementId.Fire, ElementId.Thunder]),
            Part("翼", 90, 220, flinch: 41, maxFlinch: 100, weak: [ElementId.Fire]),
            Part("背", 140, 300, flinch: 55, maxFlinch: 100, qurio: true),
            Part("尾", 60, 200, flinch: 92, maxFlinch: 100, severable: true, weak: [ElementId.Fire]),
            Part("脚", 50, 180, flinch: 28, maxFlinch: 100),
            Part("腹", 70, 210, flinch: 33, maxFlinch: 100, qurio: true),
        };

        return vm;
    }

    private static PartRowViewModel Part(
        string name,
        double hp,
        double maxHp,
        double flinch,
        double maxFlinch,
        bool qurio = false,
        bool severable = false,
        ElementId[]? weak = null)
    {
        var row = new PartRowViewModel
        {
            Name = name,
            CurrentHp = hp,
            MaxHp = maxHp,
            Flinch = flinch,
            MaxFlinch = maxFlinch,
            IsQurio = qurio,
            IsSeverable = severable,
        };
        if (weak is { Length: > 0 })
            row.SyncWeakElements(weak);
        return row;
    }
}
