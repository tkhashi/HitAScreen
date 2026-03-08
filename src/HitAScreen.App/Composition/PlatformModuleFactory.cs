using HitAScreen.Platform.Abstractions;
using HitAScreen.Platform.MacOS;

namespace HitAScreen.App.Composition;

internal sealed record PlatformModule(
    IHotkeyService HotkeyService,
    IActiveWindowService ActiveWindowService,
    IAccessibilityElementProvider AccessibilityElementProvider,
    IInputInjectionService InputInjectionService,
    IDisplayService DisplayService,
    IPermissionService PermissionService,
    ILaunchAtLoginService LaunchAtLoginService);

internal static class PlatformModuleFactory
{
    public static PlatformModule Create()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new PlatformModule(
                new MacHotkeyService(),
                new MacActiveWindowService(),
                new MacAccessibilityCandidateProvider(),
                new MacInputInjectionService(),
                new MacDisplayService(),
                new MacPermissionService(),
                new MacLaunchAtLoginService());
        }

        return new PlatformModule(
            new NoopHotkeyService(),
            new NoopActiveWindowService(),
            new NoopAccessibilityElementProvider(),
            new NoopInputInjectionService(),
            new NoopDisplayService(),
            new NoopPermissionService(),
            new NoopLaunchAtLoginService());
    }

    private sealed class NoopHotkeyService : IHotkeyService
    {
        public bool IsRegistered { get; private set; }
        public bool SuppressKeyPropagation { get; set; }
        public event Action? HotkeyPressed;
        public event Action<GlobalKeyEvent>? KeyPressed;

        public HotkeyRegistrationResult Register(HotkeyChord chord)
        {
            IsRegistered = true;
            _ = HotkeyPressed;
            _ = KeyPressed;
            return new HotkeyRegistrationResult(true);
        }

        public void Unregister() => IsRegistered = false;
        public void Dispose() => IsRegistered = false;
    }

    private sealed class NoopActiveWindowService : IActiveWindowService
    {
        public ActiveWindowContext? TryCaptureForegroundWindow() => null;
    }

    private sealed class NoopAccessibilityElementProvider : IAccessibilityElementProvider
    {
        public IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context) => Array.Empty<UiCandidate>();
    }

    private sealed class NoopInputInjectionService : IInputInjectionService
    {
        public void Execute(UiActionType action, UiCandidate candidate)
        {
        }
    }

    private sealed class NoopDisplayService : IDisplayService
    {
        private static readonly DisplayInfo FallbackDisplay = new("fallback", new ScreenRect(0, 0, 1280, 800), 1.0, true);

        public IReadOnlyList<DisplayInfo> GetDisplays() => [FallbackDisplay];

        public DisplayInfo? GetDisplayById(string displayId) => FallbackDisplay;

        public DisplayInfo? GetDisplayContainingPoint(ScreenPoint point) => FallbackDisplay;

        public ScreenPoint GetCursorPosition() => new(0, 0);
    }

    private sealed class NoopPermissionService : IPermissionService
    {
        public PermissionSnapshot GetCurrentStatus() => new(false, false, false, "unsupported platform");

        public PermissionSnapshot RequestMissingPermissions() => GetCurrentStatus();

        public bool OpenSystemSettings(PermissionArea area, out string? errorMessage)
        {
            errorMessage = $"{area} の設定画面オープンはサポート外です。";
            return false;
        }
    }

    private sealed class NoopLaunchAtLoginService : ILaunchAtLoginService
    {
        public bool IsEnabled() => false;

        public bool SetEnabled(bool enabled, out string? errorMessage)
        {
            errorMessage = "自動起動設定はサポート外です。";
            return false;
        }
    }
}
