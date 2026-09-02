using System.Text.Json;
using System.Text.Json.Serialization;

namespace RiseOverlay.Data;

public sealed record QuestStaticDto(
    int Id,
    string Title,
    IReadOnlyList<string> Monsters);

public sealed class QuestStaticStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<int, QuestStaticDto> _byId;

    private QuestStaticStore(IReadOnlyDictionary<int, QuestStaticDto> byId)
    {
        _byId = new Dictionary<int, QuestStaticDto>(byId);
    }

    public static QuestStaticStore LoadEmpty() => new(new Dictionary<int, QuestStaticDto>());

    public static QuestStaticStore Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<Dictionary<string, QuestStaticDto>>(json, JsonOptions)
                  ?? new Dictionary<string, QuestStaticDto>();

        var byId = new Dictionary<int, QuestStaticDto>();
        foreach (var (key, value) in map)
        {
            int id = value.Id > 0 ? value.Id : (int.TryParse(key, out var parsed) ? parsed : 0);
            if (id <= 0 || value.Monsters is null || value.Monsters.Count == 0)
                continue;
            byId[id] = value with { Id = id, Monsters = value.Monsters };
        }

        return new QuestStaticStore(byId);
    }

    public static string DefaultJsonPath()
        => Path.Combine(AppContext.BaseDirectory, "static", "quests-overlay.json");

    public QuestStaticDto? FindById(int id)
        => _byId.TryGetValue(id, out var quest) ? quest : null;
}
