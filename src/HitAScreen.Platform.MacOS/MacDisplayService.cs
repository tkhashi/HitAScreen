using System.Runtime.InteropServices;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacDisplayService : IDisplayService
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const uint MaxDisplays = 16;

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return Array.Empty<DisplayInfo>();
        }

        var ids = new uint[MaxDisplays];
        var error = CGGetActiveDisplayList(MaxDisplays, ids, out var count);
        if (error != 0 || count == 0)
        {
            return Array.Empty<DisplayInfo>();
        }

        var mainDisplay = CGMainDisplayID();
        var result = new List<DisplayInfo>((int)count);

        for (var index = 0; index < count; index++)
        {
            var id = ids[index];
            var bounds = CGDisplayBounds(id);
            var scale = GetDisplayScale(id);

            result.Add(new DisplayInfo(
                id.ToString(),
                new ScreenRect(bounds.Origin.X, bounds.Origin.Y, bounds.Size.Width, bounds.Size.Height),
                scale,
                id == mainDisplay));
        }

        return result;
    }

    public DisplayInfo? GetDisplayById(string displayId)
    {
        var displays = GetDisplays();
        return displays.FirstOrDefault(display => string.Equals(display.Id, displayId, StringComparison.Ordinal));
    }

    public DisplayInfo? GetDisplayContainingPoint(ScreenPoint point)
    {
        var displays = GetDisplays();
        return displays.FirstOrDefault(display => display.Bounds.Contains(point));
    }

    public ScreenPoint GetCursorPosition()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new ScreenPoint(0, 0);
        }

        var ev = CGEventCreate(IntPtr.Zero);
        if (ev == IntPtr.Zero)
        {
            return new ScreenPoint(0, 0);
        }

        try
        {
            var point = CGEventGetLocation(ev);
            return new ScreenPoint(point.X, point.Y);
        }
        finally
        {
            CFRelease(ev);
        }
    }

    private static double GetDisplayScale(uint displayId)
    {
        var mode = CGDisplayCopyDisplayMode(displayId);
        if (mode == IntPtr.Zero)
        {
            return 1.0;
        }

        try
        {
            var logicalWidth = CGDisplayModeGetWidth(mode);
            var pixelWidth = CGDisplayModeGetPixelWidth(mode);
            if (logicalWidth <= 0 || pixelWidth <= 0)
            {
                return 1.0;
            }

            return Math.Max(1.0, (double)pixelWidth / logicalWidth);
        }
        finally
        {
            CFRelease(mode);
        }
    }

    [DllImport(CoreGraphics)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] activeDisplays, out uint displayCount);

    [DllImport(CoreGraphics)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphics)]
    private static extern CGRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGDisplayCopyDisplayMode(uint display);

    [DllImport(CoreGraphics)]
    private static extern nuint CGDisplayModeGetWidth(IntPtr mode);

    [DllImport(CoreGraphics)]
    private static extern nuint CGDisplayModeGetPixelWidth(IntPtr mode);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    private static extern CGPoint CGEventGetLocation(IntPtr cgEvent);

    [DllImport(CoreGraphics)]
    private static extern void CFRelease(IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }
}
