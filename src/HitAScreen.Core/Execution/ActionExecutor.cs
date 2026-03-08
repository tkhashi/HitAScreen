using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public sealed class ActionExecutor(
    IInputInjectionService inputInjectionService,
    IPermissionService permissionService,
    IAppLogger logger)
{
    public bool TryExecute(UiActionType action, UiCandidate candidate, Action<string, PermissionSnapshot> onSucceeded, Action<string, PermissionSnapshot> onFailed)
    {
        try
        {
            inputInjectionService.Execute(action, candidate);
            onSucceeded($"action-executed:{action}", permissionService.GetCurrentStatus());
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Input injection failed.", ex);
            onFailed("action-execution-failed", permissionService.GetCurrentStatus());
            return false;
        }
    }
}
