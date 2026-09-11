namespace RiseOverlay.Domain;

/// <summary>
/// Coalesces bursty model notifications into at most one queued UI render while preserving
/// a notification that arrives during the current render.
/// </summary>
public sealed class UiRefreshGate
{
    private int _scheduled;
    private int _dirty;

    /// <returns><see langword="true"/> when the caller must enqueue the render callback.</returns>
    public bool Request()
    {
        Interlocked.Exchange(ref _dirty, 1);
        return Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;
    }

    public void BeginRender() => Interlocked.Exchange(ref _dirty, 0);

    /// <returns><see langword="true"/> when one follow-up render must be enqueued.</returns>
    public bool CompleteRenderAndTryReschedule()
    {
        Interlocked.Exchange(ref _scheduled, 0);
        if (Volatile.Read(ref _dirty) == 0)
            return false;

        return Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0;
    }
}
