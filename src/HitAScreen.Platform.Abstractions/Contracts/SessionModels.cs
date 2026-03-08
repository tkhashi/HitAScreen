namespace HitAScreen.Platform.Abstractions;

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
    AnalysisTarget Target,
    bool IsPreparing = false);
