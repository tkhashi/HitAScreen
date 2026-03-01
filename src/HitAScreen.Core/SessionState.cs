namespace HitAScreen.Core;

public enum SessionState
{
    Idle,
    CaptureContext,
    Analyze,
    OverlayActive,
    ExecuteAction,
    End
}
