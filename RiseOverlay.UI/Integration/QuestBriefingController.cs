using System.Windows;
using System.Windows.Threading;
using HunterPie.Core.Game;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Events;
using HunterPie.Core.Observability.Logging;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;
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
    private readonly QuestCompletionClock _completionClock;
    private readonly object _progressSync = new();

    private bool _questActive;
    private bool _huntSummary;
    private OverlayScene _scene = OverlayScene.Idle;
    private readonly List<TargetKey> _targetKeys = [];
    private readonly HashSet<string> _defeatedQuestTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<IMonster> _lifecycleHooked = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IMonster, MonsterTracking> _monsterTracking =
        new(ReferenceEqualityComparer.Instance);
    private long _trackingGeneration;
    private long _nextMonsterInstanceId;
    private List<TargetKey> _lastQuestKeys = [];
    private QuestBriefingDto? _lastBriefingDto;

    public QuestBriefingController(
        IContext context,
        RiseCompactMonsterViewModel viewModel,
        MonsterStaticStore staticStore,
        QuestStaticStore? questStore = null,
        QuestCompletionClock? completionClock = null)
    {
        _context = context;
        _viewModel = viewModel;
        _staticStore = staticStore;
        _questStore = questStore ?? QuestStaticStore.LoadEmpty();
        _completionClock = completionClock ?? new QuestCompletionClock();

        _placement.RestoreOrSnapInitial(viewModel.Config.Position);

        HookEvents();
        BootstrapFromCurrentState();

        _alignTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _alignTimer.Tick += OnAlignTick;
        _alignTimer.Start();
    }

    private void OnAlignTick(object? sender, EventArgs e)
    {
        // Original HunterPie behavior: fixed absolute screen position. Only rescue when a
        // display/DPI change leaves the saved point outside the virtual desktop.
        _placement.EnsureVisible(_viewModel.Config.Position);

        // Safety net if OnQuestStart/End was missed (scan race / type mapping).
        bool questNow = _context.Game.Quest is not null;
        if (questNow != _questActive)
        {
            bool wasActive = _questActive;
            _questActive = questNow;
            if (_questActive)
            {
                BeginQuestTracking(_context.Game.Quest);
                _huntSummary = false;
                _targetKeys.Clear();
                ClearBriefingProgress();
                if (!PushBriefingDto())
                    PushPendingBriefingPlaceholder();
            }
            else if (wasActive)
            {
                UnhookAllMonsterLifecycle(deactivate: true);
                _targetKeys.Clear();
                ClearBriefingProgress();
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

        UnhookAllMonsterLifecycle(deactivate: true);

        _viewModel.UIThread.BeginInvoke(() => ApplyScene(OverlayScene.Idle));
    }

    public void Dispose() => UnhookEvents();

    private void BootstrapFromCurrentState()
    {
        _questActive = _context.Game.Quest is not null;
        if (_questActive)
            BeginQuestTracking(_context.Game.Quest);
        _targetKeys.Clear();
        MergeTargetKeysFromMonsters();
        foreach (IMonster monster in _context.Game.Monsters)
            HookMonsterLifecycle(monster);

        _viewModel.UIThread.BeginInvoke(RefreshScene);
    }

    private void OnQuestStart(object? sender, IQuest e)
    {
        BeginQuestTracking(e);
        _viewModel.UIThread.BeginInvoke(() =>
        {
            _questActive = true;
            _huntSummary = false;
            _targetKeys.Clear();
            ClearBriefingProgress();
            // Do not merge live monsters here — village leftovers inflated anomaly target counts.
            // PushBriefingDto adds hunt-map monsters only when already in the field.
            if (!PushBriefingDto())
                PushPendingBriefingPlaceholder();
            // Always enter briefing (or combat if monsters already present).
            RefreshScene();
        });
    }

    private void PushPendingBriefingPlaceholder()
    {
        IQuest? quest = _context.Game.Quest;
        bool capturable = quest?.Type != QuestType.Slay;
        int expected = Math.Clamp(
            Math.Max(quest?.BriefingMonsterIds.Count ?? 0, quest?.TargetCountHint ?? 0),
            1,
            MaxBriefingTargets);

        var rows = new List<QuestBriefingTargetDto>(expected);
        for (int i = 0; i < expected; i++)
        {
            string name = expected == 1
                ? "任务目标（进入地图后刷新）"
                : $"任务目标 {i + 1}/{expected}（进入地图后刷新）";

            rows.Add(new QuestBriefingTargetDto(
                Name: name,
                OverallElementsOrdered: ElementRecommend.DisplayOrder,
                Recommended: [],
                IsCapturable: capturable,
                HasSeverableTail: false,
                FocusPartLabel: null
            ));
        }

        IReadOnlyList<QuestBriefingTargetDto> resolved = CaptureRules.ResolveBriefingTargetStates(
            rows,
            isAnomalyQuest: quest?.Level == QuestLevel.Anomaly,
            isSlayQuest: quest?.Type == QuestType.Slay);
        ApplyBriefingIfChanged(new QuestBriefingDto(resolved));
    }

    private void OnQuestEnd(object? sender, QuestEndEventArgs e)
    {
        // Last-chance synchronous scan before the DPS handler resolves the result clock.
        if (e.Status is QuestStatus.Success)
        {
            IReadOnlyList<TargetKey> keys = CollectQuestTargetKeys();
            UpdateDefeatedQuestTargets(keys);
        }
        UnhookAllMonsterLifecycle(deactivate: true);
        _viewModel.UIThread.BeginInvoke(() =>
        {
            _questActive = false;
            _targetKeys.Clear();
            ClearBriefingProgress();

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
    }

    private void OnMonsterSpawn(object? sender, IMonster monster)
    {
        HookMonsterLifecycle(monster);
        _viewModel.UIThread.BeginInvoke(() =>
        {
            RememberTarget(monster);
            RefreshScene();
        });
    }

    private void OnMonsterDespawn(object? sender, IMonster monster)
    {
        UnhookMonsterLifecycle(monster);
        _viewModel.UIThread.BeginInvoke(() =>
        {
            // Despawn also fires on area unload — never treat it as quest completion.
            RefreshScene();
        });
    }

    private void BeginQuestTracking(IQuest? quest)
    {
        int expected = Math.Max(
            quest?.BriefingMonsterIds.Count ?? 0,
            quest?.TargetCountHint ?? 0);
        KeyValuePair<IMonster, MonsterTracking>[] previouslyHooked;
        lock (_progressSync)
        {
            previouslyHooked = _monsterTracking.ToArray();
            _lifecycleHooked.Clear();
            _monsterTracking.Clear();
            _nextMonsterInstanceId = 0;
            _questActive = true;
            _trackingGeneration = _completionClock.BeginQuest(expected);
        }
        foreach ((IMonster monster, MonsterTracking tracking) in previouslyHooked)
            UnsubscribeMonsterLifecycle(monster, tracking.Handler);

        // Required when the overlay attaches after the quest has already started. Do not hook
        // village leftovers from the previous quest before the hunt map has loaded.
        if (IsOnHuntMap())
        {
            foreach (IMonster monster in _context.Game.Monsters)
                HookMonsterLifecycle(monster);
        }
    }

    private void HookMonsterLifecycle(IMonster monster)
    {
        MonsterTracking tracking;
        lock (_progressSync)
        {
            if (!_questActive)
                return;

            if (_monsterTracking.TryGetValue(monster, out MonsterTracking? current)
                && current is { Generation: var generation }
                && generation == _trackingGeneration)
                return;

            EventHandler<EventArgs> handler = (sender, args) => OnMonsterFinished(sender, args);
            tracking = new MonsterTracking(
                _trackingGeneration,
                $"{_trackingGeneration}:{++_nextMonsterInstanceId}",
                handler);
            _lifecycleHooked.Add(monster);
            _monsterTracking[monster] = tracking;
        }

        monster.OnDeath += tracking.Handler;
        monster.OnCapture += tracking.Handler;

        // A quest boundary may have invalidated this registration while event accessors ran.
        bool stillValid;
        lock (_progressSync)
            stillValid = _questActive
                && _lifecycleHooked.Contains(monster)
                && _monsterTracking.TryGetValue(monster, out MonsterTracking? current)
                && current is not null
                && current == tracking;
        if (!stillValid)
            UnsubscribeMonsterLifecycle(monster, tracking.Handler);
    }

    private void UnhookMonsterLifecycle(IMonster monster)
    {
        MonsterTracking? tracking;
        lock (_progressSync)
        {
            _lifecycleHooked.Remove(monster);
            _monsterTracking.Remove(monster, out tracking);
        }
        if (tracking is not null)
            UnsubscribeMonsterLifecycle(monster, tracking.Handler);
    }

    private void UnhookAllMonsterLifecycle(bool deactivate = false)
    {
        KeyValuePair<IMonster, MonsterTracking>[] hooked;
        lock (_progressSync)
        {
            if (deactivate)
                _questActive = false;
            hooked = _monsterTracking.ToArray();
            _lifecycleHooked.Clear();
            _monsterTracking.Clear();
        }
        foreach ((IMonster monster, MonsterTracking tracking) in hooked)
            UnsubscribeMonsterLifecycle(monster, tracking.Handler);
    }

    private static void UnsubscribeMonsterLifecycle(
        IMonster monster,
        EventHandler<EventArgs> handler)
    {
        monster.OnDeath -= handler;
        monster.OnCapture -= handler;
    }

    private void EnsureCurrentMonsterLifecycle()
    {
        if (!_questActive || !IsOnHuntMap())
            return;

        foreach (IMonster monster in _context.Game.Monsters)
            HookMonsterLifecycle(monster);
    }

    private void OnMonsterFinished(object? sender, EventArgs e)
    {
        if (sender is not IMonster monster)
            return;

        // Commit before dispatching UI work. QuestEnd can follow this callback immediately.
        TrackMonsterCompletion(monster, _context.Game.TimeElapsed);
        _viewModel.UIThread.BeginInvoke(RefreshScene);
    }

    private void OnStageUpdate(object? sender, EventArgs e)
    {
        EnsureCurrentMonsterLifecycle();
        _viewModel.UIThread.BeginInvoke(() =>
        {
            if (_huntSummary && !IsOnHuntMap() && _context.Game.Quest is null)
                _huntSummary = false;
            RefreshScene();
        });
    }

    private bool IsOnHuntMap()
        => _context.Game.Player.InHuntingZone || _context.Game.Player.StageId == 5;

    private void RefreshScene()
    {
        EnsureCurrentMonsterLifecycle();
        if (_huntSummary)
        {
            ApplyScene(OverlayScene.Combat);
            return;
        }

        // Combat HUD only after real camera lock-on (not quest auto-marker).
        if (HasLockedLargeMonster())
        {
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
    /// Builds briefing rows: quest-board targets (with defeated latch) + alive invaders.
    /// Returns true when at least one target was applied.
    /// </summary>
    private bool PushBriefingDto()
    {
        IQuest? quest = _context.Game.Quest;

        var questKeys = CollectQuestTargetKeys();
        UpdateDefeatedQuestTargets(questKeys);

        var targets = new List<QuestBriefingTargetDto>();

        foreach (TargetKey key in questKeys)
        {
            if (targets.Count >= MaxBriefingTargets)
                break;

            MonsterStaticMapped mapped = ResolveStatic(_staticStore, key);
            bool defeated = IsTargetDefeated(key);
            targets.Add(MonsterHudMapper.ToBriefingTarget(mapped) with
            {
                Kind = BriefingTargetKind.Quest,
                IsDefeated = defeated,
            });
        }

        // Alive invaders on the hunt map — keep refreshing; never treat corpses as quest rows.
        if (IsOnHuntMap())
        {
            foreach (IMonster monster in _context.Game.Monsters)
            {
                if (targets.Count >= MaxBriefingTargets)
                    break;
                if (!IsLargeMonsterCandidate(monster))
                    continue;
                if (!IsMonsterAlive(monster))
                    continue;
                if (questKeys.Any(k => MatchesMonster(k, monster)))
                    continue;

                MonsterStaticMapped mapped = ResolveStatic(_staticStore, new TargetKey(monster.Name, monster.Id));
                targets.Add(MonsterHudMapper.ToBriefingTarget(mapped) with
                {
                    Kind = BriefingTargetKind.Invasion,
                    IsDefeated = false,
                });
            }
        }

        if (targets.Count == 0)
            return false;

        // Only the afflicted primary target is uncapturable in an anomaly investigation.
        // Optional cover targets and invaders keep their own species capture rules.
        targets = CaptureRules.ResolveBriefingTargetStates(
                targets,
                isAnomalyQuest: quest?.Level == QuestLevel.Anomaly,
                isSlayQuest: quest?.Type == QuestType.Slay)
            .ToList();

        ApplyBriefingIfChanged(new QuestBriefingDto(targets));
        return true;
    }

    private void ApplyBriefingIfChanged(QuestBriefingDto dto)
    {
        if (QuestBriefingDtoComparer.Equivalent(_lastBriefingDto, dto))
            return;

        _lastBriefingDto = dto;
        _viewModel.QuestBriefing.ApplyDto(dto);
    }

    private void ClearBriefingProgress()
    {
        lock (_progressSync)
        {
            _defeatedQuestTargets.Clear();
            _lastQuestKeys = [];
        }
        _lastBriefingDto = null;
    }

    private List<TargetKey> CollectQuestTargetKeys()
    {
        lock (_progressSync)
        {
            _targetKeys.Clear();
            IQuest? quest = _context.Game.Quest;
            IReadOnlyList<string> briefingIds = quest?.BriefingMonsterIds ?? [];

            // Memory EmTypes win only when every id resolves in the static monster table.
            // Bad probes (e.g. monster_022_00) must fall back to quests-overlay.json.
            bool memoryUsable = briefingIds.Count > 0
                && briefingIds.All(id => _staticStore.FindById(id) is not null);

            if (memoryUsable)
                MergeTargetKeysFromQuestBriefingIds(briefingIds);
            else
                MergeTargetKeysFromQuestStatic();

            _lastQuestKeys = _targetKeys.ToList();
            ConfigureCompletionTargets(_lastQuestKeys);
            return _lastQuestKeys.ToList();
        }
    }

    /// <summary>
    /// Dead bodies are a fallback when a lifecycle event was missed. Completion is latched by
    /// monster instance, so corpse despawn and repeated death/capture scans cannot change counts.
    /// </summary>
    private void UpdateDefeatedQuestTargets(IReadOnlyList<TargetKey> questKeys)
    {
        if (questKeys.Count == 0)
            return;

        if (!IsOnHuntMap())
            return;

        foreach (IMonster monster in _context.Game.Monsters)
        {
            HookMonsterLifecycle(monster);
            if (IsLargeMonsterCandidate(monster) && !IsMonsterAlive(monster))
                TrackMonsterCompletion(monster, _context.Game.TimeElapsed, questKeys);
        }

        lock (_progressSync)
            SyncDefeatedTargets(questKeys);
    }

    private void TrackMonsterCompletion(
        IMonster monster,
        double elapsed,
        IReadOnlyList<TargetKey>? knownKeys = null)
    {
        if (!IsLargeMonsterCandidate(monster))
            return;

        // Pull the current quest snapshot here, not from briefing rendering. Combat mode can
        // bypass briefing refresh while anomaly/multi-target ids are still being late-filled.
        IReadOnlyList<TargetKey> keys = knownKeys ?? CollectQuestTargetKeys();
        double? newlyConfirmed = null;
        lock (_progressSync)
        {
            if (!_questActive
                || !_monsterTracking.TryGetValue(monster, out MonsterTracking? tracking)
                || tracking is null
                || tracking.Generation != _trackingGeneration)
                return;

            ConfigureCompletionTargets(keys);

            // Invaders / unmatched corpses must not stamp quest-target completion chips.
            // Always attribute the kill to the live monster's identity — never remap through
            // a fuzzy title match onto a different quest-board species.
            if (!keys.Any(key => MatchesMonster(key, monster)))
                return;

            string species = _staticStore.ResolveIdentityKey(monster.Name);
            string instanceKey = ResolveCompletionInstanceKey(monster, tracking);

            bool hadConfirmation = _completionClock.ConfirmedElapsed is not null;
            _completionClock.RecordCompletion(
                tracking.Generation,
                instanceKey,
                species,
                elapsed);
            SyncDefeatedTargets(keys);
            if (!hadConfirmation)
                newlyConfirmed = _completionClock.ConfirmedElapsed;
        }

        if (newlyConfirmed is { } confirmed)
            Logger.Info($"Rise quest objective timing confirmed at {confirmed:0.00}s");
    }

    private void ConfigureCompletionTargets(IReadOnlyList<TargetKey> keys)
    {
        _completionClock.ConfigureTargets(
            _trackingGeneration,
            keys.Select(StableSpecies).ToArray(),
            _context.Game.Quest?.TargetCountHint ?? 0);
    }

    private void SyncDefeatedTargets(IReadOnlyList<TargetKey> keys)
    {
        _defeatedQuestTargets.Clear();
        foreach (IGrouping<string, TargetKey> group in keys.GroupBy(
                     StableSpecies,
                     StringComparer.OrdinalIgnoreCase))
        {
            int completed = _completionClock.CompletedCount(_trackingGeneration, group.Key);
            foreach (TargetKey key in group.OrderBy(k => k.Occurrence).Take(completed))
                _defeatedQuestTargets.Add(StableKey(key));
        }
    }

    private bool IsTargetDefeated(TargetKey key)
    {
        lock (_progressSync)
            return _defeatedQuestTargets.Contains(StableKey(key));
    }

    private static bool IsMonsterAlive(IMonster monster)
        => MonsterHealthDisplay.ForHud(monster.Health, monster.MaxHealth) > 0;

    private bool MatchesMonster(TargetKey key, IMonster monster)
    {
        if (!string.IsNullOrWhiteSpace(key.Name)
            && !string.IsNullOrWhiteSpace(monster.Name)
            && _staticStore.MatchesSpecies(key.Name, monster.Name))
            return true;

        // Localization table ids sometimes match across spawn/despawn of the same species.
        return key.Id >= 0 && monster.Id >= 0 && key.Id == monster.Id;
    }

    /// <summary>
    /// Prefer the native monster address so corpse re-wraps cannot double-count one kill
    /// as two quest completions.
    /// </summary>
    private static string ResolveCompletionInstanceKey(IMonster monster, MonsterTracking tracking)
    {
        if (monster is MHRMonster { Address: not 0 } rise)
            return $"{tracking.Generation}:0x{rise.Address:X}";

        return tracking.InstanceKey;
    }

    private string StableSpecies(TargetKey key)
        => !string.IsNullOrWhiteSpace(key.Name)
            ? _staticStore.ResolveIdentityKey(key.Name)
            : $"#{key.Id}";

    private string StableKey(TargetKey key)
        => $"{StableSpecies(key)}#{key.Occurrence}";

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

        IReadOnlyList<string> monsters = ExpandStaticMonstersByTitle(
            staticQuest.Monsters,
            staticQuest.Title,
            quest.TargetCountHint);

        foreach (string monsterRef in monsters)
            TryAddStaticMonsterRef(monsterRef);
    }

    /// <summary>
    /// Static export often lists a species once for 「N头同种」quests (e.g. 三头伞鸟).
    /// Expand to N rows when the title / hunt hint says so.
    /// </summary>
    internal static IReadOnlyList<string> ExpandStaticMonstersByTitle(
        IReadOnlyList<string> monsters,
        string? title,
        int targetCountHint)
    {
        if (monsters.Count == 0)
            return monsters;

        int heads = Math.Max(ParseHeadCountFromTitle(title), targetCountHint);
        if (heads <= monsters.Count)
            return monsters;

        // Only auto-expand a single-species list — multi-species tables are already explicit.
        string first = monsters[0];
        if (monsters.Any(m => !string.Equals(m, first, StringComparison.OrdinalIgnoreCase)))
            return monsters;

        var expanded = new List<string>(heads);
        for (int i = 0; i < heads; i++)
            expanded.Add(first);
        return expanded;
    }

    private static int ParseHeadCountFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        // 「调查2头岩龙」「三头伞鸟」「讨伐3头」
        var digit = System.Text.RegularExpressions.Regex.Match(title, @"([2-4])\s*头");
        if (digit.Success && int.TryParse(digit.Groups[1].Value, out int n))
            return n;

        if (title.Contains('三') && title.Contains('头'))
            return 3;
        if ((title.Contains('两') || title.Contains('二')) && title.Contains('头'))
            return 2;
        if (title.Contains('四') && title.Contains('头'))
            return 4;

        return 0;
    }

    /// <summary>
    /// Target EmTypes from memory into <see cref="IQuest.BriefingMonsterIds"/>.
    /// </summary>
    private void MergeTargetKeysFromQuestBriefingIds(IReadOnlyList<string> briefingIds)
    {
        IQuest? quest = _context.Game.Quest;
        if (quest is null || briefingIds.Count == 0)
            return;

        string? title = _questStore.FindById(quest.Id)?.Title;
        IReadOnlyList<string> monsters = ExpandStaticMonstersByTitle(
            briefingIds,
            title,
            quest.TargetCountHint);

        foreach (string monsterRef in monsters)
            TryAddStaticMonsterRef(monsterRef);
    }

    /// <summary>
    /// Always append — same species twice is a valid multi-target hunt.
    /// </summary>
    private void TryAddStaticMonsterRef(string monsterRef)
    {
        if (_targetKeys.Count >= MaxBriefingTargets)
            return;

        MonsterStaticDto? monster = _staticStore.FindById(monsterRef);
        string name = monster?.Title ?? monsterRef;
        string normalizedName = _staticStore.ResolveIdentityKey(name);
        int occurrence = _targetKeys.Count(k =>
            string.Equals(
                _staticStore.ResolveIdentityKey(k.Name),
                normalizedName,
                StringComparison.OrdinalIgnoreCase));
        _targetKeys.Add(new TargetKey(name, -1, occurrence));
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

        // Live memory addresses distinguish individuals; occurrence keeps StableKey unique for species.
        string normalizedName = _staticStore.ResolveIdentityKey(monster.Name);
        int occurrence = _targetKeys.Count(k =>
            string.Equals(
                _staticStore.ResolveIdentityKey(k.Name),
                normalizedName,
                StringComparison.OrdinalIgnoreCase)
            || (k.Id >= 0 && monster.Id >= 0 && k.Id == monster.Id));

        var key = new TargetKey(monster.Name, monster.Id, occurrence);
        if (_targetKeys.Any(k => k.Occurrence == key.Occurrence
            && ((k.Id >= 0 && key.Id >= 0 && k.Id == key.Id)
                || string.Equals(
                    _staticStore.ResolveIdentityKey(k.Name),
                    _staticStore.ResolveIdentityKey(key.Name),
                    StringComparison.OrdinalIgnoreCase))))
            return;

        if (_targetKeys.Count >= MaxBriefingTargets)
            return;

        _targetKeys.Add(key);
    }

    /// <summary>
    /// Rise scans only large monsters into <see cref="IGame.Monsters"/> in hunting zones;
    /// treat any listed monster with a name or id as a large target candidate.
    /// </summary>
    private static bool IsLargeMonsterCandidate(IMonster monster)
        => monster.Id >= 0 || !string.IsNullOrWhiteSpace(monster.Name);

    internal static IReadOnlyList<QuestBriefingTargetDto> BuildBriefingTargets(
        MonsterStaticStore store,
        IEnumerable<TargetKey> keys,
        BriefingTargetKind kind = BriefingTargetKind.Quest,
        bool isDefeated = false)
    {
        var list = new List<QuestBriefingTargetDto>();
        foreach (var key in keys)
        {
            if (list.Count >= MaxBriefingTargets)
                break;

            var mapped = ResolveStatic(store, key);
            list.Add(MonsterHudMapper.ToBriefingTarget(mapped) with
            {
                Kind = kind,
                IsDefeated = isDefeated,
            });
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

        // HunterPie monster.Id is the localization table id (e.g. 42 for Kulu-Ya-Ku), not EmType species
        // (monster_107_00). Never format it as monster_{Id:D3}_00 — that maps 42 → 冰牙龙.
        if (dto is null && key.Id >= 0)
            dto = store.FindById(key.Id.ToString());

        if (dto is null)
        {
            string fallbackName = !string.IsNullOrWhiteSpace(key.Name)
                ? key.Name!
                : $"#{key.Id}";
            bool capturable = !CaptureRules.IsKnownUncapturableSpeciesName(fallbackName);
            return MonsterHudMapper.CreateFallbackStatic(fallbackName, isCapturable: capturable);
        }

        return MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));
    }

    internal readonly record struct TargetKey(string? Name, int Id, int Occurrence = 0);

    private sealed record MonsterTracking(
        long Generation,
        string InstanceKey,
        EventHandler<EventArgs> Handler);

    private enum OverlayScene
    {
        Idle,
        Briefing,
        Combat,
    }
}
