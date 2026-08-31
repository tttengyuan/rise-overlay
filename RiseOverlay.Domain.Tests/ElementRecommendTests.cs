using RiseOverlay.Domain;

public class ElementRecommendTests
{
    [Fact]
    public void Valstrax_head_all_25_except_dragon_recommends_four()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 25,
            [ElementId.Water] = 25,
            [ElementId.Ice] = 25,
            [ElementId.Thunder] = 25,
            [ElementId.Dragon] = 0,
        };
        var result = ElementRecommend.FromHitzones(values);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            result);
    }

    [Fact]
    public void Single_peak_recommends_only_that_element()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 5,
            [ElementId.Water] = 10,
            [ElementId.Ice] = 15,
            [ElementId.Thunder] = 30,
            [ElementId.Dragon] = 0,
        };
        Assert.Equal(new[] { ElementId.Thunder }, ElementRecommend.FromHitzones(values));
    }

    [Fact]
    public void All_zero_returns_empty()
    {
        var values = Enum.GetValues<ElementId>().ToDictionary(e => e, _ => 0);
        Assert.Empty(ElementRecommend.FromHitzones(values));
    }
}
