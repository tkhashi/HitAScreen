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

public sealed record UserSettings
{
    public HotkeyChord Hotkey { get; init; } = new(46, Command: true, Control: false, Option: false, Shift: true, DisplayText: "Cmd+Shift+M");
    public AnalysisTarget DefaultAnalysisTarget { get; init; } = AnalysisTarget.ActiveMonitor;
    public string LabelCharacterSet { get; init; } = "ASDFGHJKLQWERTYUIOPZXCVBNM";
    public double LabelScale { get; init; } = 1.0;
    public bool SuppressInFullscreen { get; init; } = true;
    public IReadOnlyList<string> SuppressedProcesses { get; init; } = Array.Empty<string>();
    public bool ContinuousMode { get; init; } = false;
    public bool DebugLoggingEnabled { get; init; } = false;
}

public interface IHotkeyService : IDisposable
{
    bool IsRegistered { get; }
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
}

public interface ISettingsStore
{
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
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
    DisplayInfo TargetDisplay,
    string Input,
    UiActionType PendingAction,
    IReadOnlyList<OverlayHint> Hints,
    bool ContinuousMode,
    AnalysisTarget Target);
