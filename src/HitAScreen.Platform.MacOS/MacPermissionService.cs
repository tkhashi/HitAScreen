using System.Runtime.InteropServices;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacPermissionService : IPermissionService
{
    private const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public PermissionSnapshot GetCurrentStatus()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new PermissionSnapshot(false, false, false, "macOS only");
        }

        return new PermissionSnapshot(
            AXIsProcessTrusted(),
            CGPreflightListenEventAccess(),
            CGPreflightScreenCaptureAccess());
    }

    public PermissionSnapshot RequestMissingPermissions()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new PermissionSnapshot(false, false, false, "macOS only");
        }

        if (!CGPreflightListenEventAccess())
        {
            CGRequestListenEventAccess();
        }

        if (!CGPreflightScreenCaptureAccess())
        {
            CGRequestScreenCaptureAccess();
        }

        return GetCurrentStatus();
    }

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(CoreGraphics)]
    private static extern bool CGPreflightListenEventAccess();

    [DllImport(CoreGraphics)]
    private static extern bool CGRequestListenEventAccess();

    [DllImport(CoreGraphics)]
    private static extern bool CGPreflightScreenCaptureAccess();

    [DllImport(CoreGraphics)]
    private static extern bool CGRequestScreenCaptureAccess();
}
