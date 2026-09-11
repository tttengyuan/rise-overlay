using RiseOverlay.Domain;

public class UiRefreshGateTests
{
    [Fact]
    public void Burst_requests_only_schedule_one_dispatcher_callback()
    {
        var gate = new UiRefreshGate();

        Assert.True(gate.Request());
        Assert.False(gate.Request());
        Assert.False(gate.Request());
    }

    [Fact]
    public void Request_during_render_is_rescheduled_after_current_render()
    {
        var gate = new UiRefreshGate();
        Assert.True(gate.Request());
        gate.BeginRender();

        Assert.False(gate.Request());

        Assert.True(gate.CompleteRenderAndTryReschedule());
    }

    [Fact]
    public void Gate_returns_to_idle_when_nothing_changed_during_render()
    {
        var gate = new UiRefreshGate();
        Assert.True(gate.Request());
        gate.BeginRender();

        Assert.False(gate.CompleteRenderAndTryReschedule());
        Assert.True(gate.Request());
    }
}
