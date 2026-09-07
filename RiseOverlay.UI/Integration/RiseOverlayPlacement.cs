using System.Runtime.InteropServices;
using HunterPie.Core.Settings.Types;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Keeps the HunterPie widget at the user's saved absolute screen position.
/// Unlike the former game-window delta follower, this never adds transient game-window
/// movement to the saved coordinates; it only rescues a genuinely off-screen position.
/// </summary>
public sealed class RiseOverlayPlacement
{
    private const double DefaultConfigX = 20;
    private const double DefaultConfigY = 20;
    private const double MinimumVisible = 32;
    private const double SafeInset = 20;

    public void RestoreOrSnapInitial(Position position)
    {
        if (IsFactoryDefault(position) || !IsVisible(position))
            MoveToSafePrimaryPosition(position);
    }

    /// <summary>
    /// Periodic safety check for resolution/DPI/display changes. It never follows the game
    /// window and therefore cannot accumulate position drift.
    /// </summary>
    public void EnsureVisible(Position position)
    {
        if (!IsVisible(position))
            MoveToSafePrimaryPosition(position);
    }

    private static bool IsFactoryDefault(Position position)
        => Math.Abs(position.X - DefaultConfigX) < 0.5
           && Math.Abs(position.Y - DefaultConfigY) < 0.5;

    private static bool IsVisible(Position position)
        => OverlayPositionRules.IsSufficientlyVisible(
            position.X,
            position.Y,
            GetScreenBounds(),
            MinimumVisible);

    private static OverlayScreenBounds[] GetScreenBounds()
    {
        var screens = new List<OverlayScreenBounds>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                screens.Add(new OverlayScreenBounds(
                    info.Work.Left,
                    info.Work.Top,
                    info.Work.Right,
                    info.Work.Bottom));
            }
            return true;
        }, IntPtr.Zero);

        if (screens.Count == 0)
        {
            screens.Add(new OverlayScreenBounds(
                System.Windows.SystemParameters.VirtualScreenLeft,
                System.Windows.SystemParameters.VirtualScreenTop,
                System.Windows.SystemParameters.VirtualScreenLeft + System.Windows.SystemParameters.VirtualScreenWidth,
                System.Windows.SystemParameters.VirtualScreenTop + System.Windows.SystemParameters.VirtualScreenHeight));
        }
        return screens.ToArray();
    }

    private static void MoveToSafePrimaryPosition(Position position)
    {
        OverlayScreenBounds primary = GetPrimaryScreenBounds();
        position.X = primary.Left + SafeInset;
        position.Y = primary.Top + SafeInset;
    }

    private static OverlayScreenBounds GetPrimaryScreenBounds()
    {
        OverlayScreenBounds? primary = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info) && (info.Flags & MonitorInfoPrimary) != 0)
            {
                primary = new OverlayScreenBounds(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return primary ?? GetScreenBounds()[0];
    }

    private const uint MonitorInfoPrimary = 1;

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clip,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
