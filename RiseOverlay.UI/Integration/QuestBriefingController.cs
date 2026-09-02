using System.Windows;
using System.Windows.Threading;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Events;
using HunterPie.Core.Observability.Logging;
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
/// Briefing targets: prefer static quest→monster table by <see cref="IQuest.Id"/>;
/// fall back to remembered live monster keys. Training combat only after lock-on.
/// </para>
/// </summary>
public sealed class QuestBriefingController : IContextHandler, IDisposable
{
    private const int MaxBriefingTargets = 4;
    private static readonly ILogger Logger = LoggerFactory.Create();

    private readonly IContext _context;
    private readonly RiseCompactMonsterViewModel _viewModel;
    private readonly MonsterStaticStore _staticStore;
    private readonly QuestStaticStore _questStore;
    private readonly DispatcherTimer _alignTimer;
    private readonly RiseOverlayPlacement _placement = new();

    private bool _questActive;
    private bool _enteredCombat;
    private bool _huntSummary;
    private OverlayScene _scene = OverlayScene.Idle;
    private readonly List<TargetKey> _targetKeys = [];

    public QuestBriefingController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        MonsterStaticStore staticStore,
        QuestStaticStore? questStore = null)
    {
        _context = context;
        _viewModel = viewModel;
        _staticStore = staticStore;
        _questStore = questStore ?? QuestStaticStore.LoadEmpty();

        _placement.RestoreOrSnapInitial(context.Process, viewModel.Config.Position);

        HookEvents();
        BootstrapFromCurrentState();

        _alignTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _alignTimer.Tick += OnAlignTick;
        _alignTimer.Start();
    }

    private void OnAlignTick(object? sender, EventArgs e)
    {
        // Preserve free drag: only follow game-window movement by delta, never force top-left.
        _placement.FollowGameWindowIfMoved(_context.Process, _viewModel.Config.Position);

        // Safety net if OnQuestStart/End was missed (scan race / type mapping).
        bool questNow = _context.Game.Quest is not null;
        if (questNow != _questActive)
        {
            bool wasActive = _questActive;
            _questActive = questNow;
            if (_questActive)
            {
                _enteredCombat = false;
                _huntSummary = false;
                _targetKeys.Clear();
                if (!PushBriefingDto())
                    PushPendingBriefingPlaceholder();
            }
            else if (wasActive)
            {
                _enteredCombat = false;
                _targetKeys.Clear();
                // Only keep hunt summary while still on a hunt map; village cancel → Idle.
                _huntSummary = IsOnHuntMap();
            }
        }

        // Village cancel / hub linger: drop stale combat summary without waiting for stage event.
        if (_huntSummary && !IsOnHuntMap() && _context.Game.Quest is null)
            _huntSummary = false;

        RefreshScene();
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
        _alignTimer.Stop();
        _alignTimer.Tick -= OnAlignTick;

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
            _huntSummary = false;
            _targetKeys.Clear();
            // Do not merge live monsters here — village leftovers inflated anomaly target counts.
            // PushBriefingDto adds hunt-map monsters only when already in the field.
            if (!PushBriefingDto())
                PushPendingBriefingPlaceholder();
            // Always enter briefing (or combat if monsters already present).
            RefreshScene();
        });

    private void PushPendingBriefingPlaceholder()
    {
        bool capturable = !IsAnomalyOrSlayQuest(_context.Game.Quest);
        var target = new QuestBriefingTargetDto(
            Name: "任务目标（进入地图后刷新）",
            OverallElementsOrdered: ElementRecommend.DisplayOrder,
            Recommended: [],
            IsCapturable: capturable,
            HasSeverableTail: false,
            FocusPartLabel: null
        );
        _viewModel.QuestBriefing.ApplyDto(new QuestBriefingDto([target]));
    }

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            _questActive = false;
            _enteredCombat = false;
            _targetKeys.Clear();

            // Finished a real hunt → keep DPS/HUD until leaving the map.
            // Cancelled / abandoned in hub → go Idle immediately (no empty combat shell).
            if (IsOnHuntMap())
            {
                _huntSummary = true;
                ApplyScene(OverlayScene.Combat);
            }
            else
            {
                _huntSummary = false;
                ApplyScene(OverlayScene.Idle);
            }
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
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_huntSummary && !IsOnHuntMap() && _context.Game.Quest is null)
                _huntSummary = false;
            RefreshScene();
        });

    private bool IsOnHuntMap()
        => _context.Game.Player.InHuntingZone || _context.Game.Player.StageId == 5;

    private void RefreshScene()
    {
        if (_huntSummary)
        {
            ApplyScene(OverlayScene.Combat);
            return;
        }

        // Combat HUD only after real camera lock-on (not quest auto-marker).
        if (HasLockedLargeMonster())
        {
            _enteredCombat = true;
            ApplyScene(OverlayScene.Combat);
            return;
        }

        if (_questActive)
        {
            // Rebuild every tick so late-filled anomaly targets / quest swaps refresh UI.
            _targetKeys.Clear();
            if (!PushBriefingDto())
                PushPendingBriefingPlaceholder();
            ApplyScene(OverlayScene.Briefing);
            return;
        }

        ApplyScene(OverlayScene.Idle);
    }

    private bool HasLockedLargeMonster()
        => _context.Game.Monsters.Any(m =>
            IsLargeMonsterCandidate(m)
            && m.Target == HunterPie.Core.Game.Enums.Target.Self);

    private void ApplyScene(OverlayScene scene)
    {
        bool briefing = scene == OverlayScene.Briefing;
        bool combat = scene == OverlayScene.Combat;
        bool attached = scene == OverlayScene.Idle;

        _viewModel.ShowBriefing = briefing;
        _viewModel.ShowCombat = combat;
        _viewModel.BriefingVisibility = briefing ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.CombatVisibility = combat ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.AttachedVisibility = attached ? Visibility.Visible : Visibility.Collapsed;
        // Keep host visible whenever Rise is attached so SizeToContent is not an empty hole.
        _viewModel.ContentVisibility = Visibility.Visible;

        if (_scene != scene)
        {
            _scene = scene;
            Logger.Info(
                $"Rise overlay scene → {scene} (quest={_questActive}, stage={_context.Game.Player.StageId}, monsters={_context.Game.Monsters.Count})");
        }
    }

    /// <summary>
    /// Builds briefing rows from quest static table (by quest id) then remembered live keys.
    /// Returns true when at least one target was applied.
    /// </summary>
    private bool PushBriefingDto()
    {
        IQuest? quest = _context.Game.Quest;

        // Anomaly / investigations: memory EmTypes are authoritative — do not merge static table
        // or village leftovers (that caused 1-target quests showing 2 rows).
        if (quest is { Level: QuestLevel.Anomaly, BriefingMonsterIds.Count: > 0 })
        {
            _targetKeys.Clear();
            MergeTargetKeysFromQuestBriefingIds();
            if (IsOnHuntMap())
                MergeTargetKeysFromMonsters();
        }
        else if (IsOnHuntMap())
        {
            // In the field: map spawns (quest target + intrusions) are authoritative order.
            MergeTargetKeysFromMonsters();
            MergeTargetKeysFromQuestStatic();
            MergeTargetKeysFromQuestBriefingIds();
        }
        else
        {
            MergeTargetKeysFromQuestStatic();
            MergeTargetKeysFromQuestBriefingIds();
            MergeTargetKeysFromMonsters();
        }

        var targets = BuildBriefingTargets(_staticStore, _targetKeys);
        if (targets.Count == 0)
            return false;

        // Afflicted / anomaly investigations and slay quests are capture-forbidden.
        if (IsAnomalyOrSlayQuest(quest))
        {
            targets = targets
                .Select(t => t with { IsCapturable = false })
                .ToArray();
        }

        _viewModel.QuestBriefing.ApplyDto(new QuestBriefingDto(targets));
        return true;
    }

    private static bool IsAnomalyOrSlayQuest(IQuest? quest)
        => quest is { Level: QuestLevel.Anomaly }
           || quest is { Type: QuestType.Slay };

    private void MergeTargetKeysFromQuestStatic()
    {
        IQuest? quest = _context.Game.Quest;
        if (quest is null)
            return;

        // Dynamic investigations (700000+) are not in the static table; skip to avoid bad merges.
        if (quest.Level == QuestLevel.Anomaly && quest.Id >= 700_000)
            return;

        QuestStaticDto? staticQuest = _questStore.FindById(quest.Id);
        if (staticQuest is null)
            return;

        foreach (string monsterRef in staticQuest.Monsters)
            TryAddStaticMonsterRef(monsterRef);
    }

    /// <summary>
    /// Anomaly investigations: target EmTypes read from memory into <see cref="IQuest.BriefingMonsterIds"/>.
    /// </summary>
    private void MergeTargetKeysFromQuestBriefingIds()
    {
        IQuest? quest = _context.Game.Quest;
        if (quest is null || quest.BriefingMonsterIds.Count == 0)
            return;

        foreach (string monsterRef in quest.BriefingMonsterIds)
            TryAddStaticMonsterRef(monsterRef);
    }

    private void TryAddStaticMonsterRef(string monsterRef)
    {
        if (_targetKeys.Count >= MaxBriefingTargets)
            return;

        MonsterStaticDto? monster = _staticStore.FindById(monsterRef);
        if (monster is null)
        {
            // Still show a row with the static id so briefing is not empty.
            var fallbackKey = new TargetKey(monsterRef, -1);
            if (_targetKeys.Any(k => k.SameAs(fallbackKey)))
                return;
            _targetKeys.Add(fallbackKey);
            return;
        }

        var key = new TargetKey(monster.Title, -1);
        if (_targetKeys.Any(k => k.SameAs(key) || string.Equals(k.Name, monster.Title, StringComparison.OrdinalIgnoreCase)))
            return;

        _targetKeys.Add(key);
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
        => _context.Game.Monsters.Any(m =>
            IsLargeMonsterCandidate(m) && (m.Health > 0 || m.MaxHealth > 0));

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

        if (dto is null && !string.IsNullOrWhiteSpace(key.Name))
            dto = store.FindById(key.Name!);

        // HunterPie monster.Id is the localization table id (e.g. 42 = 骚鸟), not EmType species
        // (monster_107_00). Never format it as monster_{Id:D3}_00 — that maps 42 → 冰牙龙.
        if (dto is null && key.Id >= 0)
            dto = store.FindById(key.Id.ToString());

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
