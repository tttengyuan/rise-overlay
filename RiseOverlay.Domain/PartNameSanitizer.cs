using System.Text.RegularExpressions;

namespace RiseOverlay.Domain;

/// <summary>
/// Cleans corrupted static-data labels and bridges EN↔ZH part names for matching.
/// </summary>
public static partial class PartNameSanitizer
{
    private static readonly Dictionary<string, string> EnglishToChinese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abdomen"] = "腹部",
        ["Abdominal Iceplate"] = "冻凝壳（腰部）",
        ["Air Flinch"] = "踉跄",
        ["Antenna"] = "触角",
        ["Antlers"] = "鹿角",
        ["Arms"] = "手臂",
        ["Arms (Ice)"] = "手臂（冰）",
        ["Arms (Mud)"] = "手臂(泥土)",
        ["Back"] = "背部",
        ["Back Leg Fins"] = "后腿鱼鳍",
        ["Back Sack"] = "背部瓦斯囊",
        ["Back Windsac"] = "背部风袋",
        ["Balloon"] = "气囊",
        ["Belly"] = "腹部",
        ["Big Flinch"] = "大退缩",
        ["Body"] = "身体",
        ["Body & Legs"] = "身体和腿",
        ["Body (Ice)"] = "身体（冰）",
        ["Body (Snow)"] = "身体（雪）",
        ["Ceiling Fall"] = "悬挂掉落",
        ["Charge"] = "充电失效",
        ["Chest"] = "胸部",
        ["Chest (Rock)"] = "胸部（岩石）",
        ["Chest Windsac"] = "胸部风袋",
        ["Claw"] = "爪子",
        ["Counterattack"] = "反击",
        ["Crash"] = "坠落",
        ["Crest"] = "羽冠",
        ["Dorsal Fin"] = "背鳍",
        ["Effluvia Emissions"] = "排气",
        ["Elemental Explosion"] = "力量抑制",
        ["Emerge from Snow (Body)"] = "出雪（身体）",
        ["Emerge from Snow (Head)"] = "出雪（头）",
        ["Emerge from Snow (Tail)"] = "出雪（尾巴）",
        ["Exhaust Organ (Central)"] = "排气管（中）",
        ["Exhaust Organ (Crater)"] = "排气管 (坑)",
        ["Exhaust Organ (Head)"] = "排气管（头）",
        ["Exhaust Organ (Rear)"] = "排气管（后）",
        ["Feign Death"] = "假死",
        ["Fin"] = "鳍",
        ["Fire State"] = "火焰状态",
        ["Foreleg"] = "前肢",
        ["Forelegs"] = "前腿",
        ["Front Left Arm"] = "第一左触手",
        ["Front Left Arm (Fire)"] = "第一左触手（火）",
        ["Front Left Arm (Oil)"] = "第一左触手（油）",
        ["Front Right Arm"] = "第一右触手",
        ["Front Right Arm (Fire)"] = "第一右触手（火）",
        ["Front Right Arm (Oil)"] = "第一右触手（油）",
        ["Glowing Head"] = "发光头",
        ["Glowing Tail"] = "发光尾巴",
        ["Golden Horns"] = "金角",
        ["Golden Left Arm"] = "黄金左臂",
        ["Golden Left Chest"] = "黄金左胸",
        ["Golden Left Leg"] = "黄金左腿",
        ["Golden Mane"] = "金鬃毛",
        ["Golden Right Arm"] = "黄金右臂",
        ["Golden Right Chest"] = "黄金右胸",
        ["Golden Right Leg"] = "黄金右腿",
        ["Golden Tail (Left)"] = "黄金尾巴（左）",
        ["Golden Tail (Right)"] = "黄金尾巴（右）",
        ["Head"] = "头部",
        ["Head (Crystalized)"] = "头部（白缠晶）",
        ["Head (Ice)"] = "头部（冰）",
        ["Head (Mud)"] = "头部(泥土)",
        ["Head (Rock)"] = "头部（岩石）",
        ["Head (Snow)"] = "头部（雪）",
        ["Head Sack"] = "头部瓦斯囊",
        ["Hind Leg"] = "后肢",
        ["Hind Legs"] = "后腿",
        ["Horn"] = "角",
        ["Horns"] = "角",
        ["Horns #2"] = "角 #2",
        ["Inflated Tail"] = "充气尾巴",
        ["Jaw"] = "颚",
        ["Knockdown"] = "击倒",
        ["Left Arm"] = "左臂",
        ["Left Arm (Ice)"] = "左臂(冰)",
        ["Left Arm (Rock)"] = "左臂（岩石）",
        ["Left Bone"] = "左骨",
        ["Left Chainblade"] = "左锁翼刃",
        ["Left Claw"] = "左爪",
        ["Left Cutwing"] = "左刃翼",
        ["Left Foreleg"] = "左前肢",
        ["Left Foreleg (Reinf.)"] = "左前肢（粘有附着物）",
        ["Left Hind Foreleg"] = "左后肢",
        ["Left Hind Leg"] = "左后肢",
        ["Left Leg"] = "左腿",
        ["Left Leg (Mud)"] = "左腿(泥土)",
        ["Left Legs"] = "左腿",
        ["Left Limbs"] = "左肢",
        ["Left Weak Shell"] = "左弱壳",
        ["Left Wing"] = "左翼",
        ["Left Wing (Rock)"] = "左翼（岩石）",
        ["Left Wingarm"] = "左翼足",
        ["Left Wingarm (Crystalized)"] = "左翼臂（白缠晶）",
        ["Left Wingarm (Exposed)"] = "左翼臂 (暴蚀化)",
        ["Legs"] = "腿",
        ["Lg. Iceplate (Exposed)"] = "大冻凝壳（露出后）",
        ["Lg. Iceplate (Hidden)"] = "大冻凝壳（露出前）",
        ["Limbs"] = "四肢",
        ["Lower Back"] = "下背部",
        ["Lower Body"] = "下半身",
        ["Lower Torso"] = "下躯干",
        ["Mane"] = "鬃毛",
        ["Mantle"] = "外套膜",
        ["Membrane"] = "腕间膜",
        ["Middle Left Arm"] = "第二左触手",
        ["Middle Left Arm (Fire)"] = "第二左触手（火）",
        ["Middle Left Arm (Oil)"] = "第二左触手（油）",
        ["Middle Right Arm"] = "第二右触手",
        ["Middle Right Arm (Fire)"] = "第二右触手（火）",
        ["Middle Right Arm (Oil)"] = "第二右触手（油）",
        ["Mouth"] = "嘴巴",
        ["Mud Ball"] = "泥土球",
        ["Neck"] = "颈部",
        ["Neck Left (Rock)"] = "左颈（岩石）",
        ["Neck Right (Rock)"] = "右颈（岩石）",
        ["Nose"] = "鼻子",
        ["Petals"] = "花瓣状部位",
        ["Pot"] = "茶釜",
        ["Qurio Threshold"] = "啮生虫",
        ["Rage"] = "斗气硬化失效",
        ["Rear"] = "臀部",
        ["Rear Left Arm"] = "第三左触手",
        ["Rear Left Arm (Fire)"] = "第三左触手（火）",
        ["Rear Left Arm (Oil)"] = "第三左触手（油）",
        ["Rear Power Unit"] = "背部组",
        ["Rear Right Arm"] = "第三右触手",
        ["Rear Right Arm (Fire)"] = "第三右触手（火）",
        ["Rear Right Arm (Oil)"] = "第三右触手（油）",
        ["Repel"] = "击退",
        ["Right Arm"] = "右臂",
        ["Right Arm (Ice)"] = "右臂(冰)",
        ["Right Arm (Rock)"] = "右臂（岩石）",
        ["Right Bone"] = "右骨",
        ["Right Chainblade"] = "右锁翼刃",
        ["Right Claw"] = "右爪",
        ["Right Cutwing"] = "右刃翼",
        ["Right Foreleg"] = "右前肢",
        ["Right Foreleg (Reinf.)"] = "右前肢（粘有附着物）",
        ["Right Hind Foreleg"] = "右后肢",
        ["Right Hind Leg"] = "右后肢",
        ["Right Leg"] = "右腿",
        ["Right Leg (Mud)"] = "右腿(泥土)",
        ["Right Legs"] = "右腿",
        ["Right Limbs"] = "右肢",
        ["Right Weak Shell"] = "右弱壳",
        ["Right Wing"] = "右翼",
        ["Right Wing (Rock)"] = "右翼（岩石）",
        ["Right Wingarm"] = "右翼足",
        ["Right Wingarm (Crystalized)"] = "右翼臂（白缠晶）",
        ["Right Wingarm (Exposed)"] = "右翼臂 (暴蚀化)",
        ["Rock"] = "石头",
        ["Rolling"] = "滚动",
        ["Sack Collapse"] = "瓦斯囊崩塌",
        ["Shell"] = "壳",
        ["Silver Spikes (Head)"] = "银尖刺（头）",
        ["Silver Spikes (L. Arm)"] = "银尖刺（左臂）",
        ["Silver Spikes (L. Wing)"] = "银尖刺 (左翼)",
        ["Silver Spikes (R. Arm)"] = "银尖刺（右臂）",
        ["Silver Spikes (R. Wing)"] = "银尖刺 (右翼)",
        ["Sky Fall"] = "坠落",
        ["Sponge"] = "海绵质",
        ["Stinger"] = "毒刺",
        ["Superheat"] = "高温状态",
        ["Tail"] = "尾巴",
        ["Tail (Mud)"] = "尾巴(泥土)",
        ["Tail (Rock)"] = "尾巴（岩石）",
        ["Tail (Snow)"] = "尾巴（雪）",
        ["Tail Hair"] = "尾毛",
        ["Tail Sack"] = "尾部瓦斯囊",
        ["Tail Tip"] = "尾巴尖",
        ["Tail Windsac"] = "尾部风袋",
        ["Tentacle"] = "触手",
        ["Throat"] = "喉部",
        ["Thunderballs"] = "雷电球",
        ["Tongue"] = "舌头",
        ["Torso"] = "躯干",
        ["Torso (Mud)"] = "躯干(泥土)",
        ["Upper Back"] = "上背部",
        ["Upper Body"] = "上半身",
        ["Veil"] = "水膜",
        ["Veil: Head (Left)"] = "水膜（头部左侧）",
        ["Veil: Head (Right)"] = "水膜（头部右侧）",
        ["Veil: Left Foreleg"] = "水膜（左前脚）",
        ["Veil: Right Foreleg"] = "水膜（右前脚）",
        ["Veil: Tail (Left)"] = "水膜（尾巴左侧）",
        ["Veil: Tail (Middle)"] = "水膜（尾巴）",
        ["Veil: Tail (Right)"] = "水膜（尾巴右侧）",
        ["Veil: Torso (Left)"] = "水膜（身体左侧）",
        ["Veil: Torso (Right)"] = "水膜（身体右侧）",
        ["Wingclaw"] = "翼爪",
        ["Wings"] = "翼",
        ["Wings (Ice)"] = "翼（冰）",
        ["Wing"] = "翅膀",
        ["Cutwing"] = "刃翼",
    };

    public static string Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var s = name.Trim();
        if (s.StartsWith("RabbitConverted - ", StringComparison.OrdinalIgnoreCase))
            s = s["RabbitConverted - ".Length..].Trim();
        else if (s.StartsWith("RabbitConverted-", StringComparison.OrdinalIgnoreCase))
            s = s["RabbitConverted-".Length..].Trim();
        else if (string.Equals(s, "RabbitConverted", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        // Strip export prefixes like 【亜種】后脚 / 【強化版】尾尾 (variant hitzone labels).
        var bracketPrefix = BracketPrefixRegex().Match(s);
        if (bracketPrefix.Success)
            s = bracketPrefix.Groups[1].Value.Trim();

        // "部位00　头" / "部位03　尾尾" → last segment after ideographic/ascii space
        var partPrefix = PartPrefixRegex().Match(s);
        if (partPrefix.Success)
            s = partPrefix.Groups[1].Value.Trim();

        // Collapse duplicated syllables from bad converters: 尾尾→尾, 头头→头
        if (s.Length == 2 && s[0] == s[1])
            s = s[..1];

        if (EnglishToChinese.TryGetValue(s, out var zh))
            return zh;

        return s;
    }

    public static string NormalizeKey(string? name)
    {
        var cleaned = Clean(name);
        if (cleaned.Length == 0)
            return string.Empty;

        // Match "头部" ↔ "头", "左刃翼" ↔ "右刃"
        if (cleaned.EndsWith('部') && cleaned.Length >= 2)
            cleaned = cleaned[..^1];

        return cleaned;
    }

    public static bool Matches(string? a, string? b)
    {
        var ka = NormalizeKey(a);
        var kb = NormalizeKey(b);
        if (ka.Length == 0 || kb.Length == 0)
            return false;
        if (string.Equals(ka, kb, StringComparison.OrdinalIgnoreCase))
            return true;
        return ka.Contains(kb, StringComparison.OrdinalIgnoreCase)
               || kb.Contains(ka, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^部位\d+\s*[　\s]+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PartPrefixRegex();

    [GeneratedRegex(@"^【[^】]+】\s*(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketPrefixRegex();
}
