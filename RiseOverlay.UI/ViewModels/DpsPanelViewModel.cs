using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.ViewModels;

public sealed class DpsEntryRowViewModel : INotifyPropertyChanged
{
    private int _index;
    private string _name = "";
    private bool _isSelf;
    private double _dps;
    private long _totalDamage;
    private double _shareRatio;

    public int Index
    {
        get => _index;
        set => SetField(ref _index, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsSelf
    {
        get => _isSelf;
        set => SetField(ref _isSelf, value);
    }

    public double Dps
    {
        get => _dps;
        set
        {
            if (SetField(ref _dps, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DpsText)));
        }
    }

    public long TotalDamage
    {
        get => _totalDamage;
        set
        {
            if (SetField(ref _totalDamage, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDamageText)));
        }
    }

    public double ShareRatio
    {
        get => _shareRatio;
        set => SetField(ref _shareRatio, value);
    }

    public string DpsText => FormatDps(_dps);

    public string TotalDamageText => _totalDamage.ToString(CultureInfo.InvariantCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string FormatDps(double dps) =>
        dps % 1 == 0
            ? ((int)dps).ToString(CultureInfo.InvariantCulture)
            : dps.ToString("0.#", CultureInfo.InvariantCulture);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class DpsPanelViewModel : INotifyPropertyChanged
{
    private bool _isSolo;
    private string _soloLineText = "";
    private string _soloDamageText = "";
    private string _partyTimeText = "";
    private string _partyTotalText = "";
    private string _scopeLineText = "";
    private bool _showScopeLine;
    private string _cartCounterText = "";
    private bool _showCartCounter;
    private string _timeRemainingText = "";
    private bool _showTimeRemaining;
    private ObservableCollection<DpsEntryRowViewModel> _entries = new();

    public bool IsSolo
    {
        get => _isSolo;
        private set
        {
            if (SetField(ref _isSolo, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsParty)));
        }
    }

    public bool IsParty => !_isSolo;

    public string SoloLineText
    {
        get => _soloLineText;
        private set => SetField(ref _soloLineText, value);
    }

    public string PartyTotalText
    {
        get => _partyTotalText;
        private set => SetField(ref _partyTotalText, value);
    }

    public string ScopeLineText
    {
        get => _scopeLineText;
        private set => SetField(ref _scopeLineText, value);
    }

    public bool ShowScopeLine
    {
        get => _showScopeLine;
        private set => SetField(ref _showScopeLine, value);
    }

    public string SoloDamageText
    {
        get => _soloDamageText;
        private set => SetField(ref _soloDamageText, value);
    }

    public string PartyTimeText
    {
        get => _partyTimeText;
        private set => SetField(ref _partyTimeText, value);
    }

    public string CartCounterText
    {
        get => _cartCounterText;
        private set => SetField(ref _cartCounterText, value);
    }

    public bool ShowCartCounter
    {
        get => _showCartCounter;
        private set => SetField(ref _showCartCounter, value);
    }

    public string TimeRemainingText
    {
        get => _timeRemainingText;
        private set => SetField(ref _timeRemainingText, value);
    }

    public bool ShowTimeRemaining
    {
        get => _showTimeRemaining;
        private set => SetField(ref _showTimeRemaining, value);
    }

    public ObservableCollection<DpsEntryRowViewModel> Entries
    {
        get => _entries;
        private set => SetField(ref _entries, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyDto(DpsPanelDto dto)
    {
        int maxDeaths = Math.Max(0, dto.MaxDeaths);
        int deaths = maxDeaths > 0
            ? Math.Clamp(dto.Deaths, 0, maxDeaths)
            : Math.Max(0, dto.Deaths);
        ShowCartCounter = maxDeaths > 0;
        CartCounterText = ShowCartCounter ? $"猫车 {deaths}/{maxDeaths}" : "";
        ShowTimeRemaining = dto.QuestTimeRemainingSeconds is >= 0;
        TimeRemainingText = ShowTimeRemaining
            ? $"剩余 {FormatClock(dto.QuestTimeRemainingSeconds!.Value)}"
            : "";

        var source = (dto.Entries ?? Array.Empty<DpsEntryDto>())
            .OrderByDescending(e => e.TotalDamage)
            .Take(4)
            .ToList();

        long sum = source.Sum(e => e.TotalDamage);
        double ratioBase = sum <= 0 ? 1.0 : sum;
        string timeText = FormatHuntDuration(dto.HuntDurationSeconds);
        string timePrefix = string.IsNullOrEmpty(timeText) ? "" : $"{timeText} · ";

        if (source.Count <= 1)
        {
            IsSolo = true;
            var e = source.FirstOrDefault();
            SoloLineText = e is null
                ? $"{timePrefix}DPS 0"
                : $"{timePrefix}DPS {FormatDps(e.Dps)}";
            SoloDamageText = $"本怪 {FormatDamage(e?.TotalDamage ?? 0)}";

            PartyTimeText = "";
            PartyTotalText = "";
            ScopeLineText = "";
            ShowScopeLine = false;
            Entries.Clear();
            return;
        }

        IsSolo = false;
        SoloLineText = "";
        SoloDamageText = "";
        PartyTimeText = timeText;
        PartyTotalText = $"本怪 {FormatDamage(sum)}";
        ScopeLineText = "";
        ShowScopeLine = false;

        var unused = Entries.ToList();
        var rows = new List<DpsEntryRowViewModel>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            var e = source[i];
            DpsEntryRowViewModel? row = unused.FirstOrDefault(r =>
                r.IsSelf == e.IsSelf && string.Equals(r.Name, e.Name, StringComparison.Ordinal));
            if (row is not null)
                unused.Remove(row);
            row ??= new DpsEntryRowViewModel();
            row.Index = i + 1;
            row.Name = e.Name;
            row.IsSelf = e.IsSelf;
            row.Dps = e.Dps;
            row.TotalDamage = e.TotalDamage;
            row.ShareRatio = Math.Clamp(e.TotalDamage / ratioBase, 0, 1);
            rows.Add(row);
        }

        ObservableCollectionSync.Instances(Entries, rows);
    }

    public static DpsPanelViewModel CreateSampleSolo()
    {
        var vm = new DpsPanelViewModel();
        vm.ApplyDto(new DpsPanelDto(
        [
            new DpsEntryDto("我", true, 32, 8420),
        ],
        HuntDurationSeconds: 120,
        QuestTotalDamage: 8420,
        LockedTargetDamage: 8420,
        LockedTargetName: "千刃龙",
        Deaths: 0,
        MaxDeaths: 3));
        return vm;
    }

    public static DpsPanelViewModel CreateSampleParty4()
    {
        var vm = new DpsPanelViewModel();
        vm.ApplyDto(new DpsPanelDto(
        [
            new DpsEntryDto("我", true, 68, 8420),
            new DpsEntryDto("阿飞", false, 41, 5120),
            new DpsEntryDto("小南", false, 26, 3210),
            new DpsEntryDto("老", false, 18, 2190),
        ],
        HuntDurationSeconds: 124,
        QuestTotalDamage: 18940,
        LockedTargetDamage: 18940,
        LockedTargetName: "角龙",
        Deaths: 1,
        MaxDeaths: 3));
        return vm;
    }

    private static string FormatHuntDuration(double? seconds)
    {
        if (seconds is null or <= 0)
            return "";
        var t = TimeSpan.FromSeconds(seconds.Value);
        return t.TotalHours >= 1
            ? $"用时 {(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"用时 {t.Minutes}:{t.Seconds:D2}";
    }

    private static string FormatClock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    private static string FormatDps(double dps) =>
        dps % 1 == 0
            ? ((int)dps).ToString(CultureInfo.InvariantCulture)
            : dps.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatDamage(long damage)
        => damage.ToString(CultureInfo.InvariantCulture);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
