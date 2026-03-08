namespace HitAScreen.Platform.Abstractions;

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
