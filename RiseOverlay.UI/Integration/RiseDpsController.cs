using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
using HunterPie.UI.Overlay;
using RiseOverlay.Domain;
using RiseOverlay.UI.Overlay;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Compact DPS panel. Reads <see cref="IPartyMember.Damage"/> only
/// (filled by upstream MHRGame native IPC → party). Display/freeze only.
/// </summary>
public sealed class RiseDpsController : IContextHandler, IDisposable
{
    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly DamageMeterWidgetConfig _damageConfig;
    private readonly Dictionary<IPartyMember, DpsMemberTiming> _timings = new();
    private readonly HashSet<IPartyMember> _members = new();
    private double _timeElapsed;
    private bool _summaryFrozen;
    private DpsMemberSnapshot[]? _frozenSnapshots;

    public RiseDpsController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        DamageMeterWidgetConfig damageConfig)
    {
        _context = context;
        _viewModel = viewModel;
        _damageConfig = damageConfig;
        _timeElapsed = context.Game.TimeElapsed;

        HookEvents();
        SyncExistingMembers();
        PushPanel();
    }

    public void HookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin += OnMemberJoin;
        _context.Game.Player.Party.OnMemberLeave += OnMemberLeave;
        _context.Game.OnTimeElapsedChange += OnTimeElapsedChange;
        _context.Game.OnQuestStart += OnQuestStart;
        _context.Game.OnQuestEnd += OnQuestEnd;
        _context.Game.Player.OnStageUpdate += OnStageUpdate;
    }

    public void UnhookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin -= OnMemberJoin;
        _context.Game.Player.Party.OnMemberLeave -= OnMemberLeave;
        _context.Game.OnTimeElapsedChange -= OnTimeElapsedChange;
        _context.Game.OnQuestStart -= OnQuestStart;
        _context.Game.OnQuestEnd -= OnQuestEnd;
        _context.Game.Player.OnStageUpdate -= OnStageUpdate;

        foreach (IPartyMember member in _members.ToArray())
            DetachMember(member);
        _members.Clear();
        _timings.Clear();
    }

    public void Dispose() => UnhookEvents();

    private void SyncExistingMembers()
    {
        foreach (IPartyMember member in _context.Game.Player.Party.Members)
            TryAttachMember(member);
    }

    private void OnMemberJoin(object? sender, IPartyMember e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            TryAttachMember(e);
            if (!_summaryFrozen)
                PushPanel();
        });

    private void OnMemberLeave(object? sender, IPartyMember e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            DetachMember(e);
            if (!_summaryFrozen)
                PushPanel();
        });

    private void OnTimeElapsedChange(object? sender, TimeElapsedChangeEventArgs e)
    {
        if (_summaryFrozen)
            return;

        // Game clear time stops when the last large target dies; memory QUEST_TIMER often
        // keeps ticking through cart / result transition (+few seconds). Hold the clock once
        // no alive large monsters remain on the hunt map.
        if (ShouldHoldElapsedClock())
        {
            _viewModel.UIThread.BeginInvoke(PushPanel);
            return;
        }

        const double precision = 0.5;
        double lastBucket = _timeElapsed % precision;
        double newBucket = e.TimeElapsed % precision;
        _timeElapsed = e.TimeElapsed;

        if (!e.IsTimerReset && newBucket >= lastBucket)
            return;

        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void OnQuestStart(object? sender, IQuest e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            _summaryFrozen = false;
            _frozenSnapshots = null;
            ResetMembers();
            _timeElapsed = _context.Game.TimeElapsed;
            PushPanel();
        });

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
    {
        // Quest-id refresh uses Status.None — must NOT freeze or live DPS dies mid-hunt.
        if (e.Status is QuestStatus.None)
            return;

        DpsMemberSnapshot[] captured = CaptureSnapshots();
        double elapsed = Math.Max(1, e.TimeElapsed.TotalSeconds);

        _viewModel.UIThread.BeginInvoke(() =>
        {
            // Only freeze the result card while still on the hunt map.
            // Village accept/depart flicker must not lock the panel at 0.
            if (!IsOnHuntMap())
            {
                _summaryFrozen = false;
                _frozenSnapshots = null;
                ResetMembers();
                _timeElapsed = 0;
                PushPanel();
                return;
            }

            _summaryFrozen = true;
            // Prefer the held combat clock (last target death) over quest-end TimeElapsed,
            // which usually includes a few extra seconds of cart/result padding.
            if (_timeElapsed <= 0)
                _timeElapsed = elapsed;
            _frozenSnapshots = captured.Length > 0 ? captured : null;
            PushPanel();
        });
    }

    private void OnStageUpdate(object? sender, EventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_summaryFrozen && !IsOnHuntMap() && _context.Game.Quest is null)
            {
                _summaryFrozen = false;
                _frozenSnapshots = null;
                ResetMembers();
                _timeElapsed = 0;
                PushPanel();
                return;
            }

            if (_summaryFrozen)
            {
                PushPanel();
                return;
            }

            if (_context.Game.Quest is null)
            {
                ResetMembers();
                _timeElapsed = _context.Game.TimeElapsed;
            }

            PushPanel();
        });

    private bool IsOnHuntMap()
        => _context.Game.Player.InHuntingZone || _context.Game.Player.StageId == 5;

    /// <summary>
    /// True when the hunt map has no remaining large monsters with HP — clear usually already met.
    /// </summary>
    private bool ShouldHoldElapsedClock()
    {
        if (!IsOnHuntMap() || _context.Game.Quest is null)
            return false;

        return !_context.Game.Monsters.Any(m =>
            m.MaxHealth > 0
            && MonsterHealthDisplay.ForHud(m.Health, m.MaxHealth) > 0);
    }

    private void TryAttachMember(IPartyMember member)
    {
        if (member.Type is not (MemberType.Player or MemberType.Companion))
            return;

        if (!_members.Add(member))
            return;

        double now = _context.Game.TimeElapsed;
        _timings[member] = new DpsMemberTiming
        {
            JoinedAt = now,
            FirstHitAt = member.Damage > 0 ? now : -1,
            LastDamage = member.Damage,
        };
        member.OnDamageDealt += OnMemberDamageDealt;
    }

    private void DetachMember(IPartyMember member)
    {
        if (!_members.Remove(member))
            return;

        member.OnDamageDealt -= OnMemberDamageDealt;
        _timings.Remove(member);
    }

    private void OnMemberDamageDealt(object? sender, IPartyMember e)
    {
        if (_summaryFrozen)
            return;

        if (_timings.TryGetValue(e, out DpsMemberTiming? timing))
        {
            if (e.Damage > 0 && timing.LastDamage <= 0)
                timing.FirstHitAt = _context.Game.TimeElapsed;
            timing.LastDamage = e.Damage;
        }

        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void ResetMembers()
    {
        foreach (IPartyMember member in _members.ToArray())
            DetachMember(member);

        _members.Clear();
        _timings.Clear();
        SyncExistingMembers();
    }

    private DpsMemberTiming GetTiming(IPartyMember member)
    {
        if (!_timings.TryGetValue(member, out DpsMemberTiming? timing))
        {
            double now = _context.Game.TimeElapsed;
            timing = new DpsMemberTiming
            {
                JoinedAt = now,
                FirstHitAt = member.Damage > 0 ? now : -1,
                LastDamage = member.Damage,
            };
            _timings[member] = timing;
        }

        return timing;
    }

    private void PushPanel()
    {
        if (_summaryFrozen && _frozenSnapshots is { Length: > 0 })
        {
            _viewModel.DpsPanel.ApplyDto(DpsPanelMapper.FromSnapshots(_frozenSnapshots, HuntDuration()));
            return;
        }

        _viewModel.DpsPanel.ApplyDto(DpsPanelMapper.FromSnapshots(CaptureSnapshots(), HuntDuration()));
    }

    private DpsMemberSnapshot[] CaptureSnapshots()
    {
        // Party objects are recreated by the scanner; always re-bind from live party.
        foreach (IPartyMember member in _context.Game.Player.Party.Members)
            TryAttachMember(member);

        return _members
            .Select(m =>
            {
                long total = m.Damage;
                DpsMemberTiming timing = GetTiming(m);
                if (total > 0 && timing.LastDamage <= 0)
                    timing.FirstHitAt = _context.Game.TimeElapsed;
                timing.LastDamage = total;
                DpsCalculationMode mode = _damageConfig.DpsCalculationStrategy.Value switch
                {
                    DPSCalculationStrategy.RelativeToQuest => DpsCalculationMode.RelativeToQuest,
                    DPSCalculationStrategy.RelativeToJoin => DpsCalculationMode.RelativeToJoin,
                    DPSCalculationStrategy.RelativeToFirstHit => DpsCalculationMode.RelativeToFirstHit,
                    _ => DpsCalculationMode.RelativeToJoin,
                };
                double dps = OriginalDpsCalculator.Calculate(
                    totalDamage: total,
                    questElapsed: _timeElapsed,
                    joinedAt: timing.JoinedAt,
                    firstHitAt: timing.FirstHitAt,
                    mode);
                return new DpsMemberSnapshot(m.Name, m.IsMyself, total, dps);
            })
            .ToArray();
    }

    private double? HuntDuration()
        => _timeElapsed > 0 ? _timeElapsed : null;

    private sealed class DpsMemberTiming
    {
        public double JoinedAt { get; init; }
        public double FirstHitAt { get; set; }
        public long LastDamage { get; set; }
    }
}
