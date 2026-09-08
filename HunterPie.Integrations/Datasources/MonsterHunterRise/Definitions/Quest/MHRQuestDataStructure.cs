using HunterPie.Core.Domain.Memory;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Observability.Logging;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Entity.Game;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.Quest;

[StructLayout(LayoutKind.Explicit)]
public struct MHRQuestDataStructure
{
    private static readonly ILogger Logger = LoggerFactory.Create();
    private static int _lastDumpedQuestId;
    private static string? _lastLoggedTargetSignature;
    private const bool ProbeDumpDiagnosticsEnabled = false;

    /// <summary>
    /// Candidate offsets for <c>_BossEmType</c> (System.Array&lt;EmTypes&gt; pointer).
    /// Investigation layout is typically length 7: [t0..tN-1][0][intrusion][0].
    /// </summary>
    private static readonly int[] BossEmTypePointerOffsets =
    [
        0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x58, 0x60, 0x68, 0x70,
        0x78, 0x80, 0x88, 0x90, 0x98, 0xA0, 0xA8, 0xB0, 0xB8, 0xC0,
    ];

    /// <summary>
    /// Candidate offsets for <c>_HuntTargetNum</c>. Authoritative is +0x38 (see PreferStoredHuntTargetNum).
    /// </summary>
    private static readonly int[] HuntTargetNumOffsets =
        [0x1C, 0x20, 0x24, 0x28, 0x2C, 0x30, 0x34, 0x38];

    [FieldOffset(0x10)] public IntPtr NormalQuestPointer;
    [FieldOffset(0x28)] public IntPtr AnomalyQuestPointer;

    public async Task<MHRQuestData?> GetCurrentQuestAsync(IMemoryAsync memory)
    {
        // Prefer anomaly investigation when both pointers are set — NormalQuestPointer
        // can linger and would otherwise skip BossEmType / HuntTargetNum entirely.
        if (AnomalyQuestPointer != IntPtr.Zero)
        {
            MHRAnomalyQuestDataStructure anomalyQuest =
                await memory.ReadAsync<MHRAnomalyQuestDataStructure>(AnomalyQuestPointer);

            if (anomalyQuest.Id is > 0 and <= 999_999)
            {
                (string[] targets, int huntHint) =
                    await TryReadBossEmTypeTargetsAsync(memory, AnomalyQuestPointer, anomalyQuest.Id, "anomaly");

                return new MHRQuestData
                {
                    Id = anomalyQuest.Id,
                    Level = QuestLevel.Anomaly,
                    Stars = anomalyQuest.Level,
                    TargetMonsterRefs = targets,
                    TargetCountHint = Math.Max(huntHint, targets.Length)
                };
            }
        }

        if (NormalQuestPointer == IntPtr.Zero)
            return null;

        MHRNormalQuestDataStructure normalQuest =
            await memory.ReadAsync<MHRNormalQuestDataStructure>(NormalQuestPointer);

        // Garbage NormalQuestPointer often yields huge / nonsense ids.
        if (normalQuest.Id is <= 0 or > 999_999)
            return null;

        (string[] normalTargets, int normalHint) =
            await TryReadBossEmTypeTargetsAsync(memory, NormalQuestPointer, normalQuest.Id, "normal");

        return new MHRQuestData
        {
            Id = normalQuest.Id,
            Level = normalQuest.Rank.ToQuestLevel(),
            Stars = normalQuest.Stars + 1,
            TargetMonsterRefs = normalTargets,
            TargetCountHint = Math.Max(normalHint, normalTargets.Length)
        };
    }

    private static async Task<(string[] Refs, int HuntHint)> TryReadBossEmTypeTargetsAsync(
        IMemoryAsync memory,
        IntPtr questPtr,
        int questId,
        string kind)
    {
        Dictionary<int, int> huntHints = await ReadHuntHintsAsync(memory, questPtr);
        // Anomaly: +0x38 = HuntTargetNum. Normal: +0x28 is Rank — never treat as hunt count.
        int? storedHuntGlobal = kind == "anomaly"
            ? PreferStoredHuntTargetNum(huntHints)
            : null;

        var dump = new StringBuilder();
        dump.AppendLine($"kind={kind} quest={questId} ptr=0x{questPtr.ToInt64():X}");
        dump.AppendLine(await FormatFieldScanAsync(memory, questPtr));
        dump.AppendLine($"huntHints=[{string.Join(", ", huntHints.Select(kv => $"+0x{kv.Key:X}={kv.Value}"))}]");

        // Normal fixed quests: tgt_em_type (often +0x50) + leading repeats in boss_em_type (+0x68).
        // Quest 10616 dump: +0x50=0x6D, +0x68=[0x6D,0x6D,...] → two 飞雷龙 (not +0x40=0x4 岩龙).
        if (kind == "normal")
        {
            (string[] normalResolved, int normalHint, string how) =
                await TryResolveNormalBossTargetsAsync(memory, questPtr);
            if (normalResolved.Length > 0)
            {
                dump.AppendLine($"normalResolve={how} refs=[{string.Join(", ", normalResolved)}]");
                if (MHRQuestScanRules.ShouldWriteProbeDump(ProbeDumpDiagnosticsEnabled)
                    && questId != _lastDumpedQuestId
                    && questId > 0)
                {
                    _lastDumpedQuestId = questId;
                    TryWriteDump(questId, dump.ToString());
                    Logger.Info($"Rise {kind} quest probe dump written for quest {questId}");
                }

                LogResolvedTargetsOnce(kind, questId, how, normalResolved, normalHint);
                return (normalResolved, Math.Max(normalHint, normalResolved.Length));
            }
        }

        string[] best = [];
        int bestScore = int.MinValue;
        int bestOffset = -1;
        int bestHunt = 0;

        foreach (int offset in BossEmTypePointerOffsets)
        {
            IntPtr arrayPtr = await memory.ReadAsync<IntPtr>(questPtr + offset);
            if (arrayPtr == IntPtr.Zero)
                continue;

            int[] emTypes;
            try
            {
                int arrayLen = await memory.ReadAsync<int>(arrayPtr + 0x1C);
                if (arrayLen is < 1 or > 16)
                    continue;

                emTypes = await memory.ReadArraySafeAsync<int>(arrayPtr, arrayLen);
            }
            catch
            {
                continue;
            }

            if (emTypes.Length == 0)
                continue;

            int consecutive = InferConsecutiveHuntCount(emTypes);
            bool investigationShape = LooksLikeInvestigationBossArray(emTypes, consecutive);
            int? storedHunt = storedHuntGlobal;
            int huntNum = ResolveHuntForArray(consecutive, storedHunt);

            string raw = string.Join(",", emTypes.Select(e => $"0x{e:X}"));
            dump.AppendLine(
                $"+0x{offset:X} len={emTypes.Length} consec={consecutive} shape={investigationShape} storedHunt={storedHunt?.ToString() ?? "-"} useHunt={huntNum} raw=[{raw}]");

            if (huntNum < 1)
                continue;

            string[] refs = SelectTargetRefsByIndex(emTypes, huntNum);
            if (refs.Length == 0)
                continue;

            int score = ScoreCandidate(
                kind, offset, emTypes, consecutive, huntNum, refs.Length, investigationShape, storedHunt);

            // Prefer packed short boss lists for normal quests (only after known EmTypes filter).
            if (kind == "normal" && emTypes.Length is >= 1 and <= 4 && consecutive >= 1)
                score += 15;

            dump.AppendLine($"  → score={score} refs=[{string.Join(", ", refs)}]");

            if (score > bestScore)
            {
                bestScore = score;
                best = refs;
                bestOffset = offset;
                bestHunt = huntNum;
            }
        }

        // Fixed tgt_em_type[2] style: two consecutive EmTypes embedded as i32 fields (not a System.Array).
        if (kind == "normal")
        {
            foreach (int offset in new[] { 0x40, 0x44, 0x48, 0x4C, 0x50, 0x54, 0x58, 0x5C, 0x60 })
            {
                int em0 = await memory.ReadAsync<int>(questPtr + offset);
                int em1 = await memory.ReadAsync<int>(questPtr + offset + 4);
                if (!RiseEmTypes.IsLargeMonsterEmType(em0))
                    continue;

                var packed = new List<int> { em0 };
                if (RiseEmTypes.IsLargeMonsterEmType(em1))
                    packed.Add(em1);

                int consecutive = packed.Count;
                int huntNum = consecutive;
                string[] refs = SelectTargetRefsByIndex(packed, huntNum);
                if (refs.Length == 0)
                    continue;

                int score = 20 + refs.Length * 10;
                dump.AppendLine(
                    $"+0x{offset:X} fixedPair consec={consecutive} useHunt={huntNum} → score={score} refs=[{string.Join(", ", refs)}]");

                if (score > bestScore)
                {
                    bestScore = score;
                    best = refs;
                    bestOffset = offset;
                    bestHunt = huntNum;
                }
            }
        }

        if (MHRQuestScanRules.ShouldWriteProbeDump(ProbeDumpDiagnosticsEnabled)
            && questId != _lastDumpedQuestId
            && questId > 0)
        {
            _lastDumpedQuestId = questId;
            TryWriteDump(questId, dump.ToString());
            Logger.Info($"Rise {kind} quest probe dump written for quest {questId}");
        }

        // Reject low-confidence matches (e.g. score=30 single filler that used to win).
        const int MinAcceptScore = 80;
        int huntHint = bestHunt > 0 ? bestHunt : storedHuntGlobal ?? 0;

        if (best.Length > 0 && bestScore >= MinAcceptScore)
        {
            LogResolvedTargetsOnce(
                kind,
                questId,
                $"BossEmType@+0x{bestOffset:X}/score={bestScore}",
                best,
                Math.Max(huntHint, best.Length));
            return (best, Math.Max(huntHint, best.Length));
        }

        if (best.Length > 0)
            Logger.Debug(
                $"Rise {kind} quest {questId}: rejected probe score={bestScore} refs=[{string.Join(", ", best)}]");

        return ([], huntHint);
    }

    private static void LogResolvedTargetsOnce(
        string kind,
        int questId,
        string source,
        IReadOnlyList<string> targets,
        int huntHint)
    {
        string signature = $"{kind}:{questId}:{source}:{huntHint}:{string.Join(',', targets)}";
        if (!MHRQuestScanRules.ShouldLogTargetResolution(_lastLoggedTargetSignature, signature))
            return;

        _lastLoggedTargetSignature = signature;
        Logger.Info(
            $"Rise {kind} quest {questId}: targets [{string.Join(", ", targets)}] huntHint={huntHint} via {source}");
    }

    private static async Task<Dictionary<int, int>> ReadHuntHintsAsync(IMemoryAsync memory, IntPtr anomalyPtr)
    {
        var map = new Dictionary<int, int>();
        foreach (int off in HuntTargetNumOffsets)
        {
            int n = await memory.ReadAsync<int>(anomalyPtr + off);
            if (n is >= 1 and <= 4)
                map[off] = n;
        }

        return map;
    }

    /// <summary>Raw i32 scan used to map QuestLife / TimeLimit / HuntTargetNum against the quest board UI.</summary>
    private static async Task<string> FormatFieldScanAsync(IMemoryAsync memory, IntPtr anomalyPtr)
    {
        var parts = new List<string>(14);
        for (int off = 0x10; off <= 0x44; off += 4)
        {
            int v = await memory.ReadAsync<int>(anomalyPtr + off);
            parts.Add($"+0x{off:X}={v}");
        }

        return $"fields=[{string.Join(", ", parts)}]";
    }

    /// <summary>
    /// Packed hunt targets start at index 0 and stop at the first empty/invalid slot.
    /// Do NOT use "last valid index" — that pulls intrusion/garbage into the count.
    /// </summary>
    private static int InferConsecutiveHuntCount(IReadOnlyList<int> emTypes)
    {
        int count = 0;
        int limit = Math.Min(4, emTypes.Count);
        for (int i = 0; i < limit; i++)
        {
            if (!RiseEmTypes.IsLargeMonsterEmType(emTypes[i]))
                break;
            count++;
        }

        return count;
    }

    private static bool LooksLikeInvestigationBossArray(IReadOnlyList<int> emTypes, int consecutive)
    {
        if (consecutive < 1)
            return false;

        // Classic RandomMystery layout: 5–8 slots, pad at index 4.
        if (emTypes.Count is >= 5 and <= 8)
        {
            if (emTypes.Count > 4 && emTypes[4] != 0)
                return false;
            return true;
        }

        // Short arrays that are pure packed targets (no intrusion slot).
        return emTypes.Count is >= 1 and <= 4 && consecutive == emTypes.Count;
    }

    /// <summary>
    /// Prefer <c>_HuntTargetNum</c> at +0x38.
    /// Confirmed single-target UI「讨伐1头」:
    /// - 700056 怨虎龙: +0x38=1 ✓ (+0x24=1 coincidentally)
    /// - 700065 雷狼龙: +0x38=1 ✓ (+0x24=3 wrongly showed 3 — that looks like QuestLife)
    /// - 700068: +0x38=1 ✓
    /// BossEmType still holds filler EmTypes past that count — never use consecutive alone.
    /// </summary>
    private static int? PreferStoredHuntTargetNum(IReadOnlyDictionary<int, int> huntHints)
    {
        if (huntHints.TryGetValue(0x38, out int at38) && at38 is >= 1 and <= 4)
            return at38;

        // Fallbacks. Skip +0x24 (QuestLife) and +0x20 (often over-counts appearances).
        foreach (int off in new[] { 0x1C, 0x2C, 0x28, 0x30, 0x34 })
        {
            if (huntHints.TryGetValue(off, out int n) && n is >= 1 and <= 4)
                return n;
        }

        return null;
    }

    private static int ResolveHuntForArray(int consecutive, int? storedHunt)
    {
        if (consecutive < 1)
            return 0;

        // Trust stored HuntTargetNum; clamp to packed prefix so we never invent past valid EmTypes.
        if (storedHunt is int s)
            return Math.Clamp(s, 1, consecutive);

        return consecutive;
    }

    /// <summary>
    /// Normal quest layout (from dumps): short tgt slot often at +0x50, boss_em_type System.Array at +0x68.
    /// Hunt count = leading boss entries matching the tgt EmType (e.g. two 飞雷龙).
    /// </summary>
    private static async Task<(string[] Refs, int Hint, string How)> TryResolveNormalBossTargetsAsync(
        IMemoryAsync memory,
        IntPtr questPtr)
    {
        int[]? boss = await TryReadEmTypeArrayAsync(memory, questPtr + 0x68);
        if (boss is null || boss.Length == 0)
            boss = await TryReadEmTypeArrayAsync(memory, questPtr + 0x70);

        int? tgt = null;
        foreach (int off in new[] { 0x50, 0x48, 0x58, 0x60 })
        {
            int[]? shortArr = await TryReadEmTypeArrayAsync(memory, questPtr + off);
            if (shortArr is null || shortArr.Length == 0)
                continue;
            if (!RiseEmTypes.IsLargeMonsterEmType(shortArr[0]))
                continue;
            // Prefer a clear single-target slot (second empty / invalid).
            if (shortArr.Length >= 2 && RiseEmTypes.IsLargeMonsterEmType(shortArr[1]))
                continue;
            tgt = shortArr[0];
            break;
        }

        if (boss is null || boss.Length == 0)
            return ([], 0, "");

        if (tgt is int targetEm)
        {
            int n = 0;
            int limit = Math.Min(4, boss.Length);
            for (int i = 0; i < limit; i++)
            {
                if (boss[i] != targetEm)
                    break;
                n++;
            }

            if (n >= 1)
            {
                string? id = RiseEmTypes.ToMonsterStaticId(targetEm);
                if (id is not null)
                {
                    var refs = Enumerable.Repeat(id, n).ToArray();
                    return (refs, n, $"tgt+boss n={n}");
                }
            }
        }

        // Multi-species: take packed prefix of known EmTypes up to first gap (max 4).
        int consec = InferConsecutiveHuntCount(boss);
        if (consec is >= 1 and <= 3)
        {
            string[] refs = SelectTargetRefsByIndex(boss, consec);
            if (refs.Length > 0)
                return (refs, refs.Length, $"bossPrefix n={consec}");
        }

        return ([], 0, "");
    }

    private static async Task<int[]?> TryReadEmTypeArrayAsync(IMemoryAsync memory, IntPtr fieldPtr)
    {
        IntPtr arrayPtr = await memory.ReadAsync<IntPtr>(fieldPtr);
        if (arrayPtr == IntPtr.Zero)
            return null;

        try
        {
            int arrayLen = await memory.ReadAsync<int>(arrayPtr + 0x1C);
            if (arrayLen is < 1 or > 16)
                return null;
            return await memory.ReadArraySafeAsync<int>(arrayPtr, arrayLen);
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreCandidate(
        string kind,
        int offset,
        IReadOnlyList<int> emTypes,
        int consecutive,
        int huntNum,
        int resolvedCount,
        bool investigationShape,
        int? storedHunt)
    {
        if (resolvedCount <= 0 || consecutive < 1)
            return int.MinValue;

        int score = 0;

        if (kind == "anomaly")
        {
            // Probe dumps: BossEmType pointer is stably at +0x40 for investigation quests.
            if (offset == 0x40)
                score += 60;
            else if (offset is 0x38 or 0x48)
                score -= 20;
        }
        else
        {
            // Normal: +0x40 is often icon/junk (10616: 0x4 → 岩龙). Prefer tgt/boss slots.
            if (offset == 0x40)
                score -= 80;
            if (offset == 0x50)
                score += 40;
            if (offset is 0x68 or 0x70)
                score += 50;
        }

        if (investigationShape)
            score += 40;
        else if (emTypes.Count is >= 1 and <= 4)
            score += 10;
        else
            score -= 30;

        if (emTypes.Count >= 5 && emTypes[4] == 0)
            score += 15;
        if (emTypes.Count >= 7 && emTypes[6] == 0)
            score += 8;

        if (storedHunt is int sh)
        {
            if (huntNum == sh)
                score += 100;
            else
                score -= 80;
        }
        else
        {
            score += resolvedCount * 5;
        }

        if (resolvedCount == huntNum)
            score += 15;

        return score;
    }

    /// <summary>
    /// Keep duplicate species — multi-target hunts often list the same EmType twice.
    /// </summary>
    private static string[] SelectTargetRefsByIndex(IReadOnlyList<int> emTypes, int huntNum)
    {
        var refs = new List<string>(huntNum);
        int limit = Math.Min(huntNum, emTypes.Count);
        for (int i = 0; i < limit; i++)
        {
            string? id = RiseEmTypes.ToMonsterStaticId(emTypes[i]);
            if (id is null)
                continue;
            refs.Add(id);
        }

        return refs.ToArray();
    }

    private static void TryWriteDump(int questId, string body)
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"anomaly-briefing-{questId}.txt");
            File.WriteAllText(path, body, Encoding.UTF8);
        }
        catch
        {
            // best-effort diagnostics only
        }
    }
}
