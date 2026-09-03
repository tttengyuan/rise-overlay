using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.ViewModels;

public sealed class QuestBriefingTargetViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isCapturable;
    private bool _hasSeverableTail;
    private string? _focusPartLabel;
    private bool _showSeparator;
    private BriefingTargetKind _kind = BriefingTargetKind.Quest;
    private bool _isDefeated;
    private ObservableCollection<ElementDisplayItem> _overallElements = new();
    private ObservableCollection<ElementId> _recommended = new();

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsCapturable
    {
        get => _isCapturable;
        set
        {
            if (SetField(ref _isCapturable, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaptureChipText)));
        }
    }

    public string CaptureChipText => _isCapturable ? "可捕获" : "不可捕";

    public bool HasSeverableTail
    {
        get => _hasSeverableTail;
        set => SetField(ref _hasSeverableTail, value);
    }

    public string? FocusPartLabel
    {
        get => _focusPartLabel;
        set
        {
            if (SetField(ref _focusPartLabel, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFocusPart)));
        }
    }

    public bool ShowFocusPart => !string.IsNullOrWhiteSpace(_focusPartLabel);

    public BriefingTargetKind Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KindChipText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInvasion)));
            }
        }
    }

    public string KindChipText => _kind == BriefingTargetKind.Invasion ? "入侵" : "任务";

    public bool IsInvasion => _kind == BriefingTargetKind.Invasion;

    public bool IsDefeated
    {
        get => _isDefeated;
        set
        {
            if (SetField(ref _isDefeated, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDefeatedChip)));
        }
    }

    public bool ShowDefeatedChip => _isDefeated;

    public bool ShowSeparator
    {
        get => _showSeparator;
        set => SetField(ref _showSeparator, value);
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

public sealed class QuestBriefingViewModel : INotifyPropertyChanged
{
    private const int MaxTargets = 4;

    private ObservableCollection<QuestBriefingTargetViewModel> _targets = new();

    public ObservableCollection<QuestBriefingTargetViewModel> Targets
    {
        get => _targets;
        private set
        {
            if (SetField(ref _targets, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetCountText)));
        }
    }

    public string TargetCountText => $"目标 ×{_targets.Count}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyDto(QuestBriefingDto dto)
    {
        var source = (dto.Targets ?? Array.Empty<QuestBriefingTargetDto>())
            .Take(MaxTargets)
            .ToList();

        var rows = new ObservableCollection<QuestBriefingTargetViewModel>();
        for (int i = 0; i < source.Count; i++)
        {
            var t = source[i];
            var recommended = t.Recommended ?? Array.Empty<ElementId>();
            var ordered = t.OverallElementsOrdered ?? Array.Empty<ElementId>();

            rows.Add(new QuestBriefingTargetViewModel
            {
                Name = t.Name,
                IsCapturable = t.IsCapturable,
                HasSeverableTail = t.HasSeverableTail,
                FocusPartLabel = t.FocusPartLabel,
                Kind = t.Kind,
                IsDefeated = t.IsDefeated,
                ShowSeparator = i < source.Count - 1,
                Recommended = new ObservableCollection<ElementId>(recommended),
                OverallElements = new ObservableCollection<ElementDisplayItem>(
                    ordered.Select(e => new ElementDisplayItem
                    {
                        Element = e,
                        IsDimmed = !recommended.Contains(e),
                    })),
            });
        }

        Targets = rows;
    }

    public static QuestBriefingViewModel CreateSampleSingle()
    {
        var vm = new QuestBriefingViewModel();
        vm.ApplyDto(new QuestBriefingDto(
        [
            Magnamalo(),
        ]));
        return vm;
    }

    public static QuestBriefingViewModel CreateSampleFour()
    {
        var vm = new QuestBriefingViewModel();
        vm.ApplyDto(new QuestBriefingDto(
        [
            Magnamalo(),
            new QuestBriefingTargetDto(
                Name: "天彗龙",
                IsCapturable: false,
                HasSeverableTail: true,
                FocusPartLabel: "头",
                OverallElementsOrdered:
                [
                    ElementId.Fire,
                    ElementId.Water,
                    ElementId.Thunder,
                    ElementId.Ice,
                    ElementId.Dragon,
                ],
                Recommended:
                [
                    ElementId.Fire,
                    ElementId.Water,
                    ElementId.Thunder,
                    ElementId.Ice,
                ]),
            new QuestBriefingTargetDto(
                Name: "爵银龙",
                IsCapturable: true,
                HasSeverableTail: false,
                FocusPartLabel: "前肢",
                OverallElementsOrdered:
                [
                    ElementId.Fire,
                    ElementId.Water,
                    ElementId.Thunder,
                    ElementId.Ice,
                    ElementId.Dragon,
                ],
                Recommended: [ElementId.Thunder, ElementId.Fire]),
            new QuestBriefingTargetDto(
                Name: "冰狼龙",
                IsCapturable: true,
                HasSeverableTail: true,
                FocusPartLabel: "头",
                OverallElementsOrdered:
                [
                    ElementId.Fire,
                    ElementId.Water,
                    ElementId.Thunder,
                    ElementId.Ice,
                    ElementId.Dragon,
                ],
                Recommended: [ElementId.Fire]),
        ]));
        return vm;
    }

    private static QuestBriefingTargetDto Magnamalo() => new(
        Name: "嗟怨震天怨虎龙",
        IsCapturable: true,
        HasSeverableTail: true,
        FocusPartLabel: "头",
        OverallElementsOrdered:
        [
            ElementId.Fire,
            ElementId.Water,
            ElementId.Thunder,
            ElementId.Ice,
            ElementId.Dragon,
        ],
        Recommended: [ElementId.Water]);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
