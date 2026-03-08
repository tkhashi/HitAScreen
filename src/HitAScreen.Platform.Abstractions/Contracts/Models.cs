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
