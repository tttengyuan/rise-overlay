using HunterPie.Core.Domain.Memory;
using HunterPie.Core.Game.Entity.Game.Quest;
using HunterPie.Core.Observability.Logging;
using HunterPie.Integrations.Datasources.MonsterHunterRise.Utils;
using System.Runtime.InteropServices;
using System.Text;

namespace HunterPie.Integrations.Datasources.MonsterHunterRise.Definitions.Quest;

[StructLayout(LayoutKind.Explicit)]
public struct MHRQuestDataStructure
{
    private static readonly ILogger Logger = LoggerFactory.Create();
    private static int _lastDumpedQuestId;

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
        if (NormalQuestPointer != IntPtr.Zero)
        {
            MHRNormalQuestDataStructure normalQuest = await memory.ReadAsync<MHRNormalQuestDataStructure>(NormalQuestPointer);

            return new MHRQuestData
            {
                Id = normalQuest.Id,
                Level = normalQuest.Rank.ToQuestLevel(),
                Stars = normalQuest.Stars + 1,
                TargetMonsterRefs = []
            };
        }

        if (AnomalyQuestPointer == IntPtr.Zero)
            return null;

        MHRAnomalyQuestDataStructure anomalyQuest = await memory.ReadAsync<MHRAnomalyQuestDataStructure>(AnomalyQuestPointer);
        string[] targets = await TryReadAnomalyTargetRefsAsync(memory, AnomalyQuestPointer, anomalyQuest.Id);

        if (targets.Length > 0)
            Logger.Info($"Rise anomaly quest {anomalyQuest.Id}: targets [{string.Join(", ", targets)}]");
        else
            Logger.Debug($"Rise anomaly quest {anomalyQuest.Id}: no BossEmType targets resolved yet");

        return new MHRQuestData
        {
            Id = anomalyQuest.Id,
            Level = QuestLevel.Anomaly,
            Stars = anomalyQuest.Level,
            TargetMonsterRefs = targets
        };
    }

    private static async Task<string[]> TryReadAnomalyTargetRefsAsync(
        IMemoryAsync memory,
        IntPtr anomalyPtr,
        int questId)
    {
        Dictionary<int, int> huntHints = await ReadHuntHintsAsync(memory, anomalyPtr);

        string[] best = [];
        int bestScore = int.MinValue;
        int bestOffset = -1;
        int bestHunt = 0;
        var dump = new StringBuilder();
        dump.AppendLine($"quest={questId} ptr=0x{anomalyPtr.ToInt64():X}");
        dump.AppendLine(await FormatFieldScanAsync(memory, anomalyPtr));
        dump.AppendLine($"huntHints=[{string.Join(", ", huntHints.Select(kv => $"+0x{kv.Key:X}={kv.Value}"))}]");

        foreach (int offset in BossEmTypePointerOffsets)
        {
            IntPtr arrayPtr = await memory.ReadAsync<IntPtr>(anomalyPtr + offset);
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
            int? storedHunt = PreferStoredHuntTargetNum(huntHints);
            int huntNum = ResolveHuntForArray(consecutive, storedHunt);

            string raw = string.Join(",", emTypes.Select(e => $"0x{e:X}"));
            dump.AppendLine(
                $"+0x{offset:X} len={emTypes.Length} consec={consecutive} shape={investigationShape} storedHunt={storedHunt?.ToString() ?? "-"} useHunt={huntNum} raw=[{raw}]");

            if (huntNum < 1)
                continue;

            string[] refs = SelectTargetRefsByIndex(emTypes, huntNum);
            int score = ScoreCandidate(
                offset, emTypes, consecutive, huntNum, refs.Length, investigationShape, storedHunt);

            dump.AppendLine($"  → score={score} refs=[{string.Join(", ", refs)}]");

            if (score > bestScore)
            {
                bestScore = score;
                best = refs;
                bestOffset = offset;
                bestHunt = huntNum;
            }
        }

        if (questId != _lastDumpedQuestId && questId > 0)
        {
            _lastDumpedQuestId = questId;
            TryWriteDump(questId, dump.ToString());
            Logger.Info($"Rise anomaly probe dump written for quest {questId}");
        }

        if (best.Length > 0)
        {
            Logger.Info(
                $"Rise anomaly BossEmType @+0x{bestOffset:X} hunt={bestHunt} score={bestScore} → [{string.Join(", ", best)}]");
            return best;
        }

        return [];
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

    private static int ScoreCandidate(
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

        // Probe dumps: BossEmType pointer is stably at +0x40 for investigation quests.
        if (offset == 0x40)
            score += 60;
        else if (offset is 0x38 or 0x48)
            score -= 20; // common false-positive neighbors

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

        // Match HuntTargetNum — this is what stops single-target quests from showing 3 rows.
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

    private static string[] SelectTargetRefsByIndex(IReadOnlyList<int> emTypes, int huntNum)
    {
        var refs = new List<string>(huntNum);
        int limit = Math.Min(huntNum, emTypes.Count);
        for (int i = 0; i < limit; i++)
        {
            string? id = RiseEmTypes.ToMonsterStaticId(emTypes[i]);
            if (id is null)
                continue;
            if (refs.Contains(id, StringComparer.OrdinalIgnoreCase))
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
