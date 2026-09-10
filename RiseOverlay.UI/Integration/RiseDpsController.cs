using HunterPie.Core.Client.Configuration.Enums;
using HunterPie.Core.Client.Configuration.Overlay;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Party;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Party;
using HunterPie.Core.Observability.Logging;
using HunterPie.UI.Overlay;
using RiseOverlay.Domain;
using RiseOverlay.UI.Overlay;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Compact DPS panel. Damage is scoped to the focused monster address (camera lock,
/// else quest marker). Scope sticks through unlock/death-anim so multi-monster quests
/// never fall back to AllTargets. Each monster uses its complete native damage counter.
/// </summary>
public sealed class RiseDpsController : IContextHandler, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Create();
    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly DamageMeterWidgetConfig _damageConfig;
    private readonly QuestCompletionClock _completionClock;
    private readonly Dictionary<IPartyMember, DpsMemberTiming> _timings = new();
    private readonly HashSet<IPartyMember> _members = new();
    private readonly HashSet<IMonster> _targetHooked = new();
    private IQuest? _hookedQuest;
    private int _deaths;
    private int _maxDeaths;
    private double? _questTimeRemainingSeconds;
    private double _timeElapsed;
    private bool _summaryFrozen;
    private DpsMemberSnapshot[]? _frozenSnapshots;
    private long? _frozenQuestTotal;
    private long? _frozenLockedDamage;
    private string? _frozenLockedName;

    /// <summary>Monster address whose damage we currently display (sticky across unlock flicker).</summary>
    private nint? _scopeAddress;
    private MHRMonster? _scopeMonster;
    private string? _scopeName;
    private Dictionary<int, long> _scopeDamageByEntity = new();
    private long _scopeTotal;
    private MHRMonster? _promotedTarget;

    public RiseDpsController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        DamageMeterWidgetConfig damageConfig,
        QuestCompletionClock completionClock)
    {
        _context = context;
        _viewModel = viewModel;
        _damageConfig = damageConfig;
        _completionClock = completionClock;
        _timeElapsed = context.Game.TimeElapsed;

        HookEvents();
        SyncExistingMembers();
        foreach (IMonster monster in _context.Game.Monsters)
            HookMonsterTarget(monster);
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
        _context.Game.OnMonsterSpawn += OnMonsterSpawn;
        _context.Game.OnMonsterDespawn += OnMonsterDespawn;
        HookQuest(_context.Game.Quest);
    }

    public void UnhookEvents()
    {
        _context.Game.Player.Party.OnMemberJoin -= OnMemberJoin;
        _context.Game.Player.Party.OnMemberLeave -= OnMemberLeave;
        _context.Game.OnTimeElapsedChange -= OnTimeElapsedChange;
        _context.Game.OnQuestStart -= OnQuestStart;
        _context.Game.OnQuestEnd -= OnQuestEnd;
        _context.Game.Player.OnStageUpdate -= OnStageUpdate;
        _context.Game.OnMonsterSpawn -= OnMonsterSpawn;
        _context.Game.OnMonsterDespawn -= OnMonsterDespawn;
        UnhookQuest(clearCounts: true);

        foreach (IMonster monster in _targetHooked.ToArray())
            UnhookMonsterTarget(monster);
        _targetHooked.Clear();

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

    private void OnMonsterSpawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            HookMonsterTarget(monster);
            if (!_summaryFrozen)
                PushPanel();
        });

    private void OnMonsterDespawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            UnhookMonsterTarget(monster);
            if (!_summaryFrozen)
                PushPanel();
        });

    private void HookMonsterTarget(IMonster monster)
    {
        if (!_targetHooked.Add(monster))
            return;
        monster.OnTargetChange += OnMonsterTargetChange;
    }

    private void UnhookMonsterTarget(IMonster monster)
    {
        if (!_targetHooked.Remove(monster))
            return;
        monster.OnTargetChange -= OnMonsterTargetChange;
    }

    private void OnMonsterTargetChange(object? sender, MonsterTargetEventArgs e)
    {
        if (_summaryFrozen)
            return;
        _viewModel.UIThread.BeginInvoke(() =>
        {
            if (sender is MHRMonster monster
                && (e.LockOnTarget == Target.Self || e.ManualTarget == Target.Self))
                _promotedTarget = monster;
            PushPanel();
        });
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

        const double precision = 0.5;
        double lastBucket = _timeElapsed % precision;
        double newBucket = e.TimeElapsed % precision;
        // Match HunterPie's original meter: the game quest timer is authoritative.
        // Monster HP/list state must never pause or offset this clock.
        _timeElapsed = ScopedDamageRules.ResolveQuestElapsed(e.TimeElapsed);

        if (!e.IsTimerReset && newBucket >= lastBucket)
            return;

        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void OnQuestStart(object? sender, IQuest e)
    {
        HookQuest(e);
        _viewModel.UIThread.BeginInvoke(() =>
        {
            _summaryFrozen = false;
            _frozenSnapshots = null;
            _frozenQuestTotal = null;
            _frozenLockedDamage = null;
            _frozenLockedName = null;
            ClearDamageScope();
            ResetMembers();
            _timeElapsed = _context.Game.TimeElapsed;
            PushPanel();
        });
    }

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
    {
        // Quest-id refresh uses Status.None — must NOT freeze or live DPS dies mid-hunt.
        if (e.Status is QuestStatus.None)
            return;

        _deaths = Math.Max(0, e.Quest.Deaths);
        _maxDeaths = Math.Max(0, e.Quest.MaxDeaths);
        UpdateQuestTimeRemaining(e.Quest.TimeLeft);
        UnhookQuest(clearCounts: false);

        double stateElapsed = Math.Max(1, e.TimeElapsed.TotalSeconds);
        double elapsed = _completionClock.ResolveResultElapsed(
            succeeded: e.Status is QuestStatus.Success,
            fallbackElapsed: stateElapsed);
        // Resolve the result time before calculating DPS. Otherwise the card can show the
        // objective time while its DPS rows are still divided by the later state time.
        DpsMemberSnapshot[] captured = CaptureSnapshots(elapsed);
        long targetTotal = captured.Sum(s => s.TotalDamage);
        Logger.Info(
            $"Rise DPS result timing: status={e.Status}, result={elapsed:0.00}, objective={_completionClock.ConfirmedElapsed?.ToString("0.00") ?? "none"}, state={stateElapsed:0.00}, drift={stateElapsed - elapsed:0.00}");

        _viewModel.UIThread.BeginInvoke(() =>
        {
            // Only freeze the result card while still on the hunt map.
            // Village accept/depart flicker must not lock the panel at 0.
            if (!IsOnHuntMap())
            {
                _summaryFrozen = false;
                _frozenSnapshots = null;
                _frozenQuestTotal = null;
                _frozenLockedDamage = null;
                _frozenLockedName = null;
                ClearDamageScope();
                ResetMembers();
                _timeElapsed = 0;
                _deaths = 0;
                _maxDeaths = 0;
                _questTimeRemainingSeconds = null;
                PushPanel();
                return;
            }

            _summaryFrozen = true;
            // Original HunterPie overwrites the result card with QuestEndEventArgs.TimeElapsed.
            _timeElapsed = ScopedDamageRules.ResolveQuestElapsed(elapsed);
            _frozenSnapshots = captured.Length > 0 ? captured : null;
            _frozenQuestTotal = targetTotal > 0 ? targetTotal : null;
            _frozenLockedDamage = targetTotal > 0 ? targetTotal : null;
            _frozenLockedName = _scopeName;
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
                _frozenQuestTotal = null;
                _frozenLockedDamage = null;
                _frozenLockedName = null;
                ClearDamageScope();
                ResetMembers();
                _timeElapsed = 0;
                _deaths = 0;
                _maxDeaths = 0;
                _questTimeRemainingSeconds = null;
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
                ClearDamageScope();
                _deaths = 0;
                _maxDeaths = 0;
                _questTimeRemainingSeconds = null;
            }

            PushPanel();
        });

    private bool IsOnHuntMap()
        => _context.Game.Player.InHuntingZone || _context.Game.Player.StageId == 5;

    private void TryAttachMember(IPartyMember member)
    {
        if (member.Type is not (MemberType.Player or MemberType.Companion))
            return;

        if (!_members.Add(member))
            return;

        double now = _timeElapsed;
        _timings[member] = new DpsMemberTiming
        {
            JoinedAt = now,
            FirstHitAt = -1,
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

        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void HookQuest(IQuest? quest)
    {
        if (ReferenceEquals(_hookedQuest, quest))
        {
            if (quest is not null)
            {
                _deaths = Math.Max(0, quest.Deaths);
                _maxDeaths = Math.Max(0, quest.MaxDeaths);
                UpdateQuestTimeRemaining(quest.TimeLeft);
            }
            return;
        }

        UnhookQuest(clearCounts: false);
        _hookedQuest = quest;
        if (quest is null)
        {
            _deaths = 0;
            _maxDeaths = 0;
            _questTimeRemainingSeconds = null;
            return;
        }

        quest.OnDeathCounterChange += OnDeathCounterChange;
        quest.OnTimeLeftChange += OnQuestTimeLeftChange;
        _deaths = Math.Max(0, quest.Deaths);
        _maxDeaths = Math.Max(0, quest.MaxDeaths);
        _questTimeRemainingSeconds = null;
        UpdateQuestTimeRemaining(quest.TimeLeft);
    }

    private void UnhookQuest(bool clearCounts)
    {
        if (_hookedQuest is not null)
        {
            _hookedQuest.OnDeathCounterChange -= OnDeathCounterChange;
            _hookedQuest.OnTimeLeftChange -= OnQuestTimeLeftChange;
        }
        _hookedQuest = null;

        if (!clearCounts)
            return;
        _deaths = 0;
        _maxDeaths = 0;
        _questTimeRemainingSeconds = null;
    }

    private void OnDeathCounterChange(object? sender, CounterChangeEventArgs e)
    {
        _deaths = Math.Max(0, e.Current);
        _maxDeaths = Math.Max(0, e.Max);
        _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void OnQuestTimeLeftChange(object? sender, SimpleValueChangeEventArgs<TimeSpan> e)
    {
        int previousSecond = _questTimeRemainingSeconds is { } previous
            ? (int)Math.Ceiling(previous)
            : -1;
        UpdateQuestTimeRemaining(e.NewValue);
        int currentSecond = _questTimeRemainingSeconds is { } current
            ? (int)Math.Ceiling(current)
            : -1;
        if (currentSecond != previousSecond)
            _viewModel.UIThread.BeginInvoke(PushPanel);
    }

    private void UpdateQuestTimeRemaining(TimeSpan timeLeft)
    {
        double seconds = timeLeft.TotalSeconds;
        if (seconds > 0)
            _questTimeRemainingSeconds = seconds;
        else if (_questTimeRemainingSeconds is not null)
            _questTimeRemainingSeconds = 0;
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
            timing = new DpsMemberTiming
            {
                JoinedAt = _timeElapsed,
                FirstHitAt = -1,
            };
            _timings[member] = timing;
        }

        return timing;
    }

    private void PushPanel()
    {
        // MHRQuest learns MaxDeaths on its first scan, after OnQuestStart. MaxDeaths has no
        // change event when Deaths is still 0, so poll the lightweight quest properties here.
        if (_hookedQuest is { } quest)
        {
            _deaths = Math.Max(0, quest.Deaths);
            _maxDeaths = Math.Max(0, quest.MaxDeaths);
            UpdateQuestTimeRemaining(quest.TimeLeft);
        }

        if (_summaryFrozen && _frozenSnapshots is { Length: > 0 })
        {
            _viewModel.DpsPanel.ApplyDto(DpsPanelMapper.FromSnapshots(
                _frozenSnapshots,
                HuntDuration(),
                _frozenQuestTotal,
                _frozenLockedDamage,
                _frozenLockedName,
                deaths: _deaths,
                maxDeaths: _maxDeaths,
                questTimeRemainingSeconds: _questTimeRemainingSeconds));
            return;
        }

        DpsMemberSnapshot[] snapshots = CaptureSnapshots();
        long panelTotal = snapshots.Sum(s => s.TotalDamage);

        _viewModel.DpsPanel.ApplyDto(DpsPanelMapper.FromSnapshots(
            snapshots,
            HuntDuration(),
            panelTotal > 0 ? panelTotal : null,
            _scopeAddress is not null ? panelTotal : null,
            _scopeName,
            deaths: _deaths,
            maxDeaths: _maxDeaths,
            questTimeRemainingSeconds: _questTimeRemainingSeconds));
    }

    private void ClearDamageScope()
    {
        _scopeAddress = null;
        _scopeMonster = null;
        _scopeName = null;
        _scopeDamageByEntity = new();
        _scopeTotal = 0;
        _promotedTarget = null;
    }

    /// <summary>
    /// Resolve which monster damage to show. Prefer camera lock, then quest marker.
    /// Keep the last scope while that instance is still in the monster list (0 HP / unlock).
    /// </summary>
    private void RefreshDamageScope()
    {
        MHRMonster? focused = FindFocusedMonster(_promotedTarget);
        _promotedTarget = null;
        if (focused is not null)
        {
            if (_scopeAddress != focused.Address || !ReferenceEquals(_scopeMonster, focused))
            {
                _scopeAddress = focused.Address;
                _scopeMonster = focused;
                _scopeName = focused.Name;
                _scopeDamageByEntity = new();
                _scopeTotal = 0;
            }
            else
            {
                _scopeName = focused.Name;
            }

            PullScopeDamage(focused.Address);
            return;
        }

        // Unlock / death flicker: keep scoping to the same instance while it remains listed.
        if (_scopeAddress is nint addr
            && _context.Game.Monsters.OfType<MHRMonster>().Any(m => m.Address == addr))
        {
            PullScopeDamage(addr);
            return;
        }

        // Instance despawned — keep last snapshot numbers until a new focus (do not use AllTargets).
    }

    private void PullScopeDamage(nint address)
    {
        if (_context.Game is not MHRGame game)
            return;

        if (!game.TryGetDamageSnapshot(address, out Dictionary<int, long> byIndex, out long total))
            return;

        _scopeDamageByEntity = byIndex;
        _scopeTotal = total;
    }

    private MHRMonster? FindFocusedMonster(MHRMonster? promoted)
    {
        MHRMonster[] monsters = _context.Game.Monsters.OfType<MHRMonster>().ToArray();
        MHRMonster? lockOn = TargetSelectionRules.Select(
            monsters,
            _scopeMonster,
            promoted,
            monster => monster.Target == Target.Self);
        if (lockOn is not null)
            return lockOn;

        return TargetSelectionRules.Select(
            monsters,
            _scopeMonster,
            promoted,
            monster => monster.ManualTarget == Target.Self);
    }

    private DpsMemberSnapshot[] CaptureSnapshots(double? questElapsedOverride = null)
    {
        // Party objects are recreated by the scanner; always re-bind from live party.
        foreach (IPartyMember member in _context.Game.Player.Party.Members)
            TryAttachMember(member);

        RefreshDamageScope();
        bool useScope = _scopeAddress is not null;
        Dictionary<int, long> scopedDamage = useScope
            ? ScopedDamageRules.UseFullTargetSnapshot(_scopeDamageByEntity)
            : new Dictionary<int, long>();
        long scopedTotal = useScope ? Math.Max(0, _scopeTotal) : 0;

        double calculationElapsed = questElapsedOverride ?? _timeElapsed;
        return _members
            .Select(m =>
            {
                long total;
                if (useScope)
                {
                    total = scopedDamage.GetValueOrDefault(ResolveEntityIndex(m), 0);
                    // Solo / index miss: if party row is empty but we know scope total, attribute to self.
                    if (total <= 0 && m.IsMyself && scopedTotal > 0 && scopedDamage.Count == 0)
                        total = scopedTotal;
                }
                else
                {
                    // No focus yet this hunt — show 0 rather than misleading quest-wide sum.
                    total = 0;
                }

                DpsMemberTiming timing = GetTiming(m);
                timing.FirstHitAt = ScopedDamageRules.ResolveFirstHitAt(
                    timing.FirstHitAt,
                    total,
                    calculationElapsed);
                DpsCalculationMode mode = _damageConfig.DpsCalculationStrategy.Value switch
                {
                    DPSCalculationStrategy.RelativeToQuest => DpsCalculationMode.RelativeToQuest,
                    DPSCalculationStrategy.RelativeToJoin => DpsCalculationMode.RelativeToJoin,
                    DPSCalculationStrategy.RelativeToFirstHit => DpsCalculationMode.RelativeToFirstHit,
                    _ => DpsCalculationMode.RelativeToJoin,
                };

                double dps = ScopedDamageRules.CalculateDps(
                    scopedDamage: total,
                    questElapsed: calculationElapsed,
                    joinedAt: timing.JoinedAt,
                    firstHitAt: timing.FirstHitAt,
                    mode);
                return new DpsMemberSnapshot(m.Name, m.IsMyself, total, dps);
            })
            .ToArray();
    }

    private static int ResolveEntityIndex(IPartyMember member)
        => member is MHRPartyMember mhr ? mhr.EntityIndex : member.Slot;

    private double? HuntDuration()
        => ScopedDamageRules.HuntDuration(_timeElapsed);

    private sealed class DpsMemberTiming
    {
        public double JoinedAt { get; set; }
        public double FirstHitAt { get; set; }
    }
}
