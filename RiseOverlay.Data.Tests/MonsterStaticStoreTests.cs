using RiseOverlay.Data;

public class MonsterStaticStoreTests
{
    private static string ResolveJsonPath()
    {
        var fromOutput = MonsterStaticStore.DefaultJsonPath();
        if (File.Exists(fromOutput))
            return fromOutput;

        // Fallback when running from source tree without copied content.
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
    public void Load_does_not_throw()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        Assert.NotEmpty(store.All);
    }

    [Fact]
    public void FindByTitle_finds_magnamalo()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var monster = store.FindByTitle("怨虎龙");
        Assert.NotNull(monster);
        Assert.Equal("monster_089_00", monster.Id);
        Assert.True(monster.Capturable);
    }

    [Fact]
    public void FindByTitle_finds_valstrax_variant_not_capturable()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var monster = store.FindByTitle("神秘红光天彗龙");
        Assert.NotNull(monster);
        Assert.False(monster.Capturable);
        Assert.NotEmpty(monster.Hitzones);
    }

    [Fact]
    public void FindById_returns_same_entry()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var byTitle = store.FindByTitle("怨虎龙");
        Assert.NotNull(byTitle);
        var byId = store.FindById(byTitle.Id);
        Assert.Same(byTitle, byId);
    }
}
