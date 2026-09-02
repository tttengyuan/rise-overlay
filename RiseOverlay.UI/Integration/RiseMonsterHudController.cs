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
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            ClearHudFreeze();
            _lastLiveHudDto = null;
            _lastLiveMonster = null;
            _active = null;
            ApplyResetHudForQuest(e);
            RefreshActiveMonster();
        });

    private void OnQuestEnded(object? sender, QuestEndEventArgs e)
    {
        // Prefer an already-held death freeze, then last combat frame.
        // Never rebuild from whatever map invader is currently Target.Self at quest-end.
        MonsterHudDto? captured = ChooseFreezeSnapshot(liveNow: null);

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
            bool leftHunt = !IsOnHuntMap();
            if (!_hudFrozen || !leftHunt || _context.Game.Quest is not null)
                return;

            ClearHudFreeze();
            RefreshActiveMonster();
        });

    private void AttachMonster(IMonster monster)
    {
        if (_bindings.ContainsKey(monster))
            return;

        var binding = new MonsterBinding(monster, OnMonsterDataChanged);
        _bindings[monster] = binding;
    }

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

    private IMonster? SelectAliveLockedMonster()
        => _bindings.Keys.FirstOrDefault(m =>
            m.Target == Target.Self
            && m.MaxHealth > 0
            && MonsterHealthDisplay.ForHud(m.Health, m.MaxHealth) > 0);

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

    private void FreezeOnActiveDeath(MonsterHudDto lastAliveFrame)
    {
        _hudFrozen = true;
        // Keep identity/parts from the last combat frame, but show 0 HP after the slay.
        // Rise often leaves 1 HP in memory through the death animation.
        _frozenHudDto = lastAliveFrame with { HealthCurrent = 0 };
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
            FreezeOnActiveDeath(lastAlive);
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

                // Prefer structural sever/break signals; Type alone is Qurio while infected.
                bool isSeverable = p.MaxSever > 0 || p.Type.HasFlag(PartType.Severable);
                bool isBreakable = p.Type.HasFlag(PartType.Breakable)
                                   || (!isSeverable && (p.MaxHealth > 0 || mhrPart?.IsStructurallyBroken == true));
                // Rise never increments Count. Keep structural break even while infected so
                // clearing a Qurio core cannot bring back 「可破」 on an already-broken part.
                int breakCount = p.Count;
                bool structurallyBroken = mhrPart?.IsStructurallyBroken == true
                                          || IsStructurallyBrokenFallback(p, isSeverable, isBreakable);
                if (breakCount <= 0 && structurallyBroken)
                    breakCount = 1;

                // Infected: show Qurio HP + 怪异化. Already broken + not infected: 已破坏.
                // Infected wins status via IsQurio; BreakCount still carried for after-clear.
                return new MonsterLivePartFixture(
                    Id: p.Id,
                    DisplayName: _localizePart(p.Id),
                    Health: health,
                    MaxHealth: maxHealth,
                    BreakCount: useQurio ? 0 : breakCount,
                    IsQurio: useQurio,
                    Flinch: p.Flinch,
                    MaxFlinch: p.MaxFlinch,
                    Sever: p.Sever,
                    MaxSever: p.MaxSever,
                    IsBreakable: isBreakable,
                    IsSeverable: isSeverable,
                    IsQurioThreshold: isQurioThreshold);
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
        private readonly List<IMonsterPart> _hookedParts = [];
        private readonly List<IMonsterAilment> _hookedAilments = [];

        public MonsterBinding(IMonster monster, Action<IMonster> onChanged)
        {
            _monster = monster;
            _onChanged = onChanged;

            _monster.OnHealthChange += OnAny;
            _monster.OnStaminaChange += OnAny;
            _monster.OnEnrageStateChange += OnAny;
            _monster.OnCaptureThresholdChange += OnCaptureThreshold;
            _monster.OnTargetChange += OnTarget;
            _monster.OnNewPartFound += OnNewPart;
            _monster.OnNewAilmentFound += OnNewAilment;
            _monster.OnDeath += OnAny;
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
            _monster.OnDeath -= OnAny;
            _monster.OnDespawn -= OnAny;

            foreach (var part in _hookedParts)
            {
                part.OnHealthUpdate -= OnPart;
                part.OnBreakCountUpdate -= OnPart;
                part.OnFlinchUpdate -= OnPart;
                part.OnSeverUpdate -= OnPart;
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
        private void OnCaptureThreshold(object? sender, IMonster e) => _onChanged(_monster);
        private void OnTarget(object? sender, MonsterTargetEventArgs e) => _onChanged(_monster);
        private void OnPart(object? sender, IMonsterPart e) => _onChanged(_monster);
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
