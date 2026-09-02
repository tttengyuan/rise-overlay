using System.Runtime.InteropServices;
using HunterPie.Core.Domain.Process.Entity;
using HunterPie.Core.Settings.Types;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Remembers screen-space widget position across restarts (via config Position).
/// Only auto-snaps to the game top-left when Position is still the factory default.
/// While running, follows game-window moves by delta so a drag stays put relative to the game.
/// </summary>
public sealed class RiseOverlayPlacement
{
    private const double DefaultConfigX = 20;
    private const double DefaultConfigY = 20;
    private const double InitialOffsetX = 16;
    private const double InitialOffsetY = 48;

    /// <summary>
    /// Minimized / iconic Win32 windows often report Left/Top near -32000.
    /// Following that delta flings the overlay off-screen (e.g. Position 32767,32490).
    /// </summary>
    private const int MinValidScreenCoord = -10_000;
    private const int MaxValidScreenCoord = 50_000;
    private const int MinWindowSize = 64;

    private int _lastGameLeft;
    private int _lastGameTop;
    private bool _anchored;

    /// <summary>
    /// Restore saved Position, or snap once if still at factory default (20,20).
    /// Also rescues positions that have drifted off every monitor.
    /// </summary>
    public void RestoreOrSnapInitial(IGameProcess process, Position position)
    {
        if (!TryGetGameRect(process, out RECT rect))
            return;

        _lastGameLeft = rect.Left;
        _lastGameTop = rect.Top;
        _anchored = true;

        if (IsFactoryDefault(position) || IsOffScreen(position))
        {
            position.X = rect.Left + InitialOffsetX;
            position.Y = rect.Top + InitialOffsetY;
        }
    }

    /// <summary>
    /// If the game window moved, shift the widget by the same delta.
    /// Never re-snaps to top-left (preserves drag + saved position).
    /// Ignores minimized / invalid window rects so the overlay is not flung away.
    /// </summary>
    public void FollowGameWindowIfMoved(IGameProcess process, Position position)
    {
        if (!TryGetGameRect(process, out RECT rect))
            return;

        if (!_anchored)
        {
            _lastGameLeft = rect.Left;
            _lastGameTop = rect.Top;
            _anchored = true;
            return;
        }

        int dx = rect.Left - _lastGameLeft;
        int dy = rect.Top - _lastGameTop;
        _lastGameLeft = rect.Left;
        _lastGameTop = rect.Top;

        if (dx == 0 && dy == 0)
            return;

        // Guard against minimize/restore jumps (±32000-ish).
        if (Math.Abs(dx) > 5000 || Math.Abs(dy) > 5000)
            return;

        position.X += dx;
        position.Y += dy;

        if (IsOffScreen(position))
        {
            position.X = rect.Left + InitialOffsetX;
            position.Y = rect.Top + InitialOffsetY;
        }
    }

    private static bool IsFactoryDefault(Position position)
        => Math.Abs(position.X - DefaultConfigX) < 0.5
           && Math.Abs(position.Y - DefaultConfigY) < 0.5;

    private static bool IsOffScreen(Position position)
        => position.X < MinValidScreenCoord
           || position.Y < MinValidScreenCoord
           || position.X > MaxValidScreenCoord
           || position.Y > MaxValidScreenCoord;

    private static bool TryGetGameRect(IGameProcess process, out RECT rect)
    {
        rect = default;
        IntPtr hwnd = process.SystemProcess.MainWindowHandle;
        if (hwnd == IntPtr.Zero)
            return false;

        if (!GetWindowRect(hwnd, out rect))
            return false;

        // Minimized / cloaked windows: discard so we do not update the anchor.
        if (IsIconic(hwnd))
            return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width < MinWindowSize || height < MinWindowSize)
            return false;

        if (rect.Left < MinValidScreenCoord || rect.Top < MinValidScreenCoord
            || rect.Left > MaxValidScreenCoord || rect.Top > MaxValidScreenCoord)
            return false;

        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
