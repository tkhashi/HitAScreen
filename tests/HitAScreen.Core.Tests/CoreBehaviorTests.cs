using HitAScreen.Core;
using HitAScreen.Platform.Abstractions;
using Xunit;

namespace HitAScreen.Core.Tests;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void HintLabelGenerator_GeneratesGrowingLabels()
    {
        var generator = new HintLabelGenerator();

        var labels = generator.Generate(30, "AB");

        Assert.Equal(30, labels.Count);
        Assert.Equal("A", labels[0]);
        Assert.Equal("B", labels[1]);
        Assert.Equal("AA", labels[2]);
        Assert.Equal("AB", labels[3]);
    }

    [Fact]
    public void SuppressionEvaluator_ReturnsTrue_ForFullscreenWindow()
    {
        var settings = new UserSettings
        {
            SuppressInFullscreen = true
        };

        var context = new ActiveWindowContext(
            1,
            100,
            "GameApp",
            null,
            "Game",
            new ScreenRect(0, 0, 1920, 1080),
            "display-1",
            2,
            true);

        var display = new DisplayInfo("display-1", new ScreenRect(0, 0, 1920, 1080), 2, true);

        Assert.True(SuppressionEvaluator.ShouldSuppress(settings, context, display));
    }

    [Fact]
    public async Task Orchestrator_ExecutesExactLabel_AndEndsSession()
    {
        var hotkey = new FakeHotkeyService();
        var activeWindow = new FakeActiveWindowService
        {
            Context = new ActiveWindowContext(
                42,
                123,
                "Editor",
                null,
                "Test",
                new ScreenRect(10, 20, 1000, 700),
                "display-1",
                2,
                true)
        };

        var provider = new FakeAccessibilityProvider
        {
            Candidates =
            [
                new UiCandidate("c1", new ScreenRect(100, 100, 50, 20), "AXButton", null, "OK", "AXPress", 0.9, CandidateSource.Accessibility)
            ]
        };

        var input = new FakeInputInjectionService();
        var display = new FakeDisplayService();
        var permissions = new FakePermissionService();
        var store = new InMemorySettingsStore();
        var logger = new TestLogger();

        var orchestrator = new ScreenSearchOrchestrator(hotkey, activeWindow, provider, input, display, permissions, store, logger);

        OverlayViewState? latest = null;
        orchestrator.OverlayStateChanged += state => latest = state;

        await orchestrator.InitializeAsync();
        orchestrator.StartSession();

        Assert.NotNull(latest);

        orchestrator.HandleCharacter('A');

        Assert.Equal(1, input.ExecutionCount);
        Assert.Null(latest);
    }

    [Fact]
    public async Task Orchestrator_UsesFallbackContext_WhenForegroundUnavailable()
    {
        var hotkey = new FakeHotkeyService();
        var activeWindow = new FakeActiveWindowService { Context = null };
        var provider = new FakeAccessibilityProvider();
        var input = new FakeInputInjectionService();
        var display = new FakeDisplayService();
        var permissions = new FakePermissionService();
        var store = new InMemorySettingsStore();
        var logger = new TestLogger();

        var orchestrator = new ScreenSearchOrchestrator(hotkey, activeWindow, provider, input, display, permissions, store, logger);

        SessionDiagnostics? sessionStart = null;
        orchestrator.DiagnosticsChanged += diagnostics =>
        {
            if (diagnostics.Message == "session-started")
            {
                sessionStart = diagnostics;
            }
        };

        await orchestrator.InitializeAsync();
        orchestrator.StartSession();

        Assert.NotNull(sessionStart);
        Assert.NotNull(sessionStart!.Context);
        Assert.Equal("foreground-window-not-available", sessionStart.Context!.FallbackReason);
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
        public bool IsRegistered { get; private set; }
        public event Action? HotkeyPressed;
        public event Action<GlobalKeyEvent>? KeyPressed;

        public HotkeyRegistrationResult Register(HotkeyChord chord)
        {
            IsRegistered = true;
            return new HotkeyRegistrationResult(true);
        }

        public void Unregister() => IsRegistered = false;
        public void Dispose() => IsRegistered = false;
        public void Fire() => HotkeyPressed?.Invoke();
        public void FireKey(GlobalKeyEvent key) => KeyPressed?.Invoke(key);
    }

    private sealed class FakeActiveWindowService : IActiveWindowService
    {
        public ActiveWindowContext? Context { get; set; }

        public ActiveWindowContext? TryCaptureForegroundWindow() => Context;
    }

    private sealed class FakeAccessibilityProvider : IAccessibilityElementProvider
    {
        public IReadOnlyList<UiCandidate> Candidates { get; set; } = [];

        public IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context) => Candidates;
    }

    private sealed class FakeInputInjectionService : IInputInjectionService
    {
        public int ExecutionCount { get; private set; }

        public void Execute(UiActionType action, UiCandidate candidate)
        {
            ExecutionCount++;
        }
    }

    private sealed class FakeDisplayService : IDisplayService
    {
        private static readonly DisplayInfo Display = new("display-1", new ScreenRect(0, 0, 1920, 1080), 2.0, true);

        public IReadOnlyList<DisplayInfo> GetDisplays() => [Display];
        public DisplayInfo? GetDisplayById(string displayId) => Display;
        public DisplayInfo? GetDisplayContainingPoint(ScreenPoint point) => Display;
        public ScreenPoint GetCursorPosition() => new(200, 200);
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public PermissionSnapshot GetCurrentStatus() => new(true, true, true);

        public PermissionSnapshot RequestMissingPermissions() => new(true, true, true);
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private UserSettings _settings = new();

        public Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : IAppLogger
    {
        public void Error(string message, Exception? exception = null)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }
    }
}
