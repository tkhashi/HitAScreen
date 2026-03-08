using System.Runtime.InteropServices;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacInputInjectionService : IInputInjectionService
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private const uint KcgEventLeftMouseDown = 1;
    private const uint KcgEventLeftMouseUp = 2;
    private const uint KcgEventRightMouseDown = 3;
    private const uint KcgEventRightMouseUp = 4;
    private const uint KcgEventMouseMoved = 5;
    private const uint KcgMouseButtonLeft = 0;
    private const uint KcgMouseButtonRight = 1;
    private const uint KcgHidEventTap = 0;
    private const int KcgMouseEventClickState = 1;

    public void Execute(UiActionType action, UiCandidate candidate)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var point = new CGPoint
        {
            X = candidate.Bounds.X + (candidate.Bounds.Width / 2),
            Y = candidate.Bounds.Y + (candidate.Bounds.Height / 2)
        };

        switch (action)
        {
            case UiActionType.LeftClick:
                Click(point, KcgMouseButtonLeft, 1);
                break;
            case UiActionType.RightClick:
                Click(point, KcgMouseButtonRight, 1);
                break;
            case UiActionType.DoubleClick:
                Click(point, KcgMouseButtonLeft, 2);
                break;
            case UiActionType.Focus:
                PostMouseEvent(KcgEventMouseMoved, point, 0, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported action.");
        }
    }

    private static void Click(CGPoint point, uint button, int count)
    {
        var down = button == KcgMouseButtonLeft ? KcgEventLeftMouseDown : KcgEventRightMouseDown;
        var up = button == KcgMouseButtonLeft ? KcgEventLeftMouseUp : KcgEventRightMouseUp;

        PostMouseEvent(KcgEventMouseMoved, point, 0, 0);

        for (var index = 1; index <= count; index++)
        {
            PostMouseEvent(down, point, button, index);
            PostMouseEvent(up, point, button, index);
            if (index < count)
            {
                Thread.Sleep(10);
            }
        }
    }

    private static void PostMouseEvent(uint type, CGPoint point, uint button, int clickState)
    {
        var ev = CGEventCreateMouseEvent(IntPtr.Zero, type, point, button);
        if (ev == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (clickState > 0)
            {
                CGEventSetIntegerValueField(ev, KcgMouseEventClickState, clickState);
            }

            CGEventPost(KcgHidEventTap, ev);
        }
        finally
        {
            CFRelease(ev);
        }
    }

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(CoreGraphics)]
    private static extern void CGEventPost(uint tap, IntPtr @event);

    [DllImport(CoreGraphics)]
    private static extern void CGEventSetIntegerValueField(IntPtr @event, int field, long value);

    [DllImport(CoreGraphics)]
    private static extern void CFRelease(IntPtr cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }
}
