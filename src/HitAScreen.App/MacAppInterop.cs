using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace HitAScreen.App;

internal static class MacAppInterop
{
    private const string Objc = "/usr/lib/libobjc.A.dylib";
    private static readonly IntPtr NsApplicationClass = objc_getClass("NSApplication");
    private static readonly IntPtr SharedApplicationSel = sel_registerName("sharedApplication");
    private static readonly IntPtr SetActivationPolicySel = sel_registerName("setActivationPolicy:");
    private static readonly IntPtr SetIgnoresMouseEventsSel = sel_registerName("setIgnoresMouseEvents:");
    private static readonly IntPtr SetMovableSel = sel_registerName("setMovable:");

    // NSApplicationActivationPolicyAccessory（Dock に出さない常駐アプリ扱い）
    private const nint AccessoryPolicy = 1;

    public static void ConfigureActivationPolicyAccessory()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var app = objc_msgSend(NsApplicationClass, SharedApplicationSel);
        if (app == IntPtr.Zero)
        {
            return;
        }

        _ = objc_msgSend_nint(app, SetActivationPolicySel, AccessoryPolicy);
    }

    public static void ConfigureOverlayMouseEvents(Window window, bool ignoreMouseEvents)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_bool(handle, SetIgnoresMouseEventsSel, ignoreMouseEvents);
        objc_msgSend_bool(handle, SetMovableSel, false);
    }

    [DllImport(Objc)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(Objc)]
    private static extern IntPtr sel_registerName(string selectorName);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool value);

    [DllImport(Objc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_nint(IntPtr receiver, IntPtr selector, nint value);
}
