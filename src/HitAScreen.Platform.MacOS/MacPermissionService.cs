using System.Runtime.InteropServices;
using System.Diagnostics;
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

        var screenRecordingGranted = CGPreflightScreenCaptureAccess();
        return new PermissionSnapshot(
            AXIsProcessTrusted(),
            CGPreflightListenEventAccess(),
            screenRecordingGranted,
            screenRecordingGranted
                ? null
                : "Screen Recording は任意です。未許可でも動作しますが、一部アプリではウィンドウタイトル取得が制限され、対象ウィンドウ照合精度が下がる場合があります。");
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

        return GetCurrentStatus();
    }

    public bool OpenSystemSettings(PermissionArea area, out string? errorMessage)
    {
        if (!OperatingSystem.IsMacOS())
        {
            errorMessage = "macOS のみ対応しています。";
            return false;
        }

        var uri = area switch
        {
            PermissionArea.Accessibility => "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
            PermissionArea.InputMonitoring => "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent",
            PermissionArea.ScreenRecording => "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture",
            _ => "x-apple.systempreferences:com.apple.preference.security"
        };

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                ArgumentList = { uri },
                UseShellExecute = false
            });

            if (process is null)
            {
                errorMessage = $"{area} の設定画面を開けませんでした。";
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"設定画面オープンに失敗しました: {ex.Message}";
            return false;
        }
    }

    [DllImport(AppServices)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(CoreGraphics)]
    private static extern bool CGPreflightListenEventAccess();

    [DllImport(CoreGraphics)]
    private static extern bool CGRequestListenEventAccess();

    [DllImport(CoreGraphics)]
    private static extern bool CGPreflightScreenCaptureAccess();

}
