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
    private bool _huntSummary;
    private OverlayScene _scene = OverlayScene.Idle;
    private readonly List<TargetKey> _targetKeys = [];
    private readonly HashSet<string> _defeatedQuestTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<IMonster> _lifecycleHooked = [];
    private List<TargetKey> _lastQuestKeys = [];
    private QuestBriefingDto? _lastBriefingDto;

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
                _huntSummary = false;
                _targetKeys.Clear();
                ClearBriefingProgress();
                if (!PushBriefingDto())
                    PushPendingBriefingPlaceholder();
            }
            else if (wasActive)
            {
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

        foreach (IMonster monster in _lifecycleHooked.ToArray())
            UnhookMonsterLifecycle(monster);
        _lifecycleHooked.Clear();

        _viewModel.UIThread.BeginInvoke(() => ApplyScene(OverlayScene.Idle));
    }

    public void Dispose() => UnhookEvents();

    private void BootstrapFromCurrentState()
    {
        _questActive = _context.Game.Quest is not null;
        _targetKeys.Clear();
        MergeTargetKeysFromMonsters();
        foreach (IMonster monster in _context.Game.Monsters)
            HookMonsterLifecycle(monster);

        _viewModel.UIThread.BeginInvoke(RefreshScene);
    }

    private void OnQuestStart(object? sender, IQuest e)
        => _viewModel.UIThread.BeginInvoke(() =>
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
        => _viewModel.UIThread.BeginInvoke(() =>
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

    private void OnMonsterSpawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            RememberTarget(monster);
            HookMonsterLifecycle(monster);
            RefreshScene();
        });

    private void OnMonsterDespawn(object? sender, IMonster monster)
        => _viewModel.UIThread.BeginInvoke(() =>
        {
            // Despawn also fires on area unload — never treat it as quest completion.
            UnhookMonsterLifecycle(monster);
            RefreshScene();
        });

    private void HookMonsterLifecycle(IMonster monster)
    {
        if (!_lifecycleHooked.Add(monster))
            return;

        monster.OnDeath += OnMonsterFinished;
        monster.OnCapture += OnMonsterFinished;
    }

    private void UnhookMonsterLifecycle(IMonster monster)
    {
        if (!_lifecycleHooked.Remove(monster))
            return;

        monster.OnDeath -= OnMonsterFinished;
        monster.OnCapture -= OnMonsterFinished;
    }

    private void OnMonsterFinished(object? sender, EventArgs e)
    {
        if (sender is not IMonster monster)
            return;

        _viewModel.UIThread.BeginInvoke(() =>
        {
            MarkQuestTargetDefeated(monster);
            RefreshScene();
        });
    }

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
            bool defeated = _defeatedQuestTargets.Contains(StableKey(key));
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
        _defeatedQuestTargets.Clear();
        _lastQuestKeys = [];
        _lastBriefingDto = null;
    }

    private List<TargetKey> CollectQuestTargetKeys()
    {
        _targetKeys.Clear();
        IQuest? quest = _context.Game.Quest;

        // Memory EmTypes win only when every id resolves in the static monster table.
        // Bad probes (e.g. monster_022_00) must fall back to quests-overlay.json.
        bool memoryUsable = quest is { BriefingMonsterIds.Count: > 0 }
            && quest.BriefingMonsterIds.All(id => _staticStore.FindById(id) is not null);

        if (memoryUsable)
            MergeTargetKeysFromQuestBriefingIds();
        else
            MergeTargetKeysFromQuestStatic();

        _lastQuestKeys = _targetKeys.ToList();
        return _lastQuestKeys;
    }

    /// <summary>
    /// Mark defeated only when a dead/captured body is still scanned, or OnDeath/OnCapture fired.
    /// Missing from <see cref="IGame.Monsters"/> is normal (other area / not loaded) — not defeat.
    /// Duplicate species: assign corpses to the lowest occurrence slots first.
    /// </summary>
    private void UpdateDefeatedQuestTargets(IReadOnlyList<TargetKey> questKeys)
    {
        if (questKeys.Count == 0)
            return;

        foreach (IGrouping<string, TargetKey> group in questKeys.GroupBy(StableSpecies))
        {
            var ordered = group.OrderBy(k => k.Occurrence).ToList();
            var matches = _context.Game.Monsters
                .Where(m => IsLargeMonsterCandidate(m) && MatchesMonster(ordered[0], m))
                .ToList();

            if (matches.Count == 0)
                continue;

            int alive = matches.Count(IsMonsterAlive);
            int dead = matches.Count - alive;

            for (int i = 0; i < ordered.Count; i++)
            {
                string stable = StableKey(ordered[i]);
                if (i < dead)
                    _defeatedQuestTargets.Add(stable);
                else if (i < dead + alive)
                    _defeatedQuestTargets.Remove(stable);
            }
        }
    }

    private void MarkQuestTargetDefeated(IMonster monster)
    {
        if (!IsLargeMonsterCandidate(monster))
            return;

        IEnumerable<TargetKey> keys = _lastQuestKeys.Count > 0
            ? _lastQuestKeys
            : CollectQuestTargetKeys();

        // One corpse → first matching undefeated occurrence (FIFO for same species).
        foreach (TargetKey key in keys.OrderBy(k => k.Occurrence))
        {
            if (!MatchesMonster(key, monster))
                continue;

            string stable = StableKey(key);
            if (_defeatedQuestTargets.Contains(stable))
                continue;

            _defeatedQuestTargets.Add(stable);
            break;
        }

        if (!string.IsNullOrWhiteSpace(monster.Name))
            _defeatedQuestTargets.Add(monster.Name);
    }

    private static bool IsMonsterAlive(IMonster monster)
        => MonsterHealthDisplay.ForHud(monster.Health, monster.MaxHealth) > 0;

    private bool MatchesMonster(TargetKey key, IMonster monster)
    {
        if (!string.IsNullOrWhiteSpace(key.Name) && !string.IsNullOrWhiteSpace(monster.Name))
        {
            string keyName = _staticStore.ResolveIdentityKey(key.Name);
            string liveName = _staticStore.ResolveIdentityKey(monster.Name);

            if (string.Equals(keyName, liveName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Afflicted / variant titles sometimes prefix the species name.
            string normalizedKeyName = MonsterStaticStore.NormalizeTitle(key.Name);
            string normalizedLiveName = MonsterStaticStore.NormalizeTitle(monster.Name);
            if (normalizedLiveName.EndsWith(normalizedKeyName, StringComparison.OrdinalIgnoreCase)
                || normalizedKeyName.EndsWith(normalizedLiveName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Localization table ids sometimes match across spawn/despawn of the same species.
        return key.Id >= 0 && monster.Id >= 0 && key.Id == monster.Id;
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
    private void MergeTargetKeysFromQuestBriefingIds()
    {
        IQuest? quest = _context.Game.Quest;
        if (quest is null || quest.BriefingMonsterIds.Count == 0)
            return;

        string? title = _questStore.FindById(quest.Id)?.Title;
        IReadOnlyList<string> monsters = ExpandStaticMonstersByTitle(
            quest.BriefingMonsterIds,
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

    private enum OverlayScene
    {
        Idle,
        Briefing,
        Combat,
    }
}
