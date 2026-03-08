namespace HitAScreen.Core;

public sealed class SessionStateMachine
{
    public SessionState State { get; private set; } = SessionState.Idle;

    public void TransitionTo(SessionState state)
    {
        State = state;
    }
}
