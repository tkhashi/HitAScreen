using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacActiveWindowService : IActiveWindowService
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Utf8Encoding = 0x08000100;

    private const uint CGWindowListOptionOnScreenOnly = 1;
    private const uint CGWindowListExcludeDesktopElements = 16;

    private static readonly Dictionary<string, IntPtr> KeyRefs = new(StringComparer.Ordinal)
    {
        ["kCGWindowLayer"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowLayer", Utf8Encoding),
        ["kCGWindowOwnerName"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerName", Utf8Encoding),
        ["kCGWindowName"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowName", Utf8Encoding),
        ["kCGWindowOwnerPID"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerPID", Utf8Encoding),
        ["kCGWindowNumber"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowNumber", Utf8Encoding),
        ["kCGWindowBounds"] = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowBounds", Utf8Encoding),
        ["X"] = CFStringCreateWithCString(IntPtr.Zero, "X", Utf8Encoding),
        ["Y"] = CFStringCreateWithCString(IntPtr.Zero, "Y", Utf8Encoding),
        ["Width"] = CFStringCreateWithCString(IntPtr.Zero, "Width", Utf8Encoding),
        ["Height"] = CFStringCreateWithCString(IntPtr.Zero, "Height", Utf8Encoding)
    };

    public ActiveWindowContext? TryCaptureForegroundWindow()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var windows = CGWindowListCopyWindowInfo(CGWindowListOptionOnScreenOnly | CGWindowListExcludeDesktopElements, 0);
        if (windows == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var count = CFArrayGetCount(windows);
            for (nint i = 0; i < count; i++)
            {
                var dict = CFArrayGetValueAtIndex(windows, i);
                if (dict == IntPtr.Zero)
                {
                    continue;
                }

                var layer = GetIntFromDictionary(dict, "kCGWindowLayer");
                if (layer != 0)
                {
                    continue;
                }

                var width = GetDoubleFromBoundsDictionary(dict, "Width");
                var height = GetDoubleFromBoundsDictionary(dict, "Height");
                if (width < 80 || height < 80)
                {
                    continue;
                }

                var processName = GetStringFromDictionary(dict, "kCGWindowOwnerName");
                var pid = (int)GetIntFromDictionary(dict, "kCGWindowOwnerPID");
                var windowId = (uint)GetIntFromDictionary(dict, "kCGWindowNumber");
                var title = GetStringFromDictionary(dict, "kCGWindowName");
                var x = GetDoubleFromBoundsDictionary(dict, "X");
                var y = GetDoubleFromBoundsDictionary(dict, "Y");

                if (pid <= 0 || windowId == 0 || string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                string? executablePath = null;
                try
                {
                    executablePath = Process.GetProcessById(pid).MainModule?.FileName;
                }
                catch
                {
                    executablePath = null;
                }

                return new ActiveWindowContext(
                    windowId,
                    pid,
                    processName,
                    executablePath,
                    title,
                    new ScreenRect(x, y, width, height),
                    DisplayId: null,
                    DpiScale: null,
                    IsForegroundConfirmed: true);
            }

            return null;
        }
        finally
        {
            CFRelease(windows);
        }
    }

    private static string GetStringFromDictionary(IntPtr dictionary, string key)
    {
        var value = GetDictionaryValue(dictionary, key);
        return value == IntPtr.Zero ? string.Empty : CFStringToString(value);
    }

    private static long GetIntFromDictionary(IntPtr dictionary, string key)
    {
        var value = GetDictionaryValue(dictionary, key);
        if (value == IntPtr.Zero)
        {
            return 0;
        }

        return CFNumberGetValue(value, 4, out long number) ? number : 0;
    }

    private static double GetDoubleFromBoundsDictionary(IntPtr windowDictionary, string key)
    {
        var bounds = GetDictionaryValue(windowDictionary, "kCGWindowBounds");
        if (bounds == IntPtr.Zero)
        {
            return 0;
        }

        var value = GetDictionaryValue(bounds, key);
        if (value == IntPtr.Zero)
        {
            return 0;
        }

        return CFNumberGetValue(value, 13, out double number) ? number : 0;
    }

    private static IntPtr GetDictionaryValue(IntPtr dictionary, string key)
    {
        if (!KeyRefs.TryGetValue(key, out var keyRef))
        {
            return IntPtr.Zero;
        }

        return CFDictionaryGetValue(dictionary, keyRef);
    }

    private static string CFStringToString(IntPtr cfString)
    {
        var length = CFStringGetLength(cfString);
        if (length <= 0)
        {
            return string.Empty;
        }

        var maxBytes = CFStringGetMaximumSizeForEncoding(length, Utf8Encoding) + 1;
        var buffer = new byte[maxBytes];

        if (!CFStringGetCString(cfString, buffer, buffer.Length, Utf8Encoding))
        {
            return string.Empty;
        }

        var nullIndex = Array.IndexOf(buffer, (byte)0);
        if (nullIndex < 0)
        {
            nullIndex = buffer.Length;
        }

        return Encoding.UTF8.GetString(buffer, 0, nullIndex);
    }

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out long value);

    [DllImport(CoreFoundation)]
    private static extern bool CFNumberGetValue(IntPtr number, int type, out double value);

    [DllImport(CoreFoundation)]
    private static extern nint CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundation)]
    private static extern int CFStringGetMaximumSizeForEncoding(nint length, int encoding);

    [DllImport(CoreFoundation)]
    private static extern bool CFStringGetCString(IntPtr handle, byte[] buffer, nint bufferSize, int encoding);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string str, int encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);
}
