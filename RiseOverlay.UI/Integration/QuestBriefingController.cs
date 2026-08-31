using System.Windows;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Events;
using HunterPie.UI.Overlay;
using RiseOverlay.Data;
using RiseOverlay.Domain;
using RiseOverlay.UI.Overlay;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Orchestrates quest briefing ↔ combat HUD on the compact Rise overlay host.
/// <para>
/// Event sources: <see cref="IGame.OnQuestStart"/>, <see cref="IGame.OnQuestEnd"/>,
/// <see cref="IGame.OnMonsterSpawn"/>, <see cref="IGame.OnMonsterDespawn"/>,
/// <see cref="HunterPie.Core.Game.Entity.Player.IPlayer.OnStageUpdate"/>.
/// </para>
/// <para>
/// Heuristics (Rise <see cref="IQuest"/> does not expose target monster ids):
/// briefing when quest active, no large monsters spawned yet, and at least one
/// target can be resolved from known name/id keys; combat when any large monster
/// is alive; idle when quest ended / idle.
/// </para>
/// </summary>
public sealed class QuestBriefingController : IContextHandler, IDisposable
{
    private const int MaxBriefingTargets = 4;

    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly MonsterStaticStore _staticStore;

    private bool _questActive;
    private bool _enteredCombat;
    private readonly List<TargetKey> _targetKeys = [];

    public QuestBriefingController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        MonsterStaticStore staticStore)
    {
        _context = context;
        _viewModel = viewModel;
        _staticStore = staticStore;

        HookEvents();
        BootstrapFromCurrentState();
    }

    public void HookEvents()
    {
        _context.Game.OnQuestStart += OnQuestStart;
        _context.Game.OnQuestEnd += OnQuestEnd;
        _context.Game.OnMonsterSpawn += OnMonsterSpawn;
        _context.Game.OnMonsterDespawn += OnMonsterDespawn;
        _context.Game.Player.OnStageUpdate += OnStageUpdate;
    }

    public void UnhookEvents()
    {
        _context.Game.OnQuestStart -= OnQuestStart;
        _context.Game.OnQuestEnd -= OnQuestEnd;
        _context.Game.OnMonsterSpawn -= OnMonsterSpawn;
        _context.Game.OnMonsterDespawn -= OnMonsterDespawn;
        _context.Game.Player.OnStageUpdate -= OnStageUpdate;

        _viewModel.UIThread.BeginInvoke(() => ApplyScene(OverlayScene.Idle));
    }

    public void Dispose() => UnhookEvents();

    private void BootstrapFromCurrentState()
    {
        _questActive = _context.Game.Quest is not null;
        _enteredCombat = false;
        _targetKeys.Clear();
        MergeTargetKeysFromMonsters();
        if (HasAliveLargeMonster())
            _enteredCombat = true;

        _viewModel.UIThread.BeginInvoke(RefreshScene);
    }

    private void OnQuestStart(object? sender, IQuest e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            _questActive = true;
            _enteredCombat = false;
            _targetKeys.Clear();
            MergeTargetKeysFromMonsters();
            PushBriefingDto();
            RefreshScene();
        });

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            _questActive = false;
            _enteredCombat = false;
            _targetKeys.Clear();
            ApplyScene(OverlayScene.Idle);
        });

    private void OnMonsterSpawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            RememberTarget(monster);
            if (IsLargeMonsterCandidate(monster))
                _enteredCombat = true;
            RefreshScene();
        });

    private void OnMonsterDespawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(RefreshScene);

    private void OnStageUpdate(object? sender, EventArgs e)
        => _viewModel.UIThread.BeginInvoke(RefreshScene);

    private void RefreshScene()
    {
        if (!_questActive)
        {
            ApplyScene(OverlayScene.Idle);
            return;
        }

        if (HasAliveLargeMonster())
        {
            _enteredCombat = true;
            ApplyScene(OverlayScene.Combat);
            return;
        }

        // Prefer briefing before first combat this quest (hub / loading / pre-spawn).
        // Also allow when still outside hunting zone even if combat was marked (rare).
        bool preCombat = !_enteredCombat || !_context.Game.Player.InHuntingZone;
        if (preCombat && PushBriefingDto())
        {
            ApplyScene(OverlayScene.Briefing);
            return;
        }

        ApplyScene(OverlayScene.Idle);
    }

    private void ApplyScene(OverlayScene scene)
    {
        bool briefing = scene == OverlayScene.Briefing;
        bool combat = scene == OverlayScene.Combat;

        _viewModel.ShowBriefing = briefing;
        _viewModel.ShowCombat = combat;
        _viewModel.BriefingVisibility = briefing ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.CombatVisibility = combat ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.ContentVisibility = (briefing || combat) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Builds briefing rows from remembered name/id keys via static store + mapper.
    /// Returns true when at least one target was applied.
    /// </summary>
    private bool PushBriefingDto()
    {
        MergeTargetKeysFromMonsters();
        var targets = BuildBriefingTargets(_staticStore, _targetKeys);
        if (targets.Count == 0)
            return false;

        _viewModel.QuestBriefing.ApplyDto(new QuestBriefingDto(targets));
        return true;
    }

    private void MergeTargetKeysFromMonsters()
    {
        foreach (IMonster monster in _context.Game.Monsters)
            RememberTarget(monster);
    }

    private void RememberTarget(IMonster monster)
    {
        if (!IsLargeMonsterCandidate(monster))
            return;

        var key = new TargetKey(monster.Name, monster.Id);
        if (_targetKeys.Any(k => k.SameAs(key)))
            return;

        if (_targetKeys.Count >= MaxBriefingTargets)
            return;

        _targetKeys.Add(key);
    }

    private bool HasAliveLargeMonster()
        => _context.Game.Monsters.Any(m => IsLargeMonsterCandidate(m) && m.Health > 0);

    /// <summary>
    /// Rise scans only large monsters into <see cref="IGame.Monsters"/> in hunting zones;
    /// treat any listed monster with a name or id as a large target candidate.
    /// </summary>
    private static bool IsLargeMonsterCandidate(IMonster monster)
        => monster.Id >= 0 || !string.IsNullOrWhiteSpace(monster.Name);

    internal static IReadOnlyList<QuestBriefingTargetDto> BuildBriefingTargets(
        MonsterStaticStore store,
        IEnumerable<TargetKey> keys)
    {
        var list = new List<QuestBriefingTargetDto>();
        foreach (var key in keys)
        {
            if (list.Count >= MaxBriefingTargets)
                break;

            var mapped = ResolveStatic(store, key);
            list.Add(MonsterHudMapper.ToBriefingTarget(mapped));
        }

        return list;
    }

    private static MonsterStaticMapped ResolveStatic(MonsterStaticStore store, TargetKey key)
    {
        MonsterStaticDto? dto = null;
        if (!string.IsNullOrWhiteSpace(key.Name))
            dto = store.FindByTitle(key.Name) ?? store.FindByName(key.Name);

        if (dto is null && key.Id >= 0)
        {
            dto = store.FindById(key.Id.ToString())
                  ?? store.FindById($"monster_{key.Id:D3}_00");
        }

        if (dto is null)
        {
            string fallbackName = !string.IsNullOrWhiteSpace(key.Name)
                ? key.Name!
                : $"#{key.Id}";
            return MonsterHudMapper.CreateFallbackStatic(fallbackName, isCapturable: true);
        }

        return MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));
    }

    internal readonly record struct TargetKey(string? Name, int Id)
    {
        public bool SameAs(TargetKey other)
            => Id >= 0 && other.Id >= 0
                ? Id == other.Id
                : string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    private enum OverlayScene
    {
        Idle,
        Briefing,
        Combat,
    }
}
