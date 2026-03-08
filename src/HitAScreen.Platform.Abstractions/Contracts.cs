using System.Collections.ObjectModel;

namespace HitAScreen.Platform.Abstractions;

public enum AnalysisTarget
{
    ActiveMonitor,
    ActiveWindow
}

public enum CandidateSource
{
    Accessibility
}

public enum UiActionType
{
    LeftClick,
    RightClick,
    DoubleClick,
    Focus
}

public enum MonitorSwitchDirection
{
    Left,
    Right
}

public enum PermissionArea
{
    Accessibility,
    InputMonitoring,
    ScreenRecording
}

public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(ScreenPoint point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    public bool Intersects(ScreenRect other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public ScreenPoint Center => new(X + (Width / 2), Y + (Height / 2));
}

public readonly record struct HotkeyChord(int KeyCode, bool Command, bool Control, bool Option, bool Shift, string DisplayText);

public sealed record HotkeyRegistrationResult(bool Succeeded, string? Reason = null);
public readonly record struct GlobalKeyEvent(int KeyCode, bool Command, bool Control, bool Option, bool Shift);

public sealed record ActiveWindowContext(
    uint WindowId,
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string WindowTitle,
    ScreenRect Bounds,
    string? DisplayId,
    double? DpiScale,
    bool IsForegroundConfirmed,
    string? FallbackReason = null);

public sealed record DisplayInfo(string Id, ScreenRect Bounds, double DpiScale, bool IsPrimary);

public sealed record UiCandidate(
    string CandidateId,
    ScreenRect Bounds,
    string Role,
    string? Subrole,
    string? Title,
    string? Actions,
    double Confidence,
    CandidateSource Source);

public sealed record AnalysisContext(
    ActiveWindowContext ActiveWindow,
    DisplayInfo TargetDisplay,
    AnalysisTarget Target,
    ScreenRect TargetBounds);

public sealed record PermissionSnapshot(
    bool AccessibilityGranted,
    bool InputMonitoringGranted,
    bool ScreenRecordingGranted,
    string? Note = null);

public sealed record OverlayShortcutSettings
{
    public HotkeyChord SwitchMonitorLeft { get; init; } = new(123, Command: false, Control: false, Option: false, Shift: false, DisplayText: "Left");
    public HotkeyChord SwitchMonitorRight { get; init; } = new(124, Command: false, Control: false, Option: false, Shift: false, DisplayText: "Right");
    public HotkeyChord Reanalyze { get; init; } = new(48, Command: false, Control: false, Option: false, Shift: false, DisplayText: "Tab");
    public HotkeyChord ActionLeftClick { get; init; } = new(122, Command: false, Control: false, Option: false, Shift: false, DisplayText: "F1");
    public HotkeyChord ActionRightClick { get; init; } = new(120, Command: false, Control: false, Option: false, Shift: false, DisplayText: "F2");
    public HotkeyChord ActionDoubleClick { get; init; } = new(99, Command: false, Control: false, Option: false, Shift: false, DisplayText: "F3");
    public HotkeyChord ActionFocus { get; init; } = new(118, Command: false, Control: false, Option: false, Shift: false, DisplayText: "F4");
}

public sealed record LabelAppearanceSettings
{
    public string NormalBackgroundColor { get; init; } = "#5A5A5A";
    public string MatchedBackgroundColor { get; init; } = "#FFD65C";
    public double Opacity { get; init; } = 0.85;
    public double LabelWidth { get; init; } = 44;
    public double LabelHeight { get; init; } = 26;
    public double FontSize { get; init; } = 14;
}

public sealed record UserSettings
{
    public HotkeyChord Hotkey { get; init; } = new(46, Command: true, Control: false, Option: false, Shift: true, DisplayText: "Cmd+Shift+M");
    public OverlayShortcutSettings OverlayHotkeys { get; init; } = new();
    public AnalysisTarget DefaultAnalysisTarget { get; init; } = AnalysisTarget.ActiveMonitor;
    public string LabelCharacterSet { get; init; } = "ASDFGHJKLQWERTYUIOPZXCVBNM";
    public double LabelScale { get; init; } = 1.0;
    public LabelAppearanceSettings LabelAppearance { get; init; } = new();
    public IReadOnlyList<string> RecentLabelColors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludedAxRoles { get; init; } = Array.Empty<string>();
    public bool SuppressInFullscreen { get; init; } = true;
    public IReadOnlyList<string> SuppressedProcesses { get; init; } = Array.Empty<string>();
    public bool LaunchAtLogin { get; init; } = false;
    public bool ContinuousMode { get; init; } = false;
    public bool DebugLoggingEnabled { get; init; } = false;
}

public interface IHotkeyService : IDisposable
{
    bool IsRegistered { get; }
    bool SuppressKeyPropagation { get; set; }
    event Action? HotkeyPressed;
    event Action<GlobalKeyEvent>? KeyPressed;
    HotkeyRegistrationResult Register(HotkeyChord chord);
    void Unregister();
}

public interface IActiveWindowService
{
    ActiveWindowContext? TryCaptureForegroundWindow();
}

public interface IAccessibilityElementProvider
{
    IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context);
}

public interface IInputInjectionService
{
    void Execute(UiActionType action, UiCandidate candidate);
}

public interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetDisplays();
    DisplayInfo? GetDisplayById(string displayId);
    DisplayInfo? GetDisplayContainingPoint(ScreenPoint point);
    ScreenPoint GetCursorPosition();
}

public interface IPermissionService
{
    PermissionSnapshot GetCurrentStatus();
    PermissionSnapshot RequestMissingPermissions();
    bool OpenSystemSettings(PermissionArea area, out string? errorMessage);
}

public interface ISettingsStore
{
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}

public interface ILaunchAtLoginService
{
    bool IsEnabled();
    bool SetEnabled(bool enabled, out string? errorMessage);
}

public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

public sealed record SessionDiagnostics(
    DateTimeOffset Timestamp,
    string? State,
    string? Message,
    long? CaptureContextMs,
    long? AnalyzeMs,
    long? OverlayReadyMs,
    int CandidateCount,
    ActiveWindowContext? Context,
    PermissionSnapshot? Permissions);

public sealed record OverlayHint(
    string Label,
    ScreenRect Bounds,
    string? Title,
    bool MatchesInput);

public sealed record OverlayViewState(
    Guid SessionId,
    ScreenRect TargetBounds,
    ScreenRect OverlayBounds,
    DisplayInfo TargetDisplay,
    string Input,
    UiActionType PendingAction,
    IReadOnlyList<OverlayHint> Hints,
    bool ContinuousMode,
    AnalysisTarget Target);

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
