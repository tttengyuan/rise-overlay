using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
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
    private readonly Func<string, string> _localizePart;
    private readonly Dictionary<IMonster, MonsterBinding> _bindings = new();
    private IMonster? _active;

    public RiseMonsterHudController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        MonsterStaticStore staticStore,
        Func<string, string>? localizePart = null)
    {
        _context = context;
        _viewModel = viewModel;
        _staticStore = staticStore;
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
    }

    public void UnhookEvents()
    {
        _context.Game.OnMonsterSpawn -= OnMonsterSpawn;
        _context.Game.OnMonsterDespawn -= OnMonsterDespawn;
        _context.Game.OnQuestStart -= OnQuestChanged;
        _context.Game.OnQuestEnd -= OnQuestEnded;

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
        => _viewModel.UIThread.BeginInvoke(PushActiveHud);

    private void OnQuestEnded(object? sender, QuestEndEventArgs e)
        => _viewModel.UIThread.BeginInvoke(PushActiveHud);

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
            if (!ReferenceEquals(_active, monster))
            {
                RefreshActiveMonster();
                return;
            }

            PushActiveHud();
        });

    private void RefreshActiveMonster()
    {
        IMonster? next = SelectActiveMonster();
        _active = next;
        _viewModel.HasActiveMonster = next is not null && next.Health > 0;
        PushActiveHud();
    }

    private IMonster? SelectActiveMonster()
    {
        var alive = _bindings.Keys.Where(m => m.Health > 0).ToArray();
        if (alive.Length == 0)
            return null;

        var targeted = alive.FirstOrDefault(m => m.Target == Target.Self)
                       ?? alive.FirstOrDefault(m => m.ManualTarget == Target.Self);
        return targeted ?? alive[0];
    }

    private void PushActiveHud()
    {
        if (_active is null || _active.Health <= 0)
        {
            _viewModel.HasActiveMonster = false;
            return;
        }

        var staticMapped = ResolveStatic(_active);
        var live = MonsterLiveAdapter.ToSnapshot(BuildFixture(_active));
        var dto = MonsterHudMapper.MergeLive(staticMapped, live);
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
            .Select(p => new MonsterLivePartFixture(
                Id: p.Id,
                DisplayName: _localizePart(p.Id),
                Health: p.Health,
                MaxHealth: p.MaxHealth,
                BreakCount: p.Count))
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

        bool questAllows = MonsterLiveAdapter.ResolveQuestAllowsCapture(
            _context.Game.Quest?.Type.ToString());

        return new MonsterLiveFixture(
            Name: monster.Name,
            Id: monster.Id,
            Health: monster.Health,
            MaxHealth: monster.MaxHealth,
            Stamina: monster.Stamina,
            MaxStamina: monster.MaxStamina,
            CaptureThreshold: monster.CaptureThreshold,
            IsEnraged: monster.IsEnraged,
            Parts: parts,
            Ailments: ailments,
            Enrage: enrage,
            QuestAllowsCapture: questAllows);
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
