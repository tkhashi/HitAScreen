using System.Runtime.InteropServices;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacAccessibilityCandidateProvider : IAccessibilityElementProvider
{
    private const uint Utf8Encoding = 0x08000100;
    private const int DlopenLazy = 1;
    private const int MaxAxNodeCount = 4000;
    private const double MessagingTimeoutSeconds = 2.0;

    private static readonly object CfBooleanSync = new();
    private static readonly HashSet<string> ClickableRoles = new(StringComparer.Ordinal)
    {
        "AXButton",
        "AXLink",
        "AXMenuItem",
        "AXTextField",
        "AXCheckBox",
        "AXRadioButton",
        "AXPopUpButton",
        "AXDisclosureTriangle"
    };

    private static readonly HashSet<string> BrowserLikeRoles = new(StringComparer.Ordinal)
    {
        "AXStaticText",
        "AXLink",
        "AXTextField",
        "AXTextArea",
        "AXHeading",
        "AXImage",
        "AXList",
        "AXListItem",
        "AXRow",
        "AXCell",
        "AXButton"
    };

    private static readonly HashSet<string> ActionableActions = new(StringComparer.Ordinal)
    {
        "AXPress",
        "AXConfirm",
        "AXOpen",
        "AXPick",
        "AXShowMenu"
    };

    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Safari",
        "Google Chrome",
        "Chromium",
        "Microsoft Edge",
        "Brave Browser",
        "Vivaldi",
        "Firefox",
        "Arc",
        "Opera"
    };

    private static IntPtr _kCfBooleanTrue;

    public IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context)
    {
        if (!OperatingSystem.IsMacOS() || context.ActiveWindow.ProcessId <= 0)
        {
            return Array.Empty<UiCandidate>();
        }

        if (!NativeMethods.AXIsProcessTrusted())
        {
            return Array.Empty<UiCandidate>();
        }

        var appElement = NativeMethods.AXUIElementCreateApplication(context.ActiveWindow.ProcessId);
        if (appElement == IntPtr.Zero)
        {
            return Array.Empty<UiCandidate>();
        }

        try
        {
            NativeMethods.AXUIElementSetMessagingTimeout(appElement, MessagingTimeoutSeconds);
            TryEnableEnhancedUserInterface(appElement);

            var targetWindow = FindTargetWindowElement(appElement, context.ActiveWindow);
            if (targetWindow == IntPtr.Zero)
            {
                return Array.Empty<UiCandidate>();
            }

            return EnumerateCandidates(
                targetWindow,
                context.TargetBounds,
                IsBrowserProcessName(context.ActiveWindow.ProcessName));
        }
        finally
        {
            NativeMethods.CFRelease(appElement);
        }
    }

    private static void TryEnableEnhancedUserInterface(IntPtr appElement)
    {
        try
        {
            var trueValue = GetCfBooleanTrue();
            if (trueValue == IntPtr.Zero)
            {
                return;
            }

            var attributeRef = CreateCfString("AXEnhancedUserInterface");
            if (attributeRef == IntPtr.Zero)
            {
                return;
            }

            try
            {
                _ = NativeMethods.AXUIElementSetAttributeValue(appElement, attributeRef, trueValue);
            }
            finally
            {
                NativeMethods.CFRelease(attributeRef);
            }
        }
        catch
        {
            // 失敗しても従来動作を継続する。
        }
    }

    private static IntPtr GetCfBooleanTrue()
    {
        if (_kCfBooleanTrue != IntPtr.Zero)
        {
            return _kCfBooleanTrue;
        }

        lock (CfBooleanSync)
        {
            if (_kCfBooleanTrue != IntPtr.Zero)
            {
                return _kCfBooleanTrue;
            }

            try
            {
                var handle = NativeMethods.dlopen(NativeMethods.CoreFoundation, DlopenLazy);
                if (handle == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                var symbol = NativeMethods.dlsym(handle, "kCFBooleanTrue");
                if (symbol == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                _kCfBooleanTrue = Marshal.ReadIntPtr(symbol);
            }
            catch
            {
                return IntPtr.Zero;
            }

            return _kCfBooleanTrue;
        }
    }

    private static IntPtr FindTargetWindowElement(IntPtr appElement, ActiveWindowContext target)
    {
        var windows = CopyAttributeArray(appElement, "AXWindows");
        foreach (var windowElement in windows)
        {
            var title = CopyAttributeString(windowElement, "AXTitle");
            var (x, y) = CopyAttributePosition(windowElement, "AXPosition");

            var titleMatched = string.Equals(title, target.WindowTitle, StringComparison.Ordinal);
            var near = Math.Abs(x - target.Bounds.X) < 60 && Math.Abs(y - target.Bounds.Y) < 60;
            if (titleMatched && near)
            {
                foreach (var other in windows)
                {
                    if (other != windowElement)
                    {
                        NativeMethods.CFRelease(other);
                    }
                }

                return windowElement;
            }
        }

        var focused = CopyAttributeElement(appElement, "AXFocusedWindow");
        if (focused != IntPtr.Zero)
        {
            foreach (var item in windows)
            {
                NativeMethods.CFRelease(item);
            }

            return focused;
        }

        if (windows.Count > 0)
        {
            var first = windows[0];
            for (var index = 1; index < windows.Count; index++)
            {
                NativeMethods.CFRelease(windows[index]);
            }

            return first;
        }

        return IntPtr.Zero;
    }

    private static IReadOnlyList<UiCandidate> EnumerateCandidates(
        IntPtr root,
        ScreenRect targetBounds,
        bool browserLikeProcess)
    {
        var results = new List<UiCandidate>();
        var queue = new Queue<(IntPtr Element, int Depth)>();
        queue.Enqueue((root, 0));

        var nodeIndex = 0;

        while (queue.Count > 0 && nodeIndex < MaxAxNodeCount)
        {
            var (element, depth) = queue.Dequeue();
            nodeIndex++;

            try
            {
                var role = CopyAttributeString(element, "AXRole");
                var subrole = CopyAttributeString(element, "AXSubrole");
                var title = CopyAttributeString(element, "AXTitle");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = CopyAttributeString(element, "AXDescription");
                }

                var actions = CopyActionNames(element);
                var (x, y) = CopyAttributePositionNullable(element, "AXPosition");
                var (w, h) = CopyAttributeSizeNullable(element, "AXSize");
                if (!x.HasValue || !y.HasValue || !w.HasValue || !h.HasValue)
                {
                    var (fx, fy, fw, fh) = CopyAttributeFrameNullable(element, "AXFrame");
                    x ??= fx;
                    y ??= fy;
                    w ??= fw;
                    h ??= fh;
                }

                var bounds = x.HasValue && y.HasValue && w.HasValue && h.HasValue
                    ? new ScreenRect(x.Value, y.Value, w.Value, h.Value)
                    : default;

                var hasBounds = w.GetValueOrDefault() > 2 && h.GetValueOrDefault() > 2;
                var hasActionableAction = HasActionableAction(actions);
                var hasAnyAction = !string.IsNullOrWhiteSpace(actions);
                var roleActionable = ClickableRoles.Contains(role)
                    || hasActionableAction
                    || (browserLikeProcess && (BrowserLikeRoles.Contains(role) || hasAnyAction));
                var isActionable = hasBounds && roleActionable;

                if (isActionable && bounds.Intersects(targetBounds))
                {
                    results.Add(new UiCandidate(
                        CandidateId: $"ax-{depth}-{results.Count}",
                        Bounds: bounds,
                        Role: role,
                        Subrole: subrole,
                        Title: title,
                        Actions: actions,
                        Confidence: 0.95,
                        Source: CandidateSource.Accessibility));
                }

                var deduplicatedChildren = new HashSet<nint>();
                EnqueueChildren(element, "AXChildren", depth + 1, queue, deduplicatedChildren);
                EnqueueChildren(element, "AXVisibleChildren", depth + 1, queue, deduplicatedChildren);
                EnqueueChildren(element, "AXContents", depth + 1, queue, deduplicatedChildren);
            }
            finally
            {
                NativeMethods.CFRelease(element);
            }
        }

        return results;
    }

    private static void EnqueueChildren(
        IntPtr element,
        string attribute,
        int depth,
        Queue<(IntPtr Element, int Depth)> queue,
        HashSet<nint> deduplicatedChildren)
    {
        var children = CopyAttributeArray(element, attribute);
        foreach (var child in children)
        {
            if (deduplicatedChildren.Add(child))
            {
                queue.Enqueue((child, depth));
            }
            else
            {
                NativeMethods.CFRelease(child);
            }
        }
    }

    private static List<IntPtr> CopyAttributeArray(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            if (NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) != NativeMethods.AXError.Success || valueRef == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                var count = NativeMethods.CFArrayGetCount(valueRef);
                var list = new List<IntPtr>((int)count);
                for (nint index = 0; index < count; index++)
                {
                    var item = NativeMethods.CFArrayGetValueAtIndex(valueRef, index);
                    if (item == IntPtr.Zero)
                    {
                        continue;
                    }

                    NativeMethods.CFRetain(item);
                    list.Add(item);
                }

                return list;
            }
            finally
            {
                NativeMethods.CFRelease(valueRef);
            }
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static IntPtr CopyAttributeElement(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            return NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) == NativeMethods.AXError.Success
                ? valueRef
                : IntPtr.Zero;
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static string CopyAttributeString(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            if (NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) != NativeMethods.AXError.Success || valueRef == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return CfStringToManaged(valueRef);
            }
            finally
            {
                NativeMethods.CFRelease(valueRef);
            }
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static (double X, double Y) CopyAttributePosition(IntPtr element, string attribute)
    {
        var (x, y) = CopyAttributePositionNullable(element, attribute);
        return (x ?? 0, y ?? 0);
    }

    private static (double? X, double? Y) CopyAttributePositionNullable(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            if (NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) != NativeMethods.AXError.Success || valueRef == IntPtr.Zero)
            {
                return (null, null);
            }

            try
            {
                var point = new NativeMethods.CgPoint();
                return NativeMethods.AXValueGetValue(valueRef, NativeMethods.AXValueType.CgPoint, ref point)
                    ? (point.X, point.Y)
                    : (null, null);
            }
            finally
            {
                NativeMethods.CFRelease(valueRef);
            }
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static (double? Width, double? Height) CopyAttributeSizeNullable(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            if (NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) != NativeMethods.AXError.Success || valueRef == IntPtr.Zero)
            {
                return (null, null);
            }

            try
            {
                var size = new NativeMethods.CgSize();
                return NativeMethods.AXValueGetValue(valueRef, NativeMethods.AXValueType.CgSize, ref size)
                    ? (size.Width, size.Height)
                    : (null, null);
            }
            finally
            {
                NativeMethods.CFRelease(valueRef);
            }
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static (double? X, double? Y, double? Width, double? Height) CopyAttributeFrameNullable(IntPtr element, string attribute)
    {
        var attrRef = CreateCfString(attribute);
        try
        {
            if (NativeMethods.AXUIElementCopyAttributeValue(element, attrRef, out var valueRef) != NativeMethods.AXError.Success || valueRef == IntPtr.Zero)
            {
                return (null, null, null, null);
            }

            try
            {
                var rect = new NativeMethods.CgRect();
                if (!NativeMethods.AXValueGetValue(valueRef, NativeMethods.AXValueType.CgRect, ref rect))
                {
                    return (null, null, null, null);
                }

                return (rect.Origin.X, rect.Origin.Y, rect.Size.Width, rect.Size.Height);
            }
            finally
            {
                NativeMethods.CFRelease(valueRef);
            }
        }
        finally
        {
            NativeMethods.CFRelease(attrRef);
        }
    }

    private static string CopyActionNames(IntPtr element)
    {
        if (NativeMethods.AXUIElementCopyActionNames(element, out var actRef) != NativeMethods.AXError.Success || actRef == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var count = NativeMethods.CFArrayGetCount(actRef);
            var list = new List<string>((int)count);
            for (nint index = 0; index < count; index++)
            {
                var item = NativeMethods.CFArrayGetValueAtIndex(actRef, index);
                if (item != IntPtr.Zero)
                {
                    list.Add(CfStringToManaged(item));
                }
            }

            return string.Join(", ", list);
        }
        finally
        {
            NativeMethods.CFRelease(actRef);
        }
    }

    private static bool HasActionableAction(string actions)
    {
        if (string.IsNullOrWhiteSpace(actions))
        {
            return false;
        }

        return actions
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(action => ActionableActions.Contains(action));
    }

    private static bool IsBrowserProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return BrowserProcessNames.Contains(processName);
    }

    private static IntPtr CreateCfString(string value) => NativeMethods.CFStringCreateWithCString(IntPtr.Zero, value, Utf8Encoding);

    private static string CfStringToManaged(IntPtr cf)
    {
        if (cf == IntPtr.Zero)
        {
            return string.Empty;
        }

        var len = NativeMethods.CFStringGetLength(cf);
        if (len <= 0)
        {
            return string.Empty;
        }

        var size = NativeMethods.CFStringGetMaximumSizeForEncoding(len, Utf8Encoding) + 1;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            return NativeMethods.CFStringGetCString(cf, buffer, size, Utf8Encoding)
                ? Marshal.PtrToStringUTF8(buffer) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static class NativeMethods
    {
        internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
        private const string LibSystem = "/usr/lib/libSystem.B.dylib";

        internal enum AXError
        {
            Success = 0
        }

        internal enum AXValueType
        {
            CgPoint = 1,
            CgSize = 2,
            CgRect = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CgPoint
        {
            public double X;
            public double Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CgSize
        {
            public double Width;
            public double Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CgRect
        {
            public CgPoint Origin;
            public CgSize Size;
        }

        [DllImport(AppServices)]
        internal static extern bool AXIsProcessTrusted();

        [DllImport(AppServices)]
        internal static extern IntPtr AXUIElementCreateApplication(int pid);

        [DllImport(AppServices)]
        internal static extern AXError AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

        [DllImport(AppServices)]
        internal static extern AXError AXUIElementSetAttributeValue(IntPtr element, IntPtr attribute, IntPtr value);

        [DllImport(AppServices)]
        internal static extern AXError AXUIElementCopyActionNames(IntPtr element, out IntPtr actions);

        [DllImport(AppServices)]
        internal static extern AXError AXUIElementSetMessagingTimeout(IntPtr element, double seconds);

        [DllImport(AppServices)]
        internal static extern bool AXValueGetValue(IntPtr value, AXValueType type, ref CgPoint point);

        [DllImport(AppServices)]
        internal static extern bool AXValueGetValue(IntPtr value, AXValueType type, ref CgSize size);

        [DllImport(AppServices)]
        internal static extern bool AXValueGetValue(IntPtr value, AXValueType type, ref CgRect rect);

        [DllImport(CoreFoundation)]
        internal static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);

        [DllImport(CoreFoundation)]
        internal static extern nint CFStringGetLength(IntPtr value);

        [DllImport(CoreFoundation)]
        internal static extern nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

        [DllImport(CoreFoundation)]
        internal static extern bool CFStringGetCString(IntPtr value, IntPtr buffer, nint bufferSize, uint encoding);

        [DllImport(CoreFoundation)]
        internal static extern nint CFArrayGetCount(IntPtr array);

        [DllImport(CoreFoundation)]
        internal static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

        [DllImport(CoreFoundation)]
        internal static extern void CFRelease(IntPtr value);

        [DllImport(CoreFoundation)]
        internal static extern IntPtr CFRetain(IntPtr value);

        [DllImport(LibSystem)]
        internal static extern IntPtr dlopen(string path, int mode);

        [DllImport(LibSystem)]
        internal static extern IntPtr dlsym(IntPtr handle, string symbol);
    }
}
