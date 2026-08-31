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

    public MonsterStaticDto? FindById(string id)
        => _byId.TryGetValue(id, out var monster) ? monster : null;

    public MonsterStaticDto? FindByTitle(string title)
        => _byTitle.TryGetValue(title, out var monster) ? monster : null;

    public MonsterStaticDto? FindByName(string name) => FindByTitle(name);
}
