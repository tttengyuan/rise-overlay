using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;
using HunterPie.UI.Overlay;
using RiseOverlay.Data;
using RiseOverlay.Domain;
using RiseOverlay.UI.Overlay;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Wires Rise <see cref="IMonster"/> spawn/HP/parts/ailments/enrage into compact <see cref="MonsterHudViewModel"/>.
/// Types: <see cref="IMonster"/> (HunterPie.Core), Rise impl <c>MHRMonster</c>, quest via <see cref="IGame.Quest"/> / <see cref="IQuest.Type"/>.
/// </summary>
public sealed class RiseMonsterHudController : IContextHandler, IDisposable
{
    private const double TrustedCompletionLeadSeconds = 2.0;
    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly MonsterStaticStore _staticStore;
    private readonly QuestStaticStore _questStore;
    private readonly Func<string, string> _localizePart;
    private readonly Dictionary<IMonster, MonsterBinding> _bindings = new();
    private IMonster? _active;
    private bool _hudFrozen;
    private MonsterHudDto? _frozenHudDto;
    /// <summary>Last in-combat HUD frame — used when quest end already wiped memory HP to 0.</summary>
    private MonsterHudDto? _lastLiveHudDto;
    private IMonster? _lastLiveMonster;
    private bool _questFailed;
    private double? _completionFreezeElapsed;

    private bool IsOnHuntMap()
        => _context.Game.Player.InHuntingZone || _context.Game.Player.StageId == 5;

    public RiseMonsterHudController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        MonsterStaticStore staticStore,
        Func<string, string>? localizePart = null,
        QuestStaticStore? questStore = null)
    {
        _context = context;
        _viewModel = viewModel;
        _staticStore = staticStore;
        _questStore = questStore ?? QuestStaticStore.LoadEmpty();
        _localizePart = localizePart ?? (id => id);

        HookEvents();
        SyncExistingMonsters();
        RefreshActiveMonster();
    }

    public void HookEvents()
    {
        _context.Game.OnMonsterSpawn += OnMonsterSpawn;
        _context.Game.OnMonsterDespawn += OnMonsterDespawn;
        _context.Game.OnQuestStart += OnQuestChanged;
        _context.Game.OnQuestEnd += OnQuestEnded;
        _context.Game.Player.OnStageUpdate += OnStageUpdate;
    }

    public void UnhookEvents()
    {
        _context.Game.OnMonsterSpawn -= OnMonsterSpawn;
        _context.Game.OnMonsterDespawn -= OnMonsterDespawn;
        _context.Game.OnQuestStart -= OnQuestChanged;
        _context.Game.OnQuestEnd -= OnQuestEnded;
        _context.Game.Player.OnStageUpdate -= OnStageUpdate;

        foreach (var binding in _bindings.Values)
            binding.Dispose();
        _bindings.Clear();
        _active = null;
        _viewModel.UIThread.BeginInvoke(() => _viewModel.HasActiveMonster = false);
    }

    public void Dispose() => UnhookEvents();

    private void SyncExistingMonsters()
    {
        foreach (IMonster monster in _context.Game.Monsters)
            AttachMonster(monster);
    }

    private void OnMonsterSpawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            AttachMonster(monster);
            RefreshActiveMonster();
        });

    private void OnMonsterDespawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_bindings.Remove(monster, out var binding))
                binding.Dispose();
            if (ReferenceEquals(_active, monster))
                _active = null;
            RefreshActiveMonster();
        });

    private void OnQuestChanged(object? sender, IQuest e)
    {
        _questFailed = false;
        _completionFreezeElapsed = null;
        _viewModel.UIThread.BeginInvoke(() =>
        {
            ClearHudFreeze();
            _lastLiveHudDto = null;
            _lastLiveMonster = null;
            _active = null;
            // Drop any leftover bindings from the previous hunt so Qurio part state
            // cannot leak into the next quest's first lock-on frame.
            foreach (var binding in _bindings.Values)
                binding.Dispose();
            _bindings.Clear();
            foreach (IMonster monster in _context.Game.Monsters)
                AttachMonster(monster);
            ApplyResetHudForQuest(e);
            RefreshActiveMonster();
        });
    }

    private void OnQuestEnded(object? sender, QuestEndEventArgs e)
    {
        _questFailed = e.Status is QuestStatus.Fail;

        // Failure teardown can zero every monster component and emit a false OnDeath.
        // A failed quest did not kill the monster, so prefer the last positive combat frame.
        bool hasEarlierTrustedCompletion = _completionFreezeElapsed is { } completedAt
            && _frozenHudDto?.CompletionState is MonsterCompletionState.Slain or MonsterCompletionState.Captured
            && e.TimeElapsed.TotalSeconds - completedAt >= TrustedCompletionLeadSeconds;
        MonsterHudDto? captured = _questFailed
            ? _lastLiveHudDto is not null || _frozenHudDto is not null
                ? MonsterHudMapper.ToQuestFailed(
                    _lastLiveHudDto,
                    _frozenHudDto,
                    preferFrozenCompletion: hasEarlierTrustedCompletion)
                : null
            : ChooseFreezeSnapshot(liveNow: null);
        if (captured is not null
            && e.Status is QuestStatus.Success
            && e.Quest.Type is QuestType.Hunt or QuestType.Slay or QuestType.Capture)
        {
            // The quest state can flip to Success before the final monster health scan.
            // Treat successful monster-quest completion as authoritative as well.
            captured = e.Quest.Type switch
            {
                QuestType.Capture => MonsterHudMapper.ToCaptured(captured),
                QuestType.Slay => MonsterHudMapper.ToCompleted(captured),
                _ => MonsterHudMapper.ToQuestCompleted(captured),
            };
        }

        _viewModel.UIThread.BeginInvoke(() =>
        {
            if (IsOnHuntMap())
            {
                _hudFrozen = true;
                if (captured is not null)
                    _frozenHudDto = captured;
                ApplyHudPresentation();
                return;
            }

            ClearHudFreeze();
            RefreshActiveMonster();
        });
    }

    private void OnStageUpdate(object? sender, EventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            // Always drop the post-hunt freeze when leaving the hunting zone.
            // Do not wait for Quest==null — accepting the next quest in the hub while
            // freeze is still held used to keep showing the previous monster's panel.
            if (_hudFrozen && !IsOnHuntMap())
            {
                ClearHudFreeze();
                _lastLiveHudDto = null;
                _lastLiveMonster = null;
                _active = null;
                _questFailed = false;
                _completionFreezeElapsed = null;
                RefreshActiveMonster();
            }
        });

    private void AttachMonster(IMonster monster)
    {
        if (_bindings.ContainsKey(monster))
            return;

        var binding = new MonsterBinding(
            monster,
            OnMonsterDataChanged,
            OnMonsterTargetChanged,
            OnMonsterFinished);
        _bindings[monster] = binding;
    }

    private void OnMonsterFinished(IMonster monster, MonsterCompletionState completion)
    {
        double observedAt = _context.Game.TimeElapsed;
        _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_questFailed || _context.Game.Quest?.Status is QuestStatus.Fail)
                return;

            // Death/capture is more authoritative than the health component. In Rise the
            // latter can remain slightly above zero throughout the finish animation.
            if (!ReferenceEquals(_active, monster))
                return;

            MonsterHudDto? lastCombatFrame = ReferenceEquals(_lastLiveMonster, monster)
                ? _lastLiveHudDto
                : null;
            if (lastCombatFrame is null)
                return;

            FreezeOnActiveCompletion(lastCombatFrame, completion, observedAt);
        });
    }

    private void OnMonsterTargetChanged(IMonster monster, MonsterTargetEventArgs target)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            // Every MHR monster reads the same lock-on address independently. During a switch,
            // one scan can briefly leave both the old and new instances at Target.Self.
            // The instance whose event just became Self is authoritative for this transition.
            IMonster? promoted = target.LockOnTarget == Target.Self ? monster : null;
            IMonster? next = SelectAliveLockedMonster(promoted);

            if (_hudFrozen)
            {
                if (_context.Game.Quest is not null
                    && next is not null
                    && !ReferenceEquals(next, _active))
                {
                    ClearHudFreeze();
                    _active = next;
                    _viewModel.HasActiveMonster = true;
                    PushActiveHud();
                    return;
                }

                ApplyHudPresentation();
                return;
            }

            _active = next;
            _viewModel.HasActiveMonster = next is not null;
            PushActiveHud();
        });

    private void OnMonsterDataChanged(IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_hudFrozen)
            {
                // Multi-target hunts: unlock only when the player locks onto another *alive* monster.
                // Map invaders briefly stealing Target after a kill must not replace the hold.
                if (_context.Game.Quest is not null)
                {
                    IMonster? lockedAlive = SelectAliveLockedMonster();
                    if (lockedAlive is not null && !ReferenceEquals(lockedAlive, _active))
                    {
                        ClearHudFreeze();
                        _active = lockedAlive;
                        _viewModel.HasActiveMonster = true;
                        PushActiveHud();
                        return;
                    }
                }

                ApplyHudPresentation();
                return;
            }

            if (!ReferenceEquals(_active, monster))
            {
                // HP/part updates from non-target monsters must never arbitrate selection.
                // Only refresh when the current target is no longer valid; target events
                // themselves promote the newly locked monster explicitly.
                if (_active is null || !IsAliveLocked(_active))
                    RefreshActiveMonster();
                return;
            }

            PushActiveHud();
        });

    private void RefreshActiveMonster()
    {
        if (_hudFrozen)
        {
            ApplyHudPresentation();
            return;
        }

        IMonster? next = SelectActiveMonster();
        _active = next;
        _viewModel.HasActiveMonster = next is not null;
        PushActiveHud();
    }

    private IMonster? SelectActiveMonster()
    {
        if (_hudFrozen && _active is not null)
            return _active;

        // Only real camera lock-on (Target.Self) among alive monsters.
        // Never fall back to "any alive map monster" — that swaps the panel to invasions
        // the moment the hunted target dies.
        return SelectAliveLockedMonster();
    }

    private IMonster? SelectAliveLockedMonster(IMonster? promoted = null)
        => TargetSelectionRules.Select(
            _bindings.Keys,
            _active,
            promoted,
            IsAliveLocked);

    private static bool IsAliveLocked(IMonster monster)
        => monster.Target == Target.Self
           && monster.MaxHealth > 0
           && MonsterHealthDisplay.ForHud(monster.Health, monster.MaxHealth) > 0;

    private void ApplyHudPresentation()
    {
        if (_hudFrozen && _frozenHudDto is not null)
        {
            _viewModel.MonsterHud.ApplyDto(_frozenHudDto);
            _viewModel.HasActiveMonster = true;
            return;
        }

        PushActiveHud();
    }

    private void ClearHudFreeze()
    {
        _hudFrozen = false;
        _frozenHudDto = null;
        _completionFreezeElapsed = null;
    }

    /// <summary>
    /// Prefer the pre-death combat frame. Live quest-end memory is often already 0 HP
    /// or points at an unrelated map monster.
    /// </summary>
    private MonsterHudDto? ChooseFreezeSnapshot(MonsterHudDto? liveNow)
    {
        if (_frozenHudDto is not null)
            return _frozenHudDto;

        if (_lastLiveHudDto is { HealthCurrent: > 0 })
            return _lastLiveHudDto;

        if (liveNow is { HealthCurrent: > 0 })
            return liveNow;

        return _lastLiveHudDto ?? liveNow;
    }

    private void FreezeOnActiveCompletion(
        MonsterHudDto lastAliveFrame,
        MonsterCompletionState completion = MonsterCompletionState.Slain,
        double? observedAt = null)
    {
        if (_frozenHudDto is { } existing
            && !MonsterCompletionRules.ShouldApplyMonsterFinish(existing.CompletionState, completion))
        {
            ApplyHudPresentation();
            return;
        }

        _hudFrozen = true;
        _completionFreezeElapsed ??= observedAt ?? _context.Game.TimeElapsed;
        _frozenHudDto = completion == MonsterCompletionState.Captured
            ? MonsterHudMapper.ToCaptured(lastAliveFrame)
            : MonsterHudMapper.ToCompleted(lastAliveFrame);
        ApplyHudPresentation();
    }

    private void ApplyResetHudForQuest(IQuest quest)
    {
        MonsterStaticMapped? mapped = ResolvePrimaryTargetStatic(quest);
        bool questAllows = MonsterLiveAdapter.ResolveQuestAllowsCapture(
            quest.Type.ToString(),
            quest.Level.ToString());

        MonsterHudDto dto = mapped is not null
            ? MonsterHudMapper.ToResetHud(mapped, questAllows)
            : MonsterHudMapper.ToEmptyHud();

        _viewModel.MonsterHud.ApplyDto(dto);
        _viewModel.HasActiveMonster = false;
    }

    private MonsterStaticMapped? ResolvePrimaryTargetStatic(IQuest quest)
    {
        foreach (string monsterRef in quest.BriefingMonsterIds)
        {
            MonsterStaticMapped? mapped = MapStaticRef(monsterRef);
            if (mapped is not null)
                return mapped;
        }

        QuestStaticDto? staticQuest = _questStore.FindById(quest.Id);
        if (staticQuest is not null)
        {
            foreach (string monsterRef in staticQuest.Monsters)
            {
                MonsterStaticMapped? mapped = MapStaticRef(monsterRef);
                if (mapped is not null)
                    return mapped;
            }
        }

        return null;
    }

    private MonsterStaticMapped? MapStaticRef(string monsterRef)
    {
        MonsterStaticDto? dto = _staticStore.FindById(monsterRef);
        return dto is null
            ? null
            : MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));
    }

    private void PushActiveHud()
    {
        if (_active is null)
        {
            _viewModel.HasActiveMonster = false;
            return;
        }

        var staticMapped = ResolveStatic(_active);
        var fixture = BuildFixture(_active);
        var live = MonsterLiveAdapter.ToSnapshot(fixture);
        var dto = MonsterHudMapper.MergeLive(staticMapped, live);

        // Target just died: freeze the last non-zero combat frame immediately so the panel
        // cannot jump to a full-HP invader that briefly becomes Target.Self.
        if (dto.HealthCurrent <= 0
            && _lastLiveHudDto is { HealthCurrent: > 0 } lastAlive
            && ReferenceEquals(_lastLiveMonster, _active))
        {
            if (_questFailed || _context.Game.Quest?.Status is QuestStatus.Fail)
                return;
            FreezeOnActiveCompletion(lastAlive);
            return;
        }

        if (dto.HealthCurrent > 0)
        {
            _lastLiveHudDto = dto;
            _lastLiveMonster = _active;
        }

        _viewModel.MonsterHud.ApplyDto(dto);
        _viewModel.HasActiveMonster = true;
    }

    private MonsterStaticMapped ResolveStatic(IMonster monster)
    {
        MonsterStaticDto? dto = null;
        if (!string.IsNullOrWhiteSpace(monster.Name))
            dto = _staticStore.FindByTitle(monster.Name) ?? _staticStore.FindByName(monster.Name);

        // Id in static JSON is string like "monster_089_00"; game Id is int — title match is primary.
        if (dto is null && monster.Id >= 0)
            dto = _staticStore.FindById(monster.Id.ToString());

        if (dto is null)
        {
            bool capturable = monster.CaptureThreshold > 0;
            return MonsterHudMapper.CreateFallbackStatic(
                name: string.IsNullOrWhiteSpace(monster.Name) ? $"#{monster.Id}" : monster.Name,
                isCapturable: capturable,
                captureThresholdPercent: capturable
                    ? monster.CaptureThreshold * 100.0
                    : MonsterHudMapper.DefaultCaptureThresholdPercent);
        }

        var mapped = MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));
        // Prefer live capture ratio when game reports one.
        if (monster.CaptureThreshold > 0 && mapped.IsCapturable)
        {
            return mapped with { CaptureThresholdPercent = monster.CaptureThreshold * 100.0 };
        }

        return mapped;
    }

    private MonsterLiveFixture BuildFixture(IMonster monster)
    {
        var parts = monster.Parts
            .Select(p =>
            {
                // Afflicted: 啮生虫 threshold + only *currently* infected hitzones.
                // Do not use Type==Qurio alone — Type can stick after infection ends and
                // would keep feeding stale full Qurio bars on already-cleared parts.
                bool isQurioThreshold = string.Equals(p.Id, "PART_QURIO_THRESHOLD", StringComparison.OrdinalIgnoreCase);
                var mhrPart = p as MHRMonsterPart;
                bool useQurio = mhrPart is not null
                                && (isQurioThreshold
                                    || (mhrPart.IsQurioInfected && mhrPart.QurioMaxHealth > 0));
                double health = useQurio ? mhrPart!.QurioHealth : p.Health;
                double maxHealth = useQurio ? mhrPart!.QurioMaxHealth : p.MaxHealth;

                bool isTail = MHRMonsterPart.IsCuttableTailPartId(p.Id);
                bool isHorn = p.Id.Contains("HORN", StringComparison.OrdinalIgnoreCase);
                // 「可断/已断尾」仅限可切断的尾巴。禁止用 Type.Severable / 非尾 MaxSever 噪声误标头部。
                bool isSeverable = isTail
                                   && (p.MaxSever > 0 || mhrPart?.HasSeverableEvidence == true);

                bool isBreakable = !isSeverable && (
                    p.Type.HasFlag(PartType.Breakable)
                    || p.MaxHealth > 0
                    || isHorn
                    || mhrPart?.IsStructurallyBroken == true);

                // Break and sever are distinct pools in Rise. A breakable tail pool
                // completing must not be promoted to 「已断尾」; only sever evidence may do that.
                bool structurallyBroken = mhrPart is not null
                    ? isSeverable
                        ? mhrPart.IsSeverConfirmed
                        : mhrPart.IsBreakConfirmed
                    : IsStructurallyBrokenFallback(p, isSeverable, isBreakable);

                // Keep physical confirmation under a Qurio overlay so once the core clears,
                // the row returns to its correct break/sever state.
                return new MonsterLivePartFixture(
                    Id: p.Id,
                    DisplayName: _localizePart(p.Id),
                    Health: health,
                    MaxHealth: maxHealth,
                    BreakCount: p.Count,
                    IsQurio: useQurio,
                    Flinch: p.Flinch,
                    MaxFlinch: p.MaxFlinch,
                    Sever: p.Sever,
                    MaxSever: p.MaxSever,
                    IsBreakable: isBreakable,
                    IsSeverable: isSeverable,
                    IsQurioThreshold: isQurioThreshold,
                    IsStructurallyBroken: structurallyBroken,
                    HasAuthoritativeBreakState: mhrPart is not null);
            })
            .ToArray();

        var ailments = monster.Ailments
            .Select(a => new MonsterLiveAilmentFixture(
                Id: a.Id,
                DisplayName: null,
                Timer: a.Timer,
                MaxTimer: a.MaxTimer,
                BuildUp: a.BuildUp,
                MaxBuildUp: a.MaxBuildUp))
            .ToArray();

        MonsterLiveAilmentFixture? enrage = null;
        if (monster.Enrage is { } e)
        {
            enrage = new MonsterLiveAilmentFixture(
                Id: e.Id,
                DisplayName: null,
                Timer: e.Timer,
                MaxTimer: e.MaxTimer,
                BuildUp: e.BuildUp,
                MaxBuildUp: e.MaxBuildUp);
        }

        IQuest? quest = _context.Game.Quest;
        bool questAllows = MonsterLiveAdapter.ResolveQuestAllowsCapture(
            quest?.Type.ToString(),
            quest?.Level.ToString());

        // Afflicted (Qurio) monsters are slay-only — same as HunterPie capture threshold wipe.
        if (monster is MHRMonster { MonsterType: MonsterType.Qurio })
            questAllows = false;

        return new MonsterLiveFixture(
            Name: monster.Name,
            Id: monster.Id,
            Health: MonsterHealthDisplay.ForHud(monster.Health, monster.MaxHealth),
            MaxHealth: monster.MaxHealth,
            Stamina: monster.Stamina,
            MaxStamina: monster.MaxStamina,
            CaptureThreshold: monster.CaptureThreshold,
            IsEnraged: monster.IsEnraged,
            Parts: parts,
            Ailments: ailments,
            Enrage: enrage,
            QuestAllowsCapture: questAllows,
            IsAnomaly: monster is MHRMonster { MonsterType: MonsterType.Qurio });
    }

    /// <summary>Fallback when the part is not an MHRMonsterPart (tests / other games).</summary>
    private static bool IsStructurallyBrokenFallback(IMonsterPart p, bool isSeverable, bool isBreakable)
    {
        if (p.Count > 0)
            return true;

        if (isSeverable && p.MaxSever > 0 && p.Sever >= p.MaxSever
            && p.MaxFlinch > 0 && p.Flinch < p.MaxFlinch)
            return true;

        if (p.MaxHealth > 0 && p.Health <= 0)
            return true;

        // Collapsed breakable max after we already knew it was breakable (via isBreakable flag).
        if (isBreakable && p.MaxHealth <= 0)
            return true;

        return false;
    }

    private sealed class MonsterBinding : IDisposable
    {
        private readonly IMonster _monster;
        private readonly Action<IMonster> _onChanged;
        private readonly Action<IMonster, MonsterTargetEventArgs> _onTargetChanged;
        private readonly Action<IMonster, MonsterCompletionState> _onFinished;
        private readonly List<IMonsterPart> _hookedParts = [];
        private readonly List<IMonsterAilment> _hookedAilments = [];

        public MonsterBinding(
            IMonster monster,
            Action<IMonster> onChanged,
            Action<IMonster, MonsterTargetEventArgs> onTargetChanged,
            Action<IMonster, MonsterCompletionState> onFinished)
        {
            _monster = monster;
            _onChanged = onChanged;
            _onTargetChanged = onTargetChanged;
            _onFinished = onFinished;

            _monster.OnHealthChange += OnAny;
            _monster.OnStaminaChange += OnAny;
            _monster.OnEnrageStateChange += OnAny;
            _monster.OnCaptureThresholdChange += OnCaptureThreshold;
            _monster.OnTargetChange += OnTarget;
            _monster.OnNewPartFound += OnNewPart;
            _monster.OnNewAilmentFound += OnNewAilment;
            _monster.OnDeath += OnDeath;
            _monster.OnCapture += OnCapture;
            _monster.OnDespawn += OnAny;

            foreach (var part in _monster.Parts)
                HookPart(part);
            foreach (var ailment in _monster.Ailments)
                HookAilment(ailment);
            if (_monster.Enrage is { } enrage)
                HookAilment(enrage);
        }

        public void Dispose()
        {
            _monster.OnHealthChange -= OnAny;
            _monster.OnStaminaChange -= OnAny;
            _monster.OnEnrageStateChange -= OnAny;
            _monster.OnCaptureThresholdChange -= OnCaptureThreshold;
            _monster.OnTargetChange -= OnTarget;
            _monster.OnNewPartFound -= OnNewPart;
            _monster.OnNewAilmentFound -= OnNewAilment;
            _monster.OnDeath -= OnDeath;
            _monster.OnCapture -= OnCapture;
            _monster.OnDespawn -= OnAny;

            foreach (var part in _hookedParts)
            {
                part.OnHealthUpdate -= OnPart;
                part.OnBreakCountUpdate -= OnPart;
                part.OnFlinchUpdate -= OnPart;
                part.OnSeverUpdate -= OnPart;
                if (part is MHRMonsterPart mhr)
                    mhr.OnQurioHealthChange -= OnQurioPart;
            }

            foreach (var ailment in _hookedAilments)
            {
                ailment.OnTimerUpdate -= OnAilment;
                ailment.OnBuildUpUpdate -= OnAilment;
            }

            _hookedParts.Clear();
            _hookedAilments.Clear();
        }

        private void OnAny(object? sender, EventArgs e) => _onChanged(_monster);
        private void OnDeath(object? sender, EventArgs e)
            => _onFinished(_monster, MonsterCompletionState.Slain);
        private void OnCapture(object? sender, EventArgs e)
            => _onFinished(_monster, MonsterCompletionState.Captured);
        private void OnCaptureThreshold(object? sender, IMonster e) => _onChanged(_monster);
        private void OnTarget(object? sender, MonsterTargetEventArgs e) => _onTargetChanged(_monster, e);
        private void OnPart(object? sender, IMonsterPart e) => _onChanged(_monster);
        private void OnQurioPart(object? sender, IMonsterPart e) => _onChanged(_monster);
        private void OnAilment(object? sender, IMonsterAilment e) => _onChanged(_monster);

        private void OnNewPart(object? sender, IMonsterPart part)
        {
            HookPart(part);
            _onChanged(_monster);
        }

        private void OnNewAilment(object? sender, IMonsterAilment ailment)
        {
            HookAilment(ailment);
            _onChanged(_monster);
        }

        private void HookPart(IMonsterPart part)
        {
            if (_hookedParts.Contains(part))
                return;
            part.OnHealthUpdate += OnPart;
            part.OnBreakCountUpdate += OnPart;
            part.OnFlinchUpdate += OnPart;
            part.OnSeverUpdate += OnPart;
            // Afflicted Qurio HP does not raise OnHealthUpdate — must hook explicitly
            // or 啮生虫 / 怪异化 bars stay frozen while breakable (可破) bars still move.
            if (part is MHRMonsterPart mhr)
                mhr.OnQurioHealthChange += OnQurioPart;
            _hookedParts.Add(part);
        }

        private void HookAilment(IMonsterAilment ailment)
        {
            if (_hookedAilments.Contains(ailment))
                return;
            ailment.OnTimerUpdate += OnAilment;
            ailment.OnBuildUpUpdate += OnAilment;
            _hookedAilments.Add(ailment);
        }
    }
}
