using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public static class SuppressionEvaluator
{
    private static readonly string[] SelfProcessNames =
    [
        "HitAScreen",
        "HitAScreen.App",
        "Hit A Screen"
    ];

    public static bool ShouldSuppress(UserSettings settings, ActiveWindowContext? context, DisplayInfo? display)
    {
        if (context is null)
        {
            return false;
        }

        if (IsSelfProcess(context))
        {
            return true;
        }

        if (settings.SuppressedProcesses.Any(process =>
                string.Equals(process, context.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!settings.SuppressInFullscreen || display is null)
        {
            return false;
        }

        return IsFullscreenLike(context.Bounds, display.Bounds);
    }

    public static bool IsFullscreenLike(ScreenRect window, ScreenRect display)
    {
        if (window.Width <= 0 || window.Height <= 0 || display.Width <= 0 || display.Height <= 0)
        {
            return false;
        }

        var widthRatio = window.Width / display.Width;
        var heightRatio = window.Height / display.Height;
        var nearOrigin = Math.Abs(window.X - display.X) <= 3 && Math.Abs(window.Y - display.Y) <= 3;

        return nearOrigin && widthRatio >= 0.98 && heightRatio >= 0.98;
    }

    private static bool IsSelfProcess(ActiveWindowContext context)
    {
        if (SelfProcessNames.Any(name => string.Equals(name, context.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var executableName = Path.GetFileNameWithoutExtension(context.ExecutablePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return false;
        }

        return executableName.StartsWith("HitAScreen", StringComparison.OrdinalIgnoreCase)
            || executableName.StartsWith("Hit A Screen", StringComparison.OrdinalIgnoreCase);
    }
}
