using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.ViewModels;

public sealed class ElementDisplayItem : INotifyPropertyChanged
{
    private ElementId _element;
    private bool _isDimmed;

    public ElementId Element
    {
        get => _element;
        set => SetField(ref _element, value);
    }

    public bool IsDimmed
    {
        get => _isDimmed;
        set => SetField(ref _isDimmed, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class PartRowViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private double _currentHp;
    private double _maxHp = 1;
    private bool _isSeverable;
    private bool _isBroken;
    private bool _isQurio;
    private bool _isRecommendedTarget;
    private ObservableCollection<ElementId> _weakElements = new();

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public double CurrentHp
    {
        get => _currentHp;
        set
        {
            if (SetField(ref _currentHp, value))
                NotifyDerived();
        }
    }

    public double MaxHp
    {
        get => _maxHp;
        set
        {
            if (SetField(ref _maxHp, value))
                NotifyDerived();
        }
    }

    public bool IsSeverable
    {
        get => _isSeverable;
        set
        {
            if (SetField(ref _isSeverable, value))
                NotifyDerived();
        }
    }

    public bool IsBroken
    {
        get => _isBroken;
        set
        {
            if (SetField(ref _isBroken, value))
                NotifyDerived();
        }
    }

    public bool IsQurio
    {
        get => _isQurio;
        set
        {
            if (SetField(ref _isQurio, value))
                NotifyDerived();
        }
    }

    public bool IsRecommendedTarget
    {
        get => _isRecommendedTarget;
        set => SetField(ref _isRecommendedTarget, value);
    }

    public bool HasWeakElements => _weakElements.Count > 0;

    public ObservableCollection<ElementId> WeakElements
    {
        get => _weakElements;
        set
        {
            if (SetField(ref _weakElements, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasWeakElements)));
        }
    }

    public double HealthRatio => _maxHp <= 0 ? 0 : Math.Clamp(_currentHp / _maxHp, 0, 1);

    private PartDisplayState DisplayState =>
        PartDisplayRules.Resolve(_isSeverable, _isBroken, _isQurio);

    public string StatusText => DisplayState.Text;

    public bool ShowActiveBar => DisplayState.ShowActiveBar;

    public bool ShowCompletedLine => DisplayState.IsComplete;

    public string HealthText => DisplayState.IsComplete
        ? "完成"
        : $"{FormatHp(_currentHp)}/{FormatHp(_maxHp)}";

    public double RowOpacity => DisplayState.IsComplete ? 0.64 : 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyDerived()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthRatio)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowOpacity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowActiveBar)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCompletedLine)));
    }

    private static string FormatHp(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class MonsterHudViewModel : INotifyPropertyChanged
{
    private bool _captureBannerVisible;
    private string _name = "";
    private bool _showUncapturableBadge;
    private double _healthPercent;
    private double _healthCurrent;
    private double _healthMax = 1;
    private bool _weakenLineVisible;
    private double _weakenLinePosition;
    private bool _pastWeakenLine;
    private bool _stunActive;
    private string _stunCountdownText = "";
    private string _statusLineText = "";
    private string _ailmentsLineText = "";
    private CaptureDisplayState _captureState = CaptureDisplayState.Capturable;
    private ObservableCollection<ElementDisplayItem> _overallElements = new();
    private ObservableCollection<ElementId> _recommended = new();
    private ObservableCollection<PartRowViewModel> _parts = new();
    private bool _showParts = true;
    private bool _showAilments = true;

    public bool ShowParts
    {
        get => _showParts;
        set => SetField(ref _showParts, value);
    }

    public bool ShowAilments
    {
        get => _showAilments;
        set
        {
            if (SetField(ref _showAilments, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowAilmentsLine)));
        }
    }

    public bool CaptureBannerVisible
    {
        get => _captureBannerVisible;
        set => SetField(ref _captureBannerVisible, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool ShowUncapturableBadge
    {
        get => _showUncapturableBadge;
        set => SetField(ref _showUncapturableBadge, value);
    }

    public CaptureDisplayState CaptureState
    {
        get => _captureState;
        set
        {
            if (!SetField(ref _captureState, value))
                return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureBadgeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureBadgeVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureBadgeIsCapturable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureBadgeIsAnomaly)));
        }
    }

    /// <summary>
    /// Slay/special quests on otherwise-capturable species: no badge (capture UI already hidden).
    /// </summary>
    public bool CaptureBadgeVisible => _captureState is not CaptureDisplayState.QuestRestricted;

    public string CaptureBadgeText => _captureState switch
    {
        CaptureDisplayState.Capturable => "当前任务可捕",
        CaptureDisplayState.SpeciesUncapturable => "不可捕",
        CaptureDisplayState.Anomaly => "怪异化",
        _ => "不可捕",
    };

    public bool CaptureBadgeIsCapturable => _captureState == CaptureDisplayState.Capturable;

    public bool CaptureBadgeIsAnomaly => _captureState == CaptureDisplayState.Anomaly;

    public double HealthPercent
    {
        get => _healthPercent;
        set
        {
            if (SetField(ref _healthPercent, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthRatio)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthPercentText)));
            }
        }
    }

    public double HealthCurrent
    {
        get => _healthCurrent;
        set
        {
            if (SetField(ref _healthCurrent, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthNumbersText)));
        }
    }

    public double HealthMax
    {
        get => _healthMax;
        set
        {
            if (SetField(ref _healthMax, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthNumbersText)));
        }
    }

    public bool WeakenLineVisible
    {
        get => _weakenLineVisible;
        set => SetField(ref _weakenLineVisible, value);
    }

    public double WeakenLinePosition
    {
        get => _weakenLinePosition;
        set => SetField(ref _weakenLinePosition, value);
    }

    public bool PastWeakenLine
    {
        get => _pastWeakenLine;
        set => SetField(ref _pastWeakenLine, value);
    }

    public bool StunActive
    {
        get => _stunActive;
        set => SetField(ref _stunActive, value);
    }

    public string StunCountdownText
    {
        get => _stunCountdownText;
        set => SetField(ref _stunCountdownText, value);
    }

    public string StatusLineText
    {
        get => _statusLineText;
        set => SetField(ref _statusLineText, value);
    }

    public string AilmentsLineText
    {
        get => _ailmentsLineText;
        set
        {
            if (SetField(ref _ailmentsLineText, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowAilmentsLine)));
        }
    }

    public bool ShowAilmentsLine => _showAilments && !string.IsNullOrWhiteSpace(_ailmentsLineText);

    public ObservableCollection<ElementDisplayItem> OverallElements
    {
        get => _overallElements;
        set => SetField(ref _overallElements, value);
    }

    public ObservableCollection<ElementId> Recommended
    {
        get => _recommended;
        set => SetField(ref _recommended, value);
    }

    public ObservableCollection<PartRowViewModel> Parts
    {
        get => _parts;
        set => SetField(ref _parts, value);
    }

    public double HealthRatio => Math.Clamp(_healthPercent / 100.0, 0, 1);

    public string HealthPercentText => $"{Math.Round(_healthPercent):0}%";

    public string HealthNumbersText =>
        $"{FormatHp(_healthCurrent)}/{FormatHp(_healthMax)}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyDto(MonsterHudDto dto)
    {
        Name = dto.Name;
        HealthCurrent = dto.HealthCurrent;
        HealthMax = dto.HealthMax;
        HealthPercent = dto.HealthMax <= 0 ? 0 : dto.HealthCurrent / dto.HealthMax * 100.0;

        CaptureState = dto.CaptureState;
        ShowUncapturableBadge = !dto.IsCapturable;

        var threshold = dto.CaptureThresholdPercent;
        // Restricted and anomaly quests receive no capture line from the mapper.
        WeakenLineVisible = threshold is > 0;
        WeakenLinePosition = threshold is { } t ? Math.Clamp(t / 100.0, 0, 1) : 0;
        PastWeakenLine = threshold is { } th
            && CaptureRules.IsPastThreshold(HealthPercent, th);
        // Capture banner only when actually capturable.
        CaptureBannerVisible = dto.IsCapturable && PastWeakenLine;

        var recommended = dto.Recommended ?? Array.Empty<ElementId>();
        Recommended = new ObservableCollection<ElementId>(recommended);

        var ordered = dto.OverallElementsOrdered ?? Array.Empty<ElementId>();
        OverallElements = new ObservableCollection<ElementDisplayItem>(
            ordered.Select(e => new ElementDisplayItem
            {
                Element = e,
                IsDimmed = !recommended.Contains(e),
            }));

        StunActive = dto.Status.StunActive && dto.Status.StunActiveRemaining is { TotalSeconds: > 0 };
        StunCountdownText = dto.Status.StunActiveRemaining is { } rem
            ? StatusLineFormatter.FormatStunCountdown(rem)
            : "";

        // Active stun is shown as a pulsing chip; keep the rest of the status line without duplicating it.
        var statusForLine = StunActive
            ? dto.Status with { StunActive = false, StunActiveRemaining = null, StunBuildupPercent = null }
            : dto.Status;
        StatusLineText = StatusLineFormatter.Format(statusForLine);

        Parts = new ObservableCollection<PartRowViewModel>(
            (dto.Parts ?? Array.Empty<PartDto>()).Select(p => new PartRowViewModel
            {
                Name = p.Name,
                CurrentHp = p.CurrentHp,
                MaxHp = p.MaxHp,
                IsSeverable = p.IsSeverable,
                IsBroken = p.IsBroken,
                IsQurio = p.IsQurio,
                IsRecommendedTarget = p.IsRecommendedTarget,
                WeakElements = new ObservableCollection<ElementId>(p.WeakElements),
            }));

        AilmentsLineText = AilmentLineFormatter.Format(dto.Ailments ?? Array.Empty<AilmentDto>());
    }

    public static MonsterHudViewModel CreateSampleScornedMagnamalo()
    {
        var dto = new MonsterHudDto(
            Name: "嗟怨震天怨虎龙",
            HealthCurrent: 4820,
            HealthMax: 24500,
            IsCapturable: true,
            CaptureThresholdPercent: 25,
            OverallElementsOrdered:
            [
                ElementId.Fire,
                ElementId.Water,
                ElementId.Thunder,
                ElementId.Ice,
                ElementId.Dragon,
            ],
            Recommended: [ElementId.Water],
            Status: new StatusLineModel(
                EnrageRemaining: TimeSpan.FromSeconds(14),
                StunBuildupPercent: 88,
                StunActive: false,
                StunActiveRemaining: null,
                StaminaPercent: 62,
                DownRemaining: TimeSpan.FromSeconds(2)),
            Parts:
            [
                new PartDto("头部", 620, 1200, false, false, [ElementId.Water]),
                new PartDto("前肢", 0, 900, false, true, [ElementId.Water, ElementId.Ice]),
                new PartDto("腹部", 1100, 1500, false, false, [ElementId.Water]),
                new PartDto("尾巴", 280, 800, true, false, [ElementId.Water, ElementId.Dragon]),
                new PartDto("背鳍", 450, 700, false, false, [ElementId.Water]),
            ],
            Ailments:
            [
                new AilmentDto("poison", "毒", 42, false),
                new AilmentDto("paralysis", "麻", 0, true),
                new AilmentDto("sleep", "眠", 15, false),
                new AilmentDto("blast", "爆", 67, false),
                new AilmentDto("exhaust", "减气", 8, false),
                new AilmentDto("stun", "晕眩", 88, false),
            ]);

        var vm = new MonsterHudViewModel();
        vm.ApplyDto(dto);
        return vm;
    }

    private static string FormatHp(double v) => v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.#");

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
