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
        set => SetField(ref _isSeverable, value);
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

    public ObservableCollection<ElementId> WeakElements
    {
        get => _weakElements;
        set => SetField(ref _weakElements, value);
    }

    public double HealthRatio => _maxHp <= 0 ? 0 : Math.Clamp(_currentHp / _maxHp, 0, 1);

    public string HealthText => _isBroken
        ? "已破坏"
        : $"{FormatHp(_currentHp)}/{FormatHp(_maxHp)}";

    public double RowOpacity => _isBroken ? 0.45 : 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyDerived()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthRatio)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HealthText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowOpacity)));
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

public sealed class AilmentRowViewModel : INotifyPropertyChanged
{
    private string _key = "";
    private string _displayName = "";
    private double _percent;
    private bool _isActive;

    public string Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public double Percent
    {
        get => _percent;
        set
        {
            if (SetField(ref _percent, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BarRatio)));
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetField(ref _isActive, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueText)));
        }
    }

    public string ValueText => _isActive ? "触发" : $"{Math.Round(_percent):0}%";

    public double BarRatio => Math.Clamp(_percent / 100.0, 0, 1);

    public event PropertyChangedEventHandler? PropertyChanged;

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
    private string _statusLineText = "";
    private ObservableCollection<ElementDisplayItem> _overallElements = new();
    private ObservableCollection<ElementId> _recommended = new();
    private ObservableCollection<PartRowViewModel> _parts = new();
    private ObservableCollection<AilmentRowViewModel> _ailments = new();

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

    public string StatusLineText
    {
        get => _statusLineText;
        set => SetField(ref _statusLineText, value);
    }

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

    public ObservableCollection<AilmentRowViewModel> Ailments
    {
        get => _ailments;
        set => SetField(ref _ailments, value);
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

        ShowUncapturableBadge = !dto.IsCapturable;

        var threshold = dto.CaptureThresholdPercent;
        WeakenLineVisible = dto.IsCapturable && threshold is > 0;
        WeakenLinePosition = threshold is { } t ? Math.Clamp(t / 100.0, 0, 1) : 0;
        PastWeakenLine = dto.IsCapturable
            && threshold is { } th
            && CaptureRules.IsPastThreshold(HealthPercent, th);
        CaptureBannerVisible = PastWeakenLine;

        var recommended = dto.Recommended ?? Array.Empty<ElementId>();
        Recommended = new ObservableCollection<ElementId>(recommended);

        var ordered = dto.OverallElementsOrdered ?? Array.Empty<ElementId>();
        OverallElements = new ObservableCollection<ElementDisplayItem>(
            ordered.Select(e => new ElementDisplayItem
            {
                Element = e,
                IsDimmed = !recommended.Contains(e),
            }));

        StatusLineText = StatusLineFormatter.Format(dto.Status);

        Parts = new ObservableCollection<PartRowViewModel>(
            (dto.Parts ?? Array.Empty<PartDto>()).Select(p => new PartRowViewModel
            {
                Name = p.Name,
                CurrentHp = p.CurrentHp,
                MaxHp = p.MaxHp,
                IsSeverable = p.IsSeverable,
                IsBroken = p.IsBroken,
                WeakElements = new ObservableCollection<ElementId>(p.WeakElements),
            }));

        Ailments = new ObservableCollection<AilmentRowViewModel>(
            (dto.Ailments ?? Array.Empty<AilmentDto>())
                .Where(a => !string.Equals(a.Key, "stun", StringComparison.OrdinalIgnoreCase))
                .Where(a => a.IsActive || a.Percent > 0)
                .Select(a => new AilmentRowViewModel
                {
                    Key = a.Key,
                    DisplayName = a.DisplayName,
                    Percent = a.Percent,
                    IsActive = a.IsActive,
                }));
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
