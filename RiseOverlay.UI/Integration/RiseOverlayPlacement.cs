using System.Diagnostics;
using System.Runtime.InteropServices;
using HunterPie.Core.Settings.Types;
using RiseOverlay.Domain;

namespace RiseOverlay.UI.Integration;

/// <summary>
/// Places the Rise overlay from normalized 0..1 anchors relative to the game client area,
/// so windowed / borderless / resolution changes keep the HUD inside the game window.
/// </summary>
public sealed class RiseOverlayPlacement
{
    private const double MinimumVisible = 32;
    private const double SafeInset = 18;
    private const double WidgetWidth = 300;
    private const double ConservativeWidgetHeight = 520;
    private readonly Func<Process?>? _gameProcessProvider;

    public RiseOverlayPlacement(Func<Process?>? gameProcessProvider = null)
    {
        _gameProcessProvider = gameProcessProvider;
    }

    /// <summary>Legacy HWND provider; prefer the process-based constructor.</summary>
    public RiseOverlayPlacement(Func<IntPtr> gameWindowProvider)
        : this(() =>
        {
            IntPtr hwnd = gameWindowProvider();
            if (hwnd == IntPtr.Zero)
                return null;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
                return null;
            try
            {
                return Process.GetProcessById((int)pid);
            }
            catch
            {
                return null;
            }
        })
    {
    }

    public void ApplyNormalized(Position position, double horizontal, double vertical, double scale)
    {
        OverlayScreenBounds bounds = GetGameClientBounds() ?? GetPrimaryScreenBounds();
        (position.X, position.Y) = OverlayPositionRules.ResolveNormalizedPosition(
            bounds,
            WidgetWidth,
            ConservativeWidgetHeight,
            scale,
            horizontal,
            vertical,
            SafeInset);
    }

    public void EnsureVisible(Position position, double horizontal, double vertical, double scale)
    {
        if (!IsVisible(position))
            ApplyNormalized(position, horizontal, vertical, scale);
    }

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
                double dpi = GetMonitorDpiScale(monitor);
                screens.Add(ToDipBounds(
                    info.Work.Left,
                    info.Work.Top,
                    info.Work.Right,
                    info.Work.Bottom,
                    dpi));
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

    private OverlayScreenBounds? GetGameClientBounds()
    {
        Process? process = _gameProcessProvider?.Invoke();
        if (process is null)
            return null;

        try
        {
            process.Refresh();
        }
        catch
        {
            // Process may have exited between attach and tick.
        }

        IntPtr window = ResolveGameWindow(process);
        if (window == IntPtr.Zero || !GetClientRect(window, out NativeRect client))
            return null;

        var topLeft = new NativePoint { X = client.Left, Y = client.Top };
        var bottomRight = new NativePoint { X = client.Right, Y = client.Bottom };
        if (!ClientToScreen(window, ref topLeft) || !ClientToScreen(window, ref bottomRight))
            return null;
        if (bottomRight.X <= topLeft.X || bottomRight.Y <= topLeft.Y)
            return null;

        double dpi = GetWindowDpiScale(window);
        return ToDipBounds(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y, dpi);
    }

    private static IntPtr ResolveGameWindow(Process process)
    {
        IntPtr main = IntPtr.Zero;
        try
        {
            main = process.MainWindowHandle;
        }
        catch
        {
            // ignored
        }

        if (IsUsableClient(main))
            return main;

        IntPtr best = FindLargestVisibleWindow(process.Id);
        return best != IntPtr.Zero ? best : main;
    }

    private static IntPtr FindLargestVisibleWindow(int processId)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;

        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if ((int)pid != processId)
                return true;
            if (!IsUsableClient(hwnd))
                return true;
            if (!GetClientRect(hwnd, out NativeRect client))
                return true;

            long area = (long)Math.Max(0, client.Right - client.Left) * Math.Max(0, client.Bottom - client.Top);
            if (area > bestArea)
            {
                bestArea = area;
                best = hwnd;
            }
            return true;
        }, IntPtr.Zero);

        return best;
    }

    private static bool IsUsableClient(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window) || !IsWindowVisible(window))
            return false;
        if (IsIconic(window))
            return false;
        if (!GetClientRect(window, out NativeRect client))
            return false;
        return client.Right - client.Left >= 200 && client.Bottom - client.Top >= 200;
    }

    private static OverlayScreenBounds GetPrimaryScreenBounds()
    {
        OverlayScreenBounds? primary = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info) && (info.Flags & MonitorInfoPrimary) != 0)
            {
                double dpi = GetMonitorDpiScale(monitor);
                primary = ToDipBounds(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom, dpi);
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return primary ?? GetScreenBounds()[0];
    }

    private static OverlayScreenBounds ToDipBounds(int left, int top, int right, int bottom, double dpiScale)
    {
        double scale = dpiScale <= 0 ? 1 : dpiScale;
        return new OverlayScreenBounds(left / scale, top / scale, right / scale, bottom / scale);
    }

    private static double GetWindowDpiScale(IntPtr window)
    {
        try
        {
            uint dpi = GetDpiForWindow(window);
            return dpi == 0 ? 1 : dpi / 96.0;
        }
        catch
        {
            return 1;
        }
    }

    private static double GetMonitorDpiScale(IntPtr monitor)
    {
        try
        {
            if (GetDpiForMonitor(monitor, MonitorDpiType.Effective, out uint dpiX, out _) == 0 && dpiX != 0)
                return dpiX / 96.0;
        }
        catch
        {
            // Pre-Win8.1 / missing shcore — fall through.
        }
        return 1;
    }

    private const uint MonitorInfoPrimary = 1;

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private enum MonitorDpiType
    {
        Effective = 0,
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clip,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        MonitorDpiType dpiType,
        out uint dpiX,
        out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
}
