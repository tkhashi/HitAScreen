using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public static class SuppressionEvaluator
{
    public static bool ShouldSuppress(UserSettings settings, ActiveWindowContext? context, DisplayInfo? display)
    {
        if (context is null)
        {
            return false;
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
}
