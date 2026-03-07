using HitAScreen.Core;
using HitAScreen.Infrastructure;
using HitAScreen.Platform.Abstractions;
using Xunit;

namespace HitAScreen.Core.Tests;

public sealed class SettingsAndFilteringTests
{
    [Fact]
    public async Task JsonSettingsStore_LoadsLegacySettingsJson_WithoutException()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"hitascreen-settings-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempFile,
            """
            {
              "hotkey": {
                "keyCode": 46,
                "command": true,
                "control": false,
                "option": false,
                "shift": true,
                "displayText": "Cmd+Shift+M"
              },
              "defaultAnalysisTarget": 0,
              "labelCharacterSet": "ASDF",
              "labelScale": 1.25,
              "suppressInFullscreen": true,
              "suppressedProcesses": ["Steam"],
              "continuousMode": false,
              "debugLoggingEnabled": false
            }
            """);

        try
        {
            var store = new JsonSettingsStore(tempFile);
            var loaded = await store.LoadAsync();
            var normalized = UserSettingsNormalizer.Normalize(loaded);

            Assert.Equal(46, normalized.Hotkey.KeyCode);
            Assert.Equal("ASDF", normalized.LabelCharacterSet);
            Assert.Equal(1.25, normalized.LabelScale);
            Assert.NotNull(normalized.OverlayHotkeys);
            Assert.NotEmpty(normalized.ExcludedAxRoles);
            Assert.NotNull(normalized.LabelAppearance);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void UserSettingsNormalizer_ClampsAppearanceAndFallbackValues()
    {
        var settings = new UserSettings
        {
            LabelScale = 999,
            ExcludedAxRoles = Array.Empty<string>(),
            LabelAppearance = new LabelAppearanceSettings
            {
                NormalBackgroundColor = "invalid",
                MatchedBackgroundColor = "also-invalid",
                Opacity = 9,
                LabelWidth = -10,
                LabelHeight = 500,
                FontSize = 1
            }
        };

        var normalized = UserSettingsNormalizer.Normalize(settings);

        Assert.Equal(3.0, normalized.LabelScale);
        Assert.Equal("#5A5A5A", normalized.LabelAppearance.NormalBackgroundColor);
        Assert.Equal("#FFD65C", normalized.LabelAppearance.MatchedBackgroundColor);
        Assert.Equal(1.0, normalized.LabelAppearance.Opacity);
        Assert.Equal(20, normalized.LabelAppearance.LabelWidth);
        Assert.Equal(120, normalized.LabelAppearance.LabelHeight);
        Assert.Equal(8, normalized.LabelAppearance.FontSize);
        Assert.Contains("AXGroup", normalized.ExcludedAxRoles);
    }

    [Fact]
    public async Task Orchestrator_AppliesExcludedAxRoleFilter()
    {
        var hotkey = new RecordingHotkeyService();
        var activeWindow = new StaticActiveWindowService();
        var provider = new StaticAccessibilityProvider
        {
            Candidates =
            [
                new UiCandidate("group", new ScreenRect(100, 100, 20, 20), "AXGroup", null, "group", "AXPress", 0.9, CandidateSource.Accessibility),
                new UiCandidate("button", new ScreenRect(130, 120, 20, 20), "AXButton", null, "ok", "AXPress", 0.9, CandidateSource.Accessibility)
            ]
        };

        var orchestrator = BuildOrchestrator(hotkey, activeWindow, provider, initialSettings: new UserSettings
        {
            ExcludedAxRoles = ["AXGroup"]
        });

        OverlayViewState? state = null;
        orchestrator.OverlayStateChanged += value => state = value;

        await orchestrator.InitializeAsync();
        orchestrator.StartSession();

        Assert.NotNull(state);
        Assert.Single(state!.Hints);
        Assert.Equal("A", state.Hints[0].Label);
    }

    [Fact]
    public async Task Orchestrator_RegistersUpdatedHotkey()
    {
        var hotkey = new RecordingHotkeyService();
        var orchestrator = BuildOrchestrator(hotkey, new StaticActiveWindowService(), new StaticAccessibilityProvider());

        await orchestrator.InitializeAsync();

        var updatedHotkey = new HotkeyChord(18, Command: true, Control: false, Option: true, Shift: false, DisplayText: "Cmd+Opt+1");
        await orchestrator.UpdateSettingsAsync(orchestrator.Settings with
        {
            Hotkey = updatedHotkey,
            SuppressInFullscreen = false
        });

        Assert.Equal(updatedHotkey, hotkey.LastRegisteredChord);
        Assert.True(orchestrator.LastHotkeyRegistrationResult.Succeeded);
    }

    private static ScreenSearchOrchestrator BuildOrchestrator(
        RecordingHotkeyService hotkey,
        StaticActiveWindowService activeWindow,
        StaticAccessibilityProvider provider,
        UserSettings? initialSettings = null)
    {
        return new ScreenSearchOrchestrator(
            hotkey,
            activeWindow,
            provider,
            new NoopInputInjectionService(),
            new StaticDisplayService(),
            new NoopPermissionService(),
            new InMemorySettingsStore(initialSettings ?? new UserSettings()),
            new NoopLogger());
    }

    private sealed class RecordingHotkeyService : IHotkeyService
    {
        public bool IsRegistered { get; private set; }
        public HotkeyChord LastRegisteredChord { get; private set; } = new(46, true, false, false, true, "Cmd+Shift+M");
        public event Action? HotkeyPressed;
        public event Action<GlobalKeyEvent>? KeyPressed;

        public HotkeyRegistrationResult Register(HotkeyChord chord)
        {
            LastRegisteredChord = chord;
            IsRegistered = true;
            _ = HotkeyPressed;
            _ = KeyPressed;
            return new HotkeyRegistrationResult(true);
        }

        public void Unregister() => IsRegistered = false;
        public void Dispose() => IsRegistered = false;
    }

    private sealed class StaticActiveWindowService : IActiveWindowService
    {
        public ActiveWindowContext? TryCaptureForegroundWindow() => new(
            WindowId: 1,
            ProcessId: 100,
            ProcessName: "Finder",
            ExecutablePath: null,
            WindowTitle: "Finder",
            Bounds: new ScreenRect(0, 0, 1920, 1080),
            DisplayId: "display-1",
            DpiScale: 2,
            IsForegroundConfirmed: true);
    }

    private sealed class StaticAccessibilityProvider : IAccessibilityElementProvider
    {
        public IReadOnlyList<UiCandidate> Candidates { get; set; } = [];

        public IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context) => Candidates;
    }

    private sealed class NoopInputInjectionService : IInputInjectionService
    {
        public void Execute(UiActionType action, UiCandidate candidate)
        {
        }
    }

    private sealed class StaticDisplayService : IDisplayService
    {
        private static readonly DisplayInfo Display = new("display-1", new ScreenRect(0, 0, 1920, 1080), 2.0, true);

        public IReadOnlyList<DisplayInfo> GetDisplays() => [Display];
        public DisplayInfo? GetDisplayById(string displayId) => Display;
        public DisplayInfo? GetDisplayContainingPoint(ScreenPoint point) => Display;
        public ScreenPoint GetCursorPosition() => new(0, 0);
    }

    private sealed class NoopPermissionService : IPermissionService
    {
        public PermissionSnapshot GetCurrentStatus() => new(true, true, true);

        public PermissionSnapshot RequestMissingPermissions() => new(true, true, true);

        public bool OpenSystemSettings(PermissionArea area, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private UserSettings _settings;

        public InMemorySettingsStore(UserSettings settings)
        {
            _settings = settings;
        }

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

    private sealed class NoopLogger : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
