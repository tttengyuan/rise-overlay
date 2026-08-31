using RiseOverlay.Domain;

public class PartWeaknessTests
{
    [Fact]
    public void ForPart_tie_case_uses_display_order()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 25,
            [ElementId.Water] = 25,
            [ElementId.Ice] = 25,
            [ElementId.Thunder] = 25,
            [ElementId.Dragon] = 0,
        };
        var result = PartWeakness.ForPart(values);
        Assert.Equal(
            new[] { ElementId.Fire, ElementId.Water, ElementId.Ice, ElementId.Thunder },
            result);
    }

    [Fact]
    public void ForPart_single_peak_recommends_only_that_element()
    {
        var values = new Dictionary<ElementId, int>
        {
            [ElementId.Fire] = 5,
            [ElementId.Water] = 10,
            [ElementId.Ice] = 15,
            [ElementId.Thunder] = 30,
            [ElementId.Dragon] = 0,
        };
        Assert.Equal(new[] { ElementId.Thunder }, PartWeakness.ForPart(values));
    }
}
