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
    public void FindByTitle_resolves_kulu_localization_alias()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        var byCanonical = store.FindByTitle("搔鸟");
        Assert.NotNull(byCanonical);
        Assert.Equal("monster_107_00", byCanonical.Id);

        // HunterPie zh-cn uses 骚鸟 for the same monster.
        var byAlias = store.FindByTitle("骚鸟");
        Assert.NotNull(byAlias);
        Assert.Same(byCanonical, byAlias);

        var byEnglishName = store.FindByTitle("Kulu-Ya-Ku");
        Assert.NotNull(byEnglishName);
        Assert.Same(byCanonical, byEnglishName);
        Assert.Equal("搔鸟", byEnglishName.Title);
        Assert.Equal("monster_107_00", store.ResolveIdentityKey("骚鸟"));
        Assert.Equal("monster_107_00", store.ResolveIdentityKey("Kulu-Ya-Ku"));
    }

    [Theory]
    [InlineData("monster_099_05", "百龙渊源雷神龙")]
    [InlineData("monster_072_00", "天廻龙")]
    [InlineData("monster_072_08", "怪异克服天廻龙")]
    [InlineData("monster_135_00", "冥渊龙")]
    public void Elder_and_final_boss_species_are_not_capturable(string id, string title)
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());

        var monster = store.FindById(id);

        Assert.NotNull(monster);
        Assert.Equal(title, monster.Title);
        Assert.False(monster.Capturable);
    }

    [Fact]
    public void FindByTitle_normalizes_middle_dot_variants_for_apex_monsters()
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());

        var fromLiveLocalization = store.FindByTitle("霸主·青熊兽");
        var fromStaticData = store.FindByTitle("霸主・青熊兽");

        Assert.NotNull(fromLiveLocalization);
        Assert.Same(fromStaticData, fromLiveLocalization);
        Assert.Equal("monster_060_07", fromLiveLocalization.Id);
        Assert.False(fromLiveLocalization.Capturable);
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

    [Theory]
    [InlineData("棘龙", "棘龙")]
    [InlineData("棘茶龙", "棘茶龙")]
    [InlineData("Espinas", "棘龙")]
    [InlineData("Flaming Espinas", "棘茶龙")]
    public void MatchesSpecies_accepts_same_species_aliases(string left, string right)
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        Assert.True(store.MatchesSpecies(left, right));
        Assert.True(store.MatchesSpecies(right, left));
    }

    [Theory]
    [InlineData("棘龙", "棘茶龙")]
    [InlineData("Espinas", "Flaming Espinas")]
    [InlineData("棘龙", "Flaming Espinas")]
    [InlineData("Espinas", "棘茶龙")]
    public void MatchesSpecies_rejects_related_but_distinct_variants(string left, string right)
    {
        var store = MonsterStaticStore.Load(ResolveJsonPath());
        Assert.False(store.MatchesSpecies(left, right));
        Assert.False(store.MatchesSpecies(right, left));
    }
}
