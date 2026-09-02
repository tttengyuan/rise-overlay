using System.Text.Json;
using System.Text.Json.Serialization;

namespace RiseOverlay.Data;

public sealed record MonsterHitzoneDto(
    string Part,
    int? Phase,
    int Fire,
    int Water,
    int Ice,
    int Thunder,
    int Dragon);

public sealed record MonsterPartStaticDto(
    string Part,
    string? Break,
    string? Sever);

public sealed record MonsterStaticDto(
    string Id,
    string Title,
    bool Capturable,
    IReadOnlyList<MonsterHitzoneDto> Hitzones,
    IReadOnlyList<MonsterPartStaticDto> Parts);

public sealed class MonsterStaticStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IReadOnlyList<MonsterStaticDto> _monsters;
    private readonly Dictionary<string, MonsterStaticDto> _byId;
    private readonly Dictionary<string, MonsterStaticDto> _byTitle;

    private MonsterStaticStore(IReadOnlyList<MonsterStaticDto> monsters)
    {
        _monsters = monsters;
        _byId = new Dictionary<string, MonsterStaticDto>(StringComparer.OrdinalIgnoreCase);
        _byTitle = new Dictionary<string, MonsterStaticDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var monster in monsters)
        {
            _byId[monster.Id] = monster;
            if (!string.IsNullOrWhiteSpace(monster.Title) && !_byTitle.ContainsKey(monster.Title))
                _byTitle[monster.Title] = monster;
        }
    }

    public IReadOnlyList<MonsterStaticDto> All => _monsters;

    public static MonsterStaticStore LoadEmpty() => new MonsterStaticStore([]);

    public static MonsterStaticStore Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        var monsters = JsonSerializer.Deserialize<List<MonsterStaticDto>>(json, JsonOptions);
        if (monsters is null || monsters.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException($"Monster static data at '{path}' deserialized to null or empty.");
            monsters = [];
        }

        return new MonsterStaticStore(monsters);
    }

    public static string DefaultJsonPath()
        => Path.Combine(AppContext.BaseDirectory, "static", "monsters-overlay.json");

    /// <summary>
    /// HunterPie localization may differ from overlay static titles (e.g. 骚鸟 vs 搔鸟).
    /// </summary>
    private static readonly Dictionary<string, string> TitleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["骚鸟"] = "搔鸟",
        ["Rathian"] = "雌火龙",
        ["Apex Rathian"] = "霸主·雌火龙",
        ["Rathalos"] = "火龙",
        ["Apex Rathalos"] = "霸主·火龙",
        ["Khezu"] = "奇怪龙",
        ["Basarios"] = "岩龙",
        ["Diablos"] = "角龙",
        ["Apex Diablos"] = "霸主·角龙",
        ["Rajang"] = "金狮子",
        ["Kushala Daora"] = "钢龙",
        ["Chameleos"] = "霞龙",
        ["Teostra"] = "炎王龙",
        ["Tigrex"] = "轰龙",
        ["Nargacuga"] = "迅龙",
        ["Barioth"] = "冰牙龙",
        ["Barroth"] = "土砂龙",
        ["Royal Ludroth"] = "水兽",
        ["Great Baggi"] = "眠狗龙王",
        ["Zinogre"] = "雷狼龙",
        ["Apex Zinogre"] = "霸主·雷狼龙",
        ["Great Wroggi"] = "毒狗龙王",
        ["Arzuros"] = "青熊兽",
        ["Apex Arzuros"] = "霸主·青熊兽",
        ["Lagombi"] = "白兔兽",
        ["Volvidon"] = "赤甲兽",
        ["Mizutsune"] = "泡狐龙",
        ["Apex Mizutsune"] = "霸主·泡狐龙",
        ["Crimson Glow Valstrax"] = "神秘红光天彗龙",
        ["Magnamalo"] = "怨虎龙",
        ["Bishaten"] = "天狗兽",
        ["Aknosom"] = "伞鸟",
        ["Tetranadon"] = "河童蛙",
        ["Somnacanth"] = "人鱼龙",
        ["Rakna-Kadaki"] = "妃蜘蛛",
        ["Almudron"] = "泥翁龙",
        ["Wind Serpent Ibushi"] = "风神龙",
        ["Goss Harag"] = "雪鬼兽",
        ["Great Izuchi"] = "镰鼬龙王",
        ["Thunder Serpent Narwa"] = "雷神龙",
        ["Narwa the Allmother"] = "百龙渊源雷神龙",
        ["Anjanath"] = "蛮颚龙",
        ["Pukei-Pukei"] = "毒妖鸟",
        ["Kulu-Ya-Ku"] = "骚鸟",
        ["Jyuratodus"] = "泥鱼龙",
        ["Tobi-Kadachi"] = "飞雷龙",
        ["Bazelgeuse"] = "爆鳞龙",
        ["Toadversary"] = "机关蛙",
        ["Gold Rathian"] = "金火龙",
        ["Silver Rathalos"] = "银火龙",
        ["Daimyo Hermitaur"] = "大名盾蟹",
        ["Shogun Ceanataur"] = "将军镰蟹",
        ["Furious Rajang"] = "激昂金狮子",
        ["Lucent Nargacuga"] = "月迅龙",
        ["Gore Magala"] = "黑蚀龙",
        ["Shagaru Magala"] = "天廻龙",
        ["Seregios"] = "千刃龙",
        ["Astalos"] = "电龙",
        ["Violet Mizutsune"] = "焰狐龙",
        ["Scorned Magnamalo"] = "嗟怨震天怨虎龙",
        ["Blood Orange Bishaten"] = "绯天狗兽",
        ["Aurora Somnacanth"] = "冰人鱼龙",
        ["Pyre Rakna-kadaki"] = "炽妃蜘蛛",
        ["Magma Almudron"] = "熔翁龙",
        ["Seething Bezelgeuse"] = "红莲爆鳞龙",
        ["Malzeno"] = "爵银龙",
        ["Lunagaron"] = "冰狼龙",
        ["Garangolm"] = "刚缠兽",
        ["Gaismagorm"] = "冥渊龙",
        ["Espinas"] = "棘龙",
        ["Flaming Espinas"] = "棘茶龙",
        ["Risen Kushala Daora"] = "怪异克服钢龙",
        ["Risen Chameleos"] = "怪异克服霞龙",
        ["Risen Teostra"] = "怪异克服炎王龙",
        ["Risen Shagaru Magala"] = "怪异克服天廻龙",
        ["Risen Crimson Glow Valstrax"] = "怪异克服天彗龙",
        ["Primordial Malzeno"] = "原初形态爵银龙",
        ["Chaotic Gore Magala"] = "混沌黑蚀龙",
        ["Velkhana"] = "冰呪龙",
        ["Amatsu"] = "岚龙",
    };

    public MonsterStaticDto? FindById(string id)
        => _byId.TryGetValue(id, out var monster) ? monster : null;

    public MonsterStaticDto? FindByTitle(string title)
    {
        if (_byTitle.TryGetValue(title, out var monster))
            return monster;

        if (TitleAliases.TryGetValue(title, out string? canonical)
            && _byTitle.TryGetValue(canonical, out monster))
            return monster;

        return null;
    }

    public MonsterStaticDto? FindByName(string name) => FindByTitle(name);
}
