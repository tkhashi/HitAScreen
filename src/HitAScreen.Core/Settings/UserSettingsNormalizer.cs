using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public static class UserSettingsNormalizer
{
    private const string DefaultLabelCharacterSet = "ASDFGHJKLQWERTYUIOPZXCVBNM";

    public static UserSettings Normalize(UserSettings? settings)
    {
        settings ??= new UserSettings();

        var normalizedHotkey = NormalizeChord(settings.Hotkey, new HotkeyChord(46, true, false, false, true, "Cmd+Shift+M"));
        var normalizedOverlayHotkeys = NormalizeOverlayHotkeys(settings.OverlayHotkeys);
        var normalizedLabelAppearance = NormalizeLabelAppearance(settings.LabelAppearance);
        var normalizedRecentColors = NormalizeRecentColors(settings.RecentLabelColors);
        var normalizedExcludedRoles = NormalizeRoles(settings.ExcludedAxRoles);
        var normalizedCharacterSet = string.IsNullOrWhiteSpace(settings.LabelCharacterSet)
            ? DefaultLabelCharacterSet
            : settings.LabelCharacterSet;
        var normalizedScale = Math.Clamp(settings.LabelScale, 0.5, 3.0);

        return settings with
        {
            Hotkey = normalizedHotkey,
            OverlayHotkeys = normalizedOverlayHotkeys,
            LabelAppearance = normalizedLabelAppearance,
            RecentLabelColors = normalizedRecentColors,
            ExcludedAxRoles = normalizedExcludedRoles,
            LabelCharacterSet = normalizedCharacterSet,
            LabelScale = normalizedScale
        };
    }

    private static OverlayShortcutSettings NormalizeOverlayHotkeys(OverlayShortcutSettings? settings)
    {
        settings ??= new OverlayShortcutSettings();
        return settings with
        {
            SwitchMonitorLeft = NormalizeChord(settings.SwitchMonitorLeft, new HotkeyChord(123, false, false, false, false, "Left")),
            SwitchMonitorRight = NormalizeChord(settings.SwitchMonitorRight, new HotkeyChord(124, false, false, false, false, "Right")),
            Reanalyze = NormalizeChord(settings.Reanalyze, new HotkeyChord(48, false, false, false, false, "Tab")),
            ActionLeftClick = NormalizeChord(settings.ActionLeftClick, new HotkeyChord(122, false, false, false, false, "F1")),
            ActionRightClick = NormalizeChord(settings.ActionRightClick, new HotkeyChord(120, false, false, false, false, "F2")),
            ActionDoubleClick = NormalizeChord(settings.ActionDoubleClick, new HotkeyChord(99, false, false, false, false, "F3")),
            ActionFocus = NormalizeChord(settings.ActionFocus, new HotkeyChord(118, false, false, false, false, "F4"))
        };
    }

    private static LabelAppearanceSettings NormalizeLabelAppearance(LabelAppearanceSettings? settings)
    {
        settings ??= new LabelAppearanceSettings();

        return settings with
        {
            NormalBackgroundColor = NormalizeColor(settings.NormalBackgroundColor, "#5A5A5A"),
            MatchedBackgroundColor = NormalizeColor(settings.MatchedBackgroundColor, "#FFD65C"),
            Opacity = Math.Clamp(settings.Opacity, 0.1, 1.0),
            LabelWidth = Math.Clamp(settings.LabelWidth, 20, 180),
            LabelHeight = Math.Clamp(settings.LabelHeight, 16, 120),
            FontSize = Math.Clamp(settings.FontSize, 8, 48)
        };
    }

    private static string[] NormalizeRoles(IReadOnlyList<string>? roles)
    {
        var normalized = (roles ?? Array.Empty<string>())
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized;
    }

    private static string[] NormalizeRecentColors(IReadOnlyList<string>? colors)
    {
        return (colors ?? Array.Empty<string>())
            .Select(static color => NormalizeColor(color, string.Empty))
            .Where(static color => !string.IsNullOrWhiteSpace(color))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static HotkeyChord NormalizeChord(HotkeyChord value, HotkeyChord fallback)
    {
        if (value.KeyCode < 0 || string.IsNullOrWhiteSpace(value.DisplayText))
        {
            return fallback;
        }

        return value;
    }

    private static string NormalizeColor(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        if (!normalized.StartsWith('#'))
        {
            normalized = "#" + normalized;
        }

        if (normalized.Length is 4 or 7 or 9)
        {
            return normalized.ToUpperInvariant();
        }

        return fallback;
    }
}
