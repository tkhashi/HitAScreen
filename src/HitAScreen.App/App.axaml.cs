using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;
using HitAScreen.App.Composition;
using HitAScreen.Core;
using HitAScreen.Infrastructure;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private OverlayWindow? _overlayWindow;
    private ScreenSearchOrchestrator? _orchestrator;
    private ConfigurableFileLogger? _logger;
    private IPermissionService? _permissionService;
    private ILaunchAtLoginService? _launchAtLoginService;
    private IHotkeyService? _hotkeyService;
    private bool _isShuttingDown;
    private OverlayViewState? _latestOverlayState;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MacAppInterop.ConfigureActivationPolicyAccessory();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => OnExit();

            InitializeServices();
            InitializeWindows();
            InitializeTrayIcon(desktop);

            _ = InitializeOrchestratorAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices()
    {
        _logger = new ConfigurableFileLogger(AppPaths.LogPath);
        var platform = PlatformModuleFactory.Create();
        _permissionService = platform.PermissionService;
        _launchAtLoginService = platform.LaunchAtLoginService;

        var store = new JsonSettingsStore(AppPaths.SettingsPath);
        _hotkeyService = platform.HotkeyService;

        _orchestrator = new ScreenSearchOrchestrator(
            platform.HotkeyService,
            platform.ActiveWindowService,
            platform.AccessibilityElementProvider,
            platform.InputInjectionService,
            platform.DisplayService,
            _permissionService,
            store,
            _logger);

        _orchestrator.SettingsChanged += settings =>
        {
            _logger.SetFileLogging(settings.DebugLoggingEnabled);
            Dispatcher.UIThread.Post(() => _mainWindow?.LoadSettings(settings));
        };

        _orchestrator.DiagnosticsChanged += diagnostics =>
            Dispatcher.UIThread.Post(() => _mainWindow?.AppendDiagnostics(diagnostics));

        _orchestrator.OverlayStateChanged += state =>
            Dispatcher.UIThread.Post(() => ApplyOverlayState(state));

        platform.HotkeyService.KeyPressed += OnGlobalKeyPressed;
    }

    private void InitializeWindows()
    {
        if (_orchestrator is null || _permissionService is null || _launchAtLoginService is null || _logger is null)
        {
            throw new InvalidOperationException("Services are not initialized.");
        }

        _mainWindow = new MainWindow(_orchestrator, _permissionService, _launchAtLoginService, _logger)
        {
            Title = "Hit A Screen Control Panel",
            Icon = LoadTrayIcon()
        };

        _mainWindow.Closing += (_, e) =>
        {
            if (_isShuttingDown)
            {
                return;
            }

            e.Cancel = true;
            _mainWindow.Hide();
        };
        _mainWindow.Activated += async (_, _) => await SafeRefreshHotkeyRegistrationAsync();
        _mainWindow.Deactivated += async (_, _) => await SafeRefreshHotkeyRegistrationAsync();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.CharacterTyped += character => _orchestrator.HandleCharacter(character);
        _overlayWindow.BackspacePressed += () => _orchestrator.HandleBackspace();
        _overlayWindow.EnterPressed += () => _orchestrator.ConfirmInput();
        _overlayWindow.EscapePressed += () => _orchestrator.CancelSession();
        _overlayWindow.PointerCancelRequested += () => _orchestrator.CancelSession();
        _overlayWindow.MonitorSwitchRequested += direction => _orchestrator.SwitchMonitor(direction);
        _overlayWindow.ActionSelected += action => _orchestrator.SetPendingAction(action);
        _overlayWindow.ReanalyzeRequested += () => _orchestrator.Reanalyze();
    }

    private void InitializeTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayIcon = new TrayIcon
        {
            Icon = LoadTrayIcon(),
            ToolTipText = "Hit A Screen",
            IsVisible = true,
            Menu = BuildTrayMenu(desktop)
        };

        if (OperatingSystem.IsMacOS())
        {
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        }

        var icons = new TrayIcons();
        icons.Add(_trayIcon);
        TrayIcon.SetIcons(this, icons);
    }

    private NativeMenu BuildTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openSettings = new NativeMenuItem("Open Control Panel");
        openSettings.Click += (_, _) => ShowMainWindow();

        var startSearch = new NativeMenuItem("Start Screen Search");
        startSearch.Click += (_, _) => _orchestrator?.StartSession();

        var requestPermissions = new NativeMenuItem("Request Permissions");
        requestPermissions.Click += (_, _) =>
        {
            var result = _permissionService?.RequestMissingPermissions();
            if (result is not null)
            {
                _mainWindow?.AppendDiagnostics(new SessionDiagnostics(
                    DateTimeOffset.Now,
                    "Permissions",
                    $"accessibility={result.AccessibilityGranted}, input={result.InputMonitoringGranted}, screen={result.ScreenRecordingGranted}",
                    null,
                    null,
                    null,
                    0,
                    null,
                    result));
            }
        };

        var restart = new NativeMenuItem("Restart App");
        restart.Click += (_, _) => RestartApplication(desktop);

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) =>
        {
            _isShuttingDown = true;
            desktop.Shutdown();
        };

        return new NativeMenu
        {
            Items =
            {
                openSettings,
                startSearch,
                requestPermissions,
                restart,
                new NativeMenuItemSeparator(),
                exit
            }
        };
    }

    private async Task InitializeOrchestratorAsync()
    {
        if (_orchestrator is null)
        {
            return;
        }

        try
        {
            await _orchestrator.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to initialize orchestrator.", ex);
        }
    }

    private async Task SafeRefreshHotkeyRegistrationAsync()
    {
        if (_orchestrator is null)
        {
            return;
        }

        try
        {
            await _orchestrator.RefreshHotkeyRegistrationAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to refresh hotkey registration.", ex);
        }
    }

    private void ApplyOverlayState(OverlayViewState? state)
    {
        if (_overlayWindow is null)
        {
            return;
        }

        _latestOverlayState = state;
        if (state is null)
        {
            _overlayWindow.StopVisualEffects();
            _overlayWindow.Hide();
            return;
        }

        var settings = UserSettingsNormalizer.Normalize(_orchestrator?.Settings);
        _overlayWindow.Render(state, settings.LabelAppearance, settings.LabelScale);
        if (!_overlayWindow.IsVisible)
        {
            _overlayWindow.Show();
            Dispatcher.UIThread.Post(ReapplyLatestOverlayState, DispatcherPriority.Loaded);
        }

    }

    private void ReapplyLatestOverlayState()
    {
        if (_overlayWindow is null || !_overlayWindow.IsVisible || _latestOverlayState is null)
        {
            return;
        }

        var settings = UserSettingsNormalizer.Normalize(_orchestrator?.Settings);
        _overlayWindow.Render(_latestOverlayState, settings.LabelAppearance, settings.LabelScale);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.Activate();
    }

    private static WindowIcon LoadTrayIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://HitAScreen.App/Assets/hit-a-screen-icon.png"));
        return new WindowIcon(stream);
    }

    private void RestartApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("実行ファイルのパスを解決できません。");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true
            });

            _isShuttingDown = true;
            desktop.Shutdown();
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to restart application.", ex);
            _mainWindow?.AppendDiagnostics(new SessionDiagnostics(
                DateTimeOffset.Now,
                "Restart",
                $"restart-failed: {ex.Message}",
                null,
                null,
                null,
                0,
                null,
                _permissionService?.GetCurrentStatus()));
            ShowMainWindow();
        }
    }

    private void OnExit()
    {
        _isShuttingDown = true;
        if (_hotkeyService is not null)
        {
            _hotkeyService.KeyPressed -= OnGlobalKeyPressed;
        }
        _overlayWindow?.Close();
        _mainWindow?.Close();
        _trayIcon?.Dispose();
        _orchestrator?.Dispose();
    }

    private void OnGlobalKeyPressed(GlobalKeyEvent key)
    {
        var orchestrator = _orchestrator;
        if (orchestrator is null || (orchestrator.State != SessionState.OverlayActive && orchestrator.State != SessionState.Analyze))
        {
            return;
        }

        switch (key.KeyCode)
        {
            case 53: // ESC
                Dispatcher.UIThread.Post(orchestrator.CancelSession);
                return;
            case 51: // Backspace
                Dispatcher.UIThread.Post(orchestrator.HandleBackspace);
                return;
            case 36: // Enter
                Dispatcher.UIThread.Post(orchestrator.ConfirmInput);
                return;
        }

        var settings = UserSettingsNormalizer.Normalize(orchestrator.Settings);
        var shortcuts = settings.OverlayHotkeys;
        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.SwitchMonitorLeft))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SwitchMonitor(MonitorSwitchDirection.Left));
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.SwitchMonitorRight))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SwitchMonitor(MonitorSwitchDirection.Right));
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.Reanalyze))
        {
            Dispatcher.UIThread.Post(orchestrator.Reanalyze);
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.ActionLeftClick))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SetPendingAction(UiActionType.LeftClick));
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.ActionRightClick))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SetPendingAction(UiActionType.RightClick));
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.ActionDoubleClick))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SetPendingAction(UiActionType.DoubleClick));
            return;
        }

        if (KeyBindingCatalog.IsChordMatch(key, shortcuts.ActionFocus))
        {
            Dispatcher.UIThread.Post(() => orchestrator.SetPendingAction(UiActionType.Focus));
            return;
        }

        if (key.Command || key.Control || key.Option)
        {
            return;
        }

        if (KeyBindingCatalog.TryMapInputCharacter(key.KeyCode, out var ch))
        {
            Dispatcher.UIThread.Post(() => orchestrator.HandleCharacter(ch));
        }
    }

}
