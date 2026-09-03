using RiseOverlay.Domain;

public class PartRowSyncKeyTests
{
    [Fact]
    public void For_distinguishes_breakable_and_qurio_same_name()
    {
        string breakable = PartRowSyncKey.For("右前肢", isQurio: false, isQurioThreshold: false);
        string qurio = PartRowSyncKey.For("右前肢", isQurio: true, isQurioThreshold: false);
        string threshold = PartRowSyncKey.For("啮生虫", isQurio: true, isQurioThreshold: true);

        Assert.NotEqual(breakable, qurio);
        Assert.NotEqual(qurio, threshold);

        // Name-only dictionary would collapse these — keys must stay unique.
        var map = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [breakable] = 2135,
            [qurio] = 400,
            [threshold] = 12000,
        };
        Assert.Equal(3, map.Count);
        Assert.Equal(400, map[qurio]);
    }
}
