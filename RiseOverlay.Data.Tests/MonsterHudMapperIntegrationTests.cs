using RiseOverlay.Data;
using RiseOverlay.Domain;

public class MonsterHudMapperIntegrationTests
{
    private static string ResolveJsonPath()
    {
        var fromOutput = MonsterStaticStore.DefaultJsonPath();
        if (File.Exists(fromOutput))
            return fromOutput;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "RiseOverlay.Data", "static", "monsters-overlay.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("monsters-overlay.json not found relative to test output.");
    }

    [Fact]
    public void BuildFromStatic_real_Valstrax_json_recommends_four_not_capturable()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var dto = store.FindByTitle("神秘红光天彗龙");
        Assert.NotNull(dto);

        var mapped = MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));

        Assert.False(mapped.IsCapturable);
        Assert.Null(mapped.CaptureThresholdPercent);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            mapped.Recommended);
    }

    [Fact]
    public void BuildFromStatic_real_Magnamalo_json_is_capturable()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var dto = store.FindByTitle("怨虎龙");
        Assert.NotNull(dto);

        var mapped = MonsterHudMapper.BuildFromStatic(MonsterStaticAdapter.ToSnapshot(dto));

        Assert.True(mapped.IsCapturable);
        Assert.Equal(25, mapped.CaptureThresholdPercent);
        Assert.Contains(ElementId.Water, mapped.Recommended);
    }
}
