
using HunterPie.Core.Address.Map;
using HunterPie.Core.Client.Localization;
using HunterPie.Core.Domain;
using HunterPie.Core.Domain.Process.Entity;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game.Entity.Enemy;
using HunterPie.Core.Game.Entity.Game.Chat;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Game.Entity.Player;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Events;
using HunterPie.Core.Game.Services;
using HunterPie.Core.Native.IPC.Handlers.Internal.Damage;
using HunterPie.Core.Native.IPC.Handlers.Internal.Damage.Models;
using HunterPie.Core.Native.IPC.Models.Common;
using HunterPie.Core.Observability.Logging;
using HunterPie.Core.Scan.Service;
using HunterPie.Core.Utils;
using HunterPie.Integrations.Datasources.Common;
using HunterPie.Integrations.Datasources.Common.Entity.Game;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.Quest;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.World;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Chat;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enemy;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Enums;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game.Quest;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Player;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Services;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Utils;
using System.Text;
using CoreQuestType = HunterPie.Core.Game.Entity.Game.Quest.QuestType;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;

public sealed class MHRGame : CommonGame
{
    public const int MAXIMUM_MONSTER_ARRAY_SIZE = 5;
    public const int TRAINING_ROOM_ID = 5;

    private static readonly ILogger Logger = LoggerFactory.Create();

    private readonly MHRChat _chat = new();
    private readonly MHRPlayer _player;
    private readonly object _questTimerSync = new();
    private float _timeElapsed;
    private (int, DateTime) _lastTeleport = (0, DateTime.Now);
    private bool _hasObservedRawQuestElapsed;
    private bool _awaitingFreshRawQuestElapsed = true;
    private float _stageEntryRawQuestElapsed;
    private bool _isHudOpen;
    private DateTime _lastDamageUpdate = DateTime.MinValue;
    private nint? _lastDamageTarget;
    private readonly Dictionary<IntPtr, IMonster> _monsters = new();
    private readonly Dictionary<IntPtr, EntityDamageData[]> _damageDone = new();
    private readonly ILocalizationRepository _localizationRepository;
    private QuestState? _loggedQuestState;
    private int _loggedQuestId = int.MinValue;
    private Enums.QuestType _loggedQuestType;

    public override IPlayer Player => _player;
    public override List<IMonster> Monsters { get; } = new();

    public override IChat Chat => _chat;

    public override bool IsHudOpen
    {
        get => _isHudOpen;
        protected set
        {
            if (value != _isHudOpen)
            {
                _isHudOpen = value;
                this.Dispatch(_onHudStateChange, this);
            }
        }
    }

    public override float TimeElapsed
    {
        get => _timeElapsed;
        protected set
        {
            if (value != _timeElapsed)
            {
                bool hasReset = MHRQuestTimerRules.IsTimerReset(_timeElapsed, value);

                _timeElapsed = value;
                this.Dispatch(_onTimeElapsedChange, new TimeElapsedChangeEventArgs(hasReset, value));
            }
        }
    }

    private MHRQuest? _quest;
    public override IQuest? Quest => _quest;

    public override IAbnormalityCategorizationService AbnormalityCategorizationService { get; } = new MHRAbnormalityCategorizationService();

    public MHRGame(
        IGameProcess process,
        IScanService scanService,
        ILocalizationRepository localizationRepository
    ) : base(process, scanService)
    {
        _localizationRepository = localizationRepository;
        _player = new MHRPlayer(process, scanService);

        HookEvents();
    }

    private void HookEvents()
    {
        DamageMessageHandler.OnReceived += OnReceivePlayersDamage;
        _player.OnStageUpdate += OnPlayerStageUpdate;
    }

    public override void Dispose()
    {
        DamageMessageHandler.OnReceived -= OnReceivePlayersDamage;
        _player.OnStageUpdate -= OnPlayerStageUpdate;
        base.Dispose();
    }

    [ScannableMethod]
    private async Task ScanChat()
    {
        IntPtr chatArrayPtr = await Memory.ReadAsync(
            address: AddressMap.GetAbsolute("CHAT_ADDRESS"),
            offsets: AddressMap.GetOffsets("CHAT_OFFSETS")
        );
        IntPtr chatArray = await Memory.ReadAsync<IntPtr>(chatArrayPtr);
        int chatCount = await Memory.ReadAsync<int>(chatArrayPtr + 0x8);

        if (chatCount <= 0)
            return;

        IntPtr[] chatMessagePtrs = await Memory.ReadAsync<IntPtr>(chatArray + 0x20, chatCount);

        bool isChatOpen = false;

        for (int i = 0; i < chatCount; i++)
        {
            IntPtr messagePtr = chatMessagePtrs[i];

            MHRChatMessageStructure message = await Memory.ReadAsync<MHRChatMessageStructure>(messagePtr);

            if (message.Type is not 0 and not 1)
                continue;

            if (!isChatOpen)
                isChatOpen |= message.Visibility == 2;

            if (_chat.ContainsMessage(messagePtr))
                continue;

            MHRChatMessage messageData = await DerefChatMessageAsync(message);

            _chat.AddMessage(messagePtr, messageData);
        }

        if (!isChatOpen)
            isChatOpen |= await Memory.DerefAsync<byte>(
                address: AddressMap.GetAbsolute("CHAT_UI_ADDRESS"),
                offsets: AddressMap.GetOffsets("CHAT_UI_OFFSETS")
            ) == 1;

        _chat.SetChatState(isChatOpen);
    }

    [ScannableMethod]
    private async Task GetElapsedTime()
    {
        float elapsedTime = await Memory.DerefAsync<float>(
            address: AddressMap.GetAbsolute("QUEST_ADDRESS"),
            offsets: AddressMap.GetOffsets("QUEST_TIMER_OFFSETS")
        );

        lock (_questTimerSync)
        {
            DateTime now = DateTime.Now;
            // StageId is the only player value in this snapshot. InHuntingZone also reads
            // _stageData, which is updated separately by MHRPlayer's concurrent scan and can
            // otherwise produce an impossible old-stage/new-zone combination.
            int stageId = Player.StageId;
            bool stageChanged = EnsureTimerStage(stageId, now, elapsedTime);

            float stageElapsed = (float)(now - _lastTeleport.Item2).TotalSeconds;
            bool isHuntOrTraining = MHRQuestTimerRules.IsHuntOrTrainingStage(stageId);
            if (!isHuntOrTraining)
            {
                _hasObservedRawQuestElapsed = false;
                _awaitingFreshRawQuestElapsed = true;
                _stageEntryRawQuestElapsed = elapsedTime;
            }

            bool rawElapsedIsValid = float.IsFinite(elapsedTime) && elapsedTime > 0;
            bool needsEntrySample = _awaitingFreshRawQuestElapsed
                && !float.IsFinite(_stageEntryRawQuestElapsed);
            if (needsEntrySample)
                _stageEntryRawQuestElapsed = elapsedTime;
            bool rawChangedSinceStageEntry = rawElapsedIsValid
                && !needsEntrySample
                && (_stageEntryRawQuestElapsed <= 0
                    || Math.Abs(elapsedTime - _stageEntryRawQuestElapsed) >= 0.005f);
            bool allowRawElapsed = !_awaitingFreshRawQuestElapsed || rawChangedSinceStageEntry;

            TimeElapsed = MHRQuestTimerRules.ResolveLiveElapsed(
                rawElapsed: elapsedTime,
                previousElapsed: stageChanged ? 0 : TimeElapsed,
                currentStageElapsed: stageElapsed,
                isHuntOrTraining: isHuntOrTraining,
                hasObservedRawElapsed: _hasObservedRawQuestElapsed,
                allowRawElapsed: allowRawElapsed);
            if (isHuntOrTraining && allowRawElapsed && rawElapsedIsValid)
            {
                _awaitingFreshRawQuestElapsed = false;
                _hasObservedRawQuestElapsed = true;
            }
        }
    }

    private bool EnsureTimerStage(int stageId, DateTime now, float entryRawElapsed)
    {
        if (stageId == _lastTeleport.Item1)
            return false;

        _lastTeleport = (stageId, now);
        _hasObservedRawQuestElapsed = false;
        _awaitingFreshRawQuestElapsed = true;
        _stageEntryRawQuestElapsed = entryRawElapsed;
        return true;
    }

    private void AdvanceTimerStageFromPlayer()
    {
        lock (_questTimerSync)
        {
            int stageId = Player.StageId;
            if (EnsureTimerStage(stageId, DateTime.Now, float.NaN))
                TimeElapsed = 0;
        }
    }

    [ScannableMethod]
    private async Task GetWorldData()
    {
        IntPtr timersArrayPtr = await Memory.DerefAsync<IntPtr>(
            address: AddressMap.GetAbsolute("STAGE_MANAGER_ADDRESS"),
            offsets: AddressMap.GetOffsets("WORLD_TIME_OFFSETS")
        );
        List<MHRWorldTimeStructure> timers = Memory.ReadListOfPtrsSafeAsync<MHRWorldTimeStructure>(
           address: timersArrayPtr,
           size: 3
        ).Collect();
        MHRWorldTimeStructure lastTimer = timers.LastOrDefault(default(MHRWorldTimeStructure));

        if (lastTimer is not { Hours: <= 24 and >= 0, Minutes: <= 60 and >= 0, Seconds: <= 60 and >= 0 })
            return;

        WorldTime = new TimeOnly(lastTimer.Hours, lastTimer.Minutes, lastTimer.Seconds);
    }

    [ScannableMethod]
    private async Task GetQuest()
    {
        MHRQuestStructure questStructure = await Memory.DerefAsync<MHRQuestStructure>(
            address: AddressMap.GetAbsolute("QUEST_ADDRESS"),
            offsets: AddressMap.GetOffsets("QUEST_OFFSETS")
        );

        var questType = questStructure.Type.ToQuestType();
        bool hasQuestType = questStructure.Type != Enums.QuestType.None;
        MHRQuestData? currentQuest = null;
        if (MHRQuestScanRules.ShouldReadQuestData(hasQuestType))
        {
            MHRQuestDataStructure questData = await Memory.ReadAsync<MHRQuestDataStructure>(questStructure.QuestDataPointer);
            currentQuest = await questData.GetCurrentQuestAsync(Memory);
        }

        bool hasQuestData = (currentQuest?.Id ?? 0) > 0
            && hasQuestType;

        // Rise keeps State=Idle on the village "depart" screen after accepting;
        // Ready/InQuest appear later. Treat Idle+quest data as an accepted quest
        // so briefing can show before loading in.
        bool hasQuestStarted = questStructure.State is QuestState.InQuest or QuestState.Ready
            || (questStructure.State == QuestState.Idle && hasQuestData);

        // Do not treat a brief quest-data read miss as "over" while still InQuest/Ready —
        // that used to end+restart the quest mid-hunt and wipe native damage via Clear.
        bool isQuestOver = questStructure.State.IsQuestOver()
            || questStructure.State is QuestState.Success or QuestState.SuccessSub
            || (!hasQuestData
                && questStructure.State is not (QuestState.InQuest or QuestState.Ready));

        if (_loggedQuestState != questStructure.State
            || _loggedQuestId != (currentQuest?.Id ?? 0)
            || _loggedQuestType != questStructure.Type)
        {
            _loggedQuestState = questStructure.State;
            _loggedQuestId = currentQuest?.Id ?? 0;
            _loggedQuestType = questStructure.Type;
            Logger.Info(
                $"Rise quest scan: state={questStructure.State}({(int)questStructure.State}), id={_loggedQuestId}, rawType={questStructure.Type}, mapped={questType}, timeLimit={questStructure.TimeLimit:0}, maxDeaths={questStructure.MaxDeaths}, stage={Player.StageId}");
        }

        if (_quest is not null
            && isQuestOver)
        {
            QuestStatus endStatus = questStructure.State.ToQuestStatus();
            float endElapsed = MHRQuestTimerRules.ResolveEndElapsed(
                endStatus,
                questStructure.TimeElapsed,
                TimeElapsed);
            Logger.Info(
                $"Rise quest end timing: status={endStatus}, result={endElapsed:0.00}, structure={questStructure.TimeElapsed:0.00}, global={TimeElapsed:0.00}, drift={TimeElapsed - endElapsed:0.00}");
            this.Dispatch(_onQuestEnd, new QuestEndEventArgs(_quest, endStatus, endElapsed));
            _quest.Dispose();
            _quest = null;
        }

        // Quest swapped without a clean end (rare) — restart so briefing targets refresh.
        if (_quest is not null
            && currentQuest is { } swapped
            && swapped.Id > 0
            && swapped.Id != _quest.Id)
        {
            this.Dispatch(_onQuestEnd, new QuestEndEventArgs(_quest, QuestStatus.None, TimeElapsed));
            _quest.Dispose();
            _quest = null;
        }

        // Late-fill / refresh targets when EmType pointers become valid (anomaly + normal multi).
        if (_quest is not null && currentQuest is { } pending)
        {
            string[] filledTargets = pending.TargetMonsterRefs ?? [];
            bool idsChanged = filledTargets.Length > 0
                && !BriefingIdsEqual(_quest.BriefingMonsterIds, filledTargets);
            bool hintChanged = pending.TargetCountHint > _quest.TargetCountHint;

            if (idsChanged || hintChanged)
            {
                if (filledTargets.Length > 0)
                    _quest.ReplaceBriefingMonsterIds(filledTargets, pending.TargetCountHint);
                else if (hintChanged)
                    _quest.ReplaceBriefingMonsterIds(_quest.BriefingMonsterIds, pending.TargetCountHint);

                Logger.Info(
                    $"Rise quest targets updated id={_quest.Id} level={_quest.Level} targets=[{string.Join(", ", filledTargets)}] hint={pending.TargetCountHint}");
            }
        }

        if (_quest is null
            && hasQuestStarted
            && hasQuestData
            && currentQuest is { } quest)
        {
            _quest = new MHRQuest(
                process: Process,
                scanService: ScanService,
                id: quest.Id,
                type: questType ?? CoreQuestType.Hunt,
                level: quest.Level,
                stars: quest.Stars,
                briefingMonsterIds: quest.TargetMonsterRefs ?? [],
                targetCountHint: quest.TargetCountHint
            );

            Logger.Info(
                $"Rise OnQuestStart id={quest.Id} state={questStructure.State} targets={(quest.TargetMonsterRefs ?? []).Length} hint={quest.TargetCountHint}");
            this.Dispatch(_onQuestStart, _quest);
        }
    }

    private static bool BriefingIdsEqual(IReadOnlyList<string> current, IReadOnlyList<string> next)
    {
        if (current.Count != next.Count)
            return false;
        for (int i = 0; i < current.Count; i++)
        {
            if (!string.Equals(current[i], next[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    [ScannableMethod]
    private async Task GetPartyMembersDamage()
    {
        if ((DateTime.Now - _lastDamageUpdate).TotalMilliseconds < 100)
            return;

        _lastDamageUpdate = DateTime.Now;

        if (!Player.InHuntingZone)
        {
            _lastDamageTarget = null;
            return;
        }

        await DamageMessageHandler.RequestHuntStatisticsAsync(CommonConstants.AllTargets);

        // The custom DPS panel is current-target-only. Polling every map monster multiplies
        // IPC traffic in multi-monster quests and stores data the UI deliberately never sums.
        MHRMonster[] monsters = Monsters.OfType<MHRMonster>().ToArray();
        MHRMonster[] focused = monsters
            .Where(m => m.Target == Target.Self)
            .ToArray();
        if (focused.Length == 0)
        {
            focused = monsters
                .Where(m => m.ManualTarget == Target.Self)
                .ToArray();
        }

        IReadOnlyList<nint> targets = MHRDamagePollingRules.SelectTargets(
            focused.Select(m => m.Address).ToArray(),
            _lastDamageTarget,
            monsters.Select(m => m.Address).ToHashSet());
        if (focused.Length > 0 && targets.Count > 0)
            _lastDamageTarget = targets[^1];
        else if (targets.Count == 0)
            _lastDamageTarget = null;

        // Keep polling the sticky target through unlock/death animation so the killing
        // blow reaches the final card. A newly focused target replaces it immediately.
        foreach (nint target in targets)
            await DamageMessageHandler.RequestHuntStatisticsAsync(target);
    }

    /// <summary>
    /// Returns whether the native tracker has answered for this target, including a valid
    /// zero-damage answer. Per-target counters include damage dealt before lock-on.
    /// </summary>
    public bool TryGetDamageSnapshot(
        nint monsterAddress,
        out Dictionary<int, long> byEntityIndex,
        out long total)
    {
        byEntityIndex = new Dictionary<int, long>();
        total = 0;
        if (!_damageDone.TryGetValue(monsterAddress, out EntityDamageData[]? entities)
            || entities is null)
            return false;

        foreach (IGrouping<int, EntityDamageData> group in entities.GroupBy(e => e.Entity.Index))
        {
            long damage = (long)group.Sum(e => e.RawDamage + e.ElementalDamage);
            if (damage > 0 || group.Key >= 0)
                byEntityIndex[group.Key] = Math.Max(0, damage);
        }

        total = Math.Max(0, (long)entities.Sum(e => e.RawDamage + e.ElementalDamage));
        return true;
    }

    [ScannableMethod]
    private async Task GetUiState()
    {
        byte isHudOpen = await Memory.DerefAsync<byte>(
            address: AddressMap.GetAbsolute("MOUSE_ADDRESS"),
            offsets: AddressMap.GetOffsets("MOUSE_OFFSETS")
        );

        byte isCutsceneActive = await Memory.DerefAsync<byte>(
            address: AddressMap.GetAbsolute("EVENTCAMERA_ADDRESS"),
            offsets: AddressMap.GetOffsets("CUTSCENE_STATE_OFFSETS")
        );

        IsHudOpen = isHudOpen == 1 || isCutsceneActive != 0;
    }

    [ScannableMethod]
    private async Task GetMonstersArray()
    {
        // Only scans for monsters in hunting areas
        if (!Player.InHuntingZone && Player.StageId != TRAINING_ROOM_ID)
        {
            if (_monsters.Keys.Count <= 0)
                return;

            foreach (IntPtr mAddress in _monsters.Keys)
                HandleMonsterDespawn(mAddress);

            return;
        }

        IntPtr address = await Memory.ReadAsync(
            address: AddressMap.GetAbsolute("MONSTERS_ADDRESS"),
            offsets: AddressMap.GetOffsets("MONSTER_LIST_OFFSETS")
        );

        var monsterAddresses = (await Memory.ReadArraySafeAsync<IntPtr>(address, MAXIMUM_MONSTER_ARRAY_SIZE))
            .Where(mAddress => mAddress != IntPtr.Zero)
            .ToHashSet();

        IntPtr[] toDespawn = _monsters.Keys.Where(it => !monsterAddresses.Contains(it))
            .ToArray();

        foreach (IntPtr mAddress in toDespawn)
            HandleMonsterDespawn(mAddress);

        IntPtr[] toSpawn = monsterAddresses.Where(it => !_monsters.ContainsKey(it))
            .ToArray();

        foreach (IntPtr mAddress in toSpawn)
            await HandleMonsterSpawn(mAddress);

    }

    private async Task HandleMonsterSpawn(IntPtr monsterAddress)
    {
        if (monsterAddress.IsNullPointer() || _monsters.ContainsKey(monsterAddress))
            return;

        int monsterId = await Memory.ReadAsync<int>(monsterAddress + 0x2D4);

        nint monsterTypePtr = await Memory.ReadPtrAsync(
            address: monsterAddress,
            offsets: AddressMap.Get<int[]>("MONSTER_TYPE_OFFSETS")
        );
        int monsterType = await Memory.ReadAsync<int>(monsterTypePtr + 0x5C);

        var monster = new MHRMonster(
            process: Process,
            scanService: ScanService,
            address: monsterAddress,
            id: monsterId,
            monsterType: (MonsterType)monsterType,
            localizationRepository: _localizationRepository
        );

        _monsters.Add(monsterAddress, monster);
        Monsters.Add(monster);

        this.Dispatch(_onMonsterSpawn, monster);
    }

    private void HandleMonsterDespawn(IntPtr address)
    {
        if (_monsters[address] is not MHRMonster monster)
            return;

        _monsters.Remove(address);
        _damageDone.Remove(address);
        Monsters.Remove(monster);

        this.Dispatch(_onMonsterDespawn, monster);

        monster.Dispose();
    }

    #region Damage helpers

    private async void OnPlayerStageUpdate(object? sender, EventArgs e)
    {
        // Synchronize the timer generation before the first await. Game/player scans run in
        // parallel, so delayed timer reset here can otherwise leak the previous stage clock.
        AdvanceTimerStageFromPlayer();
        _damageDone.Clear();
        _lastDamageTarget = null;
        await DamageMessageHandler.ClearAllHuntStatisticsExceptAsync(Array.Empty<IntPtr>());
        await DamageMessageHandler.RequestHuntStatisticsAsync(CommonConstants.AllTargets);
    }

    private void OnReceivePlayersDamage(object? sender, ResponseDamageMessage e)
    {
        nint target = e.Target;

        _damageDone[target] = e.Entities;

        // Party meters use AllTargets only. Per-monster keys are for locked-target scopes —
        // summing every dict value would double-count once those are requested too.
        if (!_damageDone.TryGetValue(CommonConstants.AllTargets, out EntityDamageData[]? all)
            || all is null)
            return;

        EntityDamageData[] damages = all
            .GroupBy(entity => entity.Entity.Index)
            .Select(group =>
            {
                EntityDamageData entity = group.ElementAt(0);

                return entity with
                {
                    RawDamage = group.Sum(damage => damage.RawDamage),
                    ElementalDamage = group.Sum(damage => damage.ElementalDamage)
                };
            })
            .ToArray();

        _player.UpdatePartyMembersDamage(damages);
    }

    #endregion

    #region Chat helpers
    private async Task<MHRChatMessage> DerefChatMessageAsync(MHRChatMessageStructure message)
    {
        return message.Type switch
        {
            0x0 => await DerefNormalChatMessageAsync(message),
            _ => DerefUnknownTypeMessage()
        };
    }

    private async Task<MHRChatMessage> DerefNormalChatMessageAsync(MHRChatMessageStructure message)
    {
        int messageStringLength = await Memory.ReadAsync<int>(message.Message + 0x10);
        int messageAuthorLength = await Memory.ReadAsync<int>(message.Author + 0x10);

        string messageString = await Memory.ReadAsync(message.Message + 0x14, messageStringLength * 2, Encoding.Unicode);
        string messageAuthor = await Memory.ReadAsync(message.Author + 0x14, messageAuthorLength * 2, Encoding.Unicode);

        return new MHRChatMessage
        {
            Message = messageString,
            Author = messageAuthor,
            Type = AuthorType.Player,
            PlayerSlot = message.PlayerSlot,
        };
    }

    private static MHRChatMessage DerefUnknownTypeMessage() => new() { Type = AuthorType.None };
    #endregion
}
