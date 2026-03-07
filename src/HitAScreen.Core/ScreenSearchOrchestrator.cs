using System.Diagnostics;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public sealed class ScreenSearchOrchestrator : IDisposable
{
    private sealed record HintBinding(string Label, UiCandidate Candidate);

    private sealed class ActiveSession
    {
        public required Guid SessionId { get; init; }
        public required ActiveWindowContext Context { get; init; }
        public required DisplayInfo Display { get; set; }
        public required ScreenRect TargetBounds { get; set; }
        public required AnalysisTarget Target { get; init; }
        public required List<HintBinding> Bindings { get; set; }
        public required DateTimeOffset StartedAt { get; init; }
        public string Input { get; set; } = string.Empty;
        public UiActionType PendingAction { get; set; } = UiActionType.LeftClick;
        public long CaptureMs { get; set; }
        public long AnalyzeMs { get; set; }
        public long OverlayMs { get; set; }
    }

    private readonly object _sync = new();
    private readonly IHotkeyService _hotkeyService;
    private readonly IActiveWindowService _activeWindowService;
    private readonly IAccessibilityElementProvider _accessibilityElementProvider;
    private readonly IInputInjectionService _inputInjectionService;
    private readonly IDisplayService _displayService;
    private readonly IPermissionService _permissionService;
    private readonly ISettingsStore _settingsStore;
    private readonly IAppLogger _logger;
    private readonly HintLabelGenerator _labelGenerator = new();

    private CancellationTokenSource? _suppressionCts;
    private Task? _suppressionTask;
    private UserSettings _settings = new();
    private ActiveSession? _session;
    private SessionState _state = SessionState.Idle;
    private bool _disposed;
    private HotkeyChord? _registeredChord;
    private bool _suppressionActive;
    private HotkeyRegistrationResult _lastHotkeyRegistrationResult = new(true);

    public ScreenSearchOrchestrator(
        IHotkeyService hotkeyService,
        IActiveWindowService activeWindowService,
        IAccessibilityElementProvider accessibilityElementProvider,
        IInputInjectionService inputInjectionService,
        IDisplayService displayService,
        IPermissionService permissionService,
        ISettingsStore settingsStore,
        IAppLogger logger)
    {
        _hotkeyService = hotkeyService;
        _activeWindowService = activeWindowService;
        _accessibilityElementProvider = accessibilityElementProvider;
        _inputInjectionService = inputInjectionService;
        _displayService = displayService;
        _permissionService = permissionService;
        _settingsStore = settingsStore;
        _logger = logger;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
    }

    public event Action<OverlayViewState?>? OverlayStateChanged;
    public event Action<SessionDiagnostics>? DiagnosticsChanged;
    public event Action<UserSettings>? SettingsChanged;

    public UserSettings Settings
    {
        get
        {
            lock (_sync)
            {
                return _settings;
            }
        }
    }

    public SessionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public HotkeyRegistrationResult LastHotkeyRegistrationResult
    {
        get
        {
            lock (_sync)
            {
                return _lastHotkeyRegistrationResult;
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _settingsStore.LoadAsync(cancellationToken);
        _settings = UserSettingsNormalizer.Normalize(loaded);
        SettingsChanged?.Invoke(_settings);

        PublishDiagnostics(message: "orchestrator initialized", state: SessionState.Idle, permissions: _permissionService.GetCurrentStatus());

        await RefreshHotkeyRegistrationAsync(cancellationToken);
        StartSuppressionMonitor();
    }

    public async Task UpdateSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = UserSettingsNormalizer.Normalize(settings);
        lock (_sync)
        {
            _settings = normalized;
        }

        await _settingsStore.SaveAsync(normalized, cancellationToken);
        SettingsChanged?.Invoke(normalized);
        await RefreshHotkeyRegistrationAsync(cancellationToken);
    }

    public void StartSession()
    {
        lock (_sync)
        {
            if (_session is not null)
            {
                return;
            }

            _state = SessionState.CaptureContext;
        }

        var totalStopwatch = Stopwatch.StartNew();
        var captureStopwatch = Stopwatch.StartNew();

        var capturedContext = _activeWindowService.TryCaptureForegroundWindow();
        var context = capturedContext ?? BuildFallbackContext("foreground-window-not-available");

        if (context is null)
        {
            PublishDiagnostics(message: "failed to build session context", state: SessionState.End, permissions: _permissionService.GetCurrentStatus());
            EndSessionInternal();
            return;
        }

        context = EnsureContextHasDisplay(context);
        captureStopwatch.Stop();

        lock (_sync)
        {
            _state = SessionState.Analyze;
        }

        var analyzeStopwatch = Stopwatch.StartNew();
        var (display, targetBounds, bindings, excludedCount) = Analyze(context, _settings.DefaultAnalysisTarget);
        analyzeStopwatch.Stop();

        lock (_sync)
        {
            _session = new ActiveSession
            {
                SessionId = Guid.NewGuid(),
                Context = context,
                Display = display,
                TargetBounds = targetBounds,
                Target = _settings.DefaultAnalysisTarget,
                Bindings = bindings,
                StartedAt = DateTimeOffset.UtcNow,
                CaptureMs = captureStopwatch.ElapsedMilliseconds,
                AnalyzeMs = analyzeStopwatch.ElapsedMilliseconds
            };
            _state = SessionState.OverlayActive;
        }

        totalStopwatch.Stop();

        lock (_sync)
        {
            if (_session is not null)
            {
                _session.OverlayMs = totalStopwatch.ElapsedMilliseconds;
            }
        }

        PublishOverlay();
        PublishDiagnostics(
            message: $"session-started ax-role-filtered={excludedCount}",
            state: SessionState.OverlayActive,
            captureMs: captureStopwatch.ElapsedMilliseconds,
            analyzeMs: analyzeStopwatch.ElapsedMilliseconds,
            overlayReadyMs: totalStopwatch.ElapsedMilliseconds,
            candidateCount: bindings.Count,
            context: context,
            permissions: _permissionService.GetCurrentStatus());
    }

    public void CancelSession()
    {
        EndSessionInternal();
    }

    public void Reanalyze()
    {
        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }
        }

        ReanalyzeCurrentSession();
    }

    public void SetPendingAction(UiActionType action)
    {
        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            _session.PendingAction = action;
        }

        PublishOverlay();
    }

    public void SwitchMonitor(MonitorSwitchDirection direction)
    {
        ActiveSession? snapshot;
        lock (_sync)
        {
            snapshot = _session;
        }

        if (snapshot is null)
        {
            return;
        }

        var displays = _displayService.GetDisplays();
        if (displays.Count <= 1)
        {
            return;
        }

        var currentIndex = displays.ToList().FindIndex(display => display.Id == snapshot.Display.Id);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = direction == MonitorSwitchDirection.Left
            ? (currentIndex - 1 + displays.Count) % displays.Count
            : (currentIndex + 1) % displays.Count;

        var nextDisplay = displays[nextIndex];

        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            _session.Display = nextDisplay;
            _session.TargetBounds = _settings.DefaultAnalysisTarget == AnalysisTarget.ActiveWindow
                ? _session.Context.Bounds
                : nextDisplay.Bounds;
        }

        ReanalyzeCurrentSession();
    }

    public void HandleCharacter(char character)
    {
        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            _session.Input += char.ToUpperInvariant(character);
        }

        PublishOverlay();
        TryExecuteFromInput();
    }

    public void HandleBackspace()
    {
        lock (_sync)
        {
            if (_session is null || _session.Input.Length == 0)
            {
                return;
            }

            _session.Input = _session.Input[..^1];
        }

        PublishOverlay();
    }

    public void ConfirmInput()
    {
        TryExecuteFromInput(forceExecutePrefixMatch: true);
    }

    public async Task RefreshHotkeyRegistrationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var context = _activeWindowService.TryCaptureForegroundWindow();
        var normalizedContext = context is null ? null : EnsureContextHasDisplay(context);
        var display = normalizedContext?.DisplayId is null
            ? null
            : _displayService.GetDisplayById(normalizedContext.DisplayId);

        var shouldSuppress = SuppressionEvaluator.ShouldSuppress(_settings, normalizedContext, display);

        if (shouldSuppress)
        {
            if (_hotkeyService.IsRegistered)
            {
                _logger.Info("Suppression active; unregistering hotkey.");
                _hotkeyService.Unregister();
            }

            _registeredChord = null;
            _suppressionActive = true;
            lock (_sync)
            {
                _lastHotkeyRegistrationResult = new HotkeyRegistrationResult(true, "suppressed");
            }
            return;
        }

        var chordChanged = _registeredChord is null || _registeredChord.Value != _settings.Hotkey;
        var needRegister = !_hotkeyService.IsRegistered || chordChanged || _suppressionActive;
        _suppressionActive = false;
        if (!needRegister)
        {
            return;
        }

        if (_hotkeyService.IsRegistered && chordChanged)
        {
            _hotkeyService.Unregister();
        }

        var result = _hotkeyService.Register(_settings.Hotkey);
        if (!result.Succeeded)
        {
            lock (_sync)
            {
                _lastHotkeyRegistrationResult = result;
            }
            PublishDiagnostics(message: $"hotkey-register-failed: {result.Reason}", state: _state, permissions: _permissionService.GetCurrentStatus());
            _logger.Warn($"Failed to register hotkey: {result.Reason}");
            return;
        }

        _registeredChord = _settings.Hotkey;
        lock (_sync)
        {
            _lastHotkeyRegistrationResult = result;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _suppressionCts?.Cancel();
        _suppressionTask?.Wait(TimeSpan.FromSeconds(1));

        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Unregister();
        _hotkeyService.Dispose();
        _suppressionCts?.Dispose();
    }

    private void OnHotkeyPressed()
    {
        try
        {
            StartSession();
        }
        catch (Exception ex)
        {
            _logger.Error("Hotkey handler failed.", ex);
            PublishDiagnostics(message: "hotkey-handler-failed", state: _state, permissions: _permissionService.GetCurrentStatus());
            EndSessionInternal();
        }
    }

    private (DisplayInfo display, ScreenRect targetBounds, List<HintBinding> bindings, int excludedCount) Analyze(ActiveWindowContext context, AnalysisTarget target)
    {
        var displays = _displayService.GetDisplays();
        var targetDisplay = context.DisplayId is not null
            ? _displayService.GetDisplayById(context.DisplayId)
            : null;

        targetDisplay ??= _displayService.GetDisplayContainingPoint(context.Bounds.Center);
        targetDisplay ??= displays.FirstOrDefault() ?? new DisplayInfo("fallback", context.Bounds, context.DpiScale ?? 1.0, true);

        var targetBounds = target == AnalysisTarget.ActiveWindow ? context.Bounds : targetDisplay.Bounds;

        var analysis = new AnalysisContext(context, targetDisplay, target, targetBounds);
        var candidates = FilterAndSortCandidates(
            _accessibilityElementProvider.GetActionableElements(analysis),
            targetBounds,
            out var excludedCount);
        if (excludedCount > 0)
        {
            _logger.Info($"AX role filter applied. excluded={excludedCount}, roles={string.Join(",", _settings.ExcludedAxRoles)}");
        }

        var labels = _labelGenerator.Generate(candidates.Count, _settings.LabelCharacterSet);
        var bindings = candidates
            .Select((candidate, index) => new HintBinding(labels[index], candidate))
            .ToList();

        return (targetDisplay, targetBounds, bindings, excludedCount);
    }

    private void ReanalyzeCurrentSession()
    {
        ActiveSession? session;
        lock (_sync)
        {
            session = _session;
            if (session is null)
            {
                return;
            }

            _state = SessionState.Analyze;
        }

        var analyzeStopwatch = Stopwatch.StartNew();
        var analysis = new AnalysisContext(session.Context, session.Display, session.Target, session.TargetBounds);
        var candidates = FilterAndSortCandidates(
            _accessibilityElementProvider.GetActionableElements(analysis),
            session.TargetBounds,
            out var excludedCount);
        if (excludedCount > 0)
        {
            _logger.Info($"AX role filter applied. excluded={excludedCount}, roles={string.Join(",", _settings.ExcludedAxRoles)}");
        }

        var labels = _labelGenerator.Generate(candidates.Count, _settings.LabelCharacterSet);
        analyzeStopwatch.Stop();

        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            _session.Bindings = candidates.Select((candidate, index) => new HintBinding(labels[index], candidate)).ToList();
            _session.Input = string.Empty;
            _state = SessionState.OverlayActive;
            _session.AnalyzeMs = analyzeStopwatch.ElapsedMilliseconds;
        }

        PublishOverlay();
        PublishDiagnostics(
            message: $"session-reanalyzed ax-role-filtered={excludedCount}",
            state: SessionState.OverlayActive,
            analyzeMs: analyzeStopwatch.ElapsedMilliseconds,
            candidateCount: candidates.Count,
            context: session.Context,
            permissions: _permissionService.GetCurrentStatus());
    }

    private List<UiCandidate> FilterAndSortCandidates(
        IReadOnlyList<UiCandidate> candidates,
        ScreenRect targetBounds,
        out int excludedCount)
    {
        var excludedRoles = _settings.ExcludedAxRoles
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var inBounds = candidates
            .Where(candidate => candidate.Bounds.Intersects(targetBounds))
            .ToList();

        var filtered = inBounds
            .Where(candidate => excludedRoles.Count == 0 || !excludedRoles.Contains(candidate.Role))
            .OrderBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ToList();

        excludedCount = inBounds.Count - filtered.Count;
        return filtered;
    }

    private void TryExecuteFromInput(bool forceExecutePrefixMatch = false)
    {
        ActiveSession? session;
        lock (_sync)
        {
            session = _session;
        }

        if (session is null || session.Input.Length == 0)
        {
            return;
        }

        var exactMatch = session.Bindings.FirstOrDefault(binding =>
            string.Equals(binding.Label, session.Input, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            ExecuteBinding(exactMatch);
            return;
        }

        if (!forceExecutePrefixMatch)
        {
            return;
        }

        var prefixMatches = session.Bindings.Where(binding =>
            binding.Label.StartsWith(session.Input, StringComparison.OrdinalIgnoreCase)).ToList();

        if (prefixMatches.Count == 1)
        {
            ExecuteBinding(prefixMatches[0]);
        }
    }

    private void ExecuteBinding(HintBinding binding)
    {
        ActiveSession? snapshot;
        lock (_sync)
        {
            snapshot = _session;
            if (snapshot is null)
            {
                return;
            }

            _state = SessionState.ExecuteAction;
        }

        // マウスイベントを送出する前に、オーバーレイが対象を覆わないようにする。
        OverlayStateChanged?.Invoke(null);
        Thread.Sleep(45);

        try
        {
            _inputInjectionService.Execute(snapshot.PendingAction, binding.Candidate);
            PublishDiagnostics(
                message: $"action-executed:{snapshot.PendingAction}",
                state: SessionState.ExecuteAction,
                candidateCount: snapshot.Bindings.Count,
                context: snapshot.Context,
                permissions: _permissionService.GetCurrentStatus());
        }
        catch (Exception ex)
        {
            _logger.Error("Input injection failed.", ex);
            PublishDiagnostics(message: "action-execution-failed", state: SessionState.ExecuteAction, permissions: _permissionService.GetCurrentStatus());
        }

        lock (_sync)
        {
            if (_session is null)
            {
                return;
            }

            if (_settings.ContinuousMode)
            {
                _session.Input = string.Empty;
                _session.PendingAction = UiActionType.LeftClick;
                _state = SessionState.OverlayActive;
            }
            else
            {
                _state = SessionState.End;
            }
        }

        if (_settings.ContinuousMode)
        {
            PublishOverlay();
        }
        else
        {
            EndSessionInternal();
        }
    }

    private void EndSessionInternal()
    {
        lock (_sync)
        {
            _session = null;
            _state = SessionState.Idle;
        }

        OverlayStateChanged?.Invoke(null);
        PublishDiagnostics(message: "session-ended", state: SessionState.Idle, permissions: _permissionService.GetCurrentStatus());
    }

    private ActiveWindowContext EnsureContextHasDisplay(ActiveWindowContext context)
    {
        if (context.DisplayId is not null && context.DpiScale is not null)
        {
            return context;
        }

        var containingDisplay = _displayService.GetDisplayContainingPoint(context.Bounds.Center)
            ?? _displayService.GetDisplays().FirstOrDefault();

        return context with
        {
            DisplayId = containingDisplay?.Id,
            DpiScale = containingDisplay?.DpiScale ?? context.DpiScale ?? 1.0
        };
    }

    private ActiveWindowContext? BuildFallbackContext(string fallbackReason)
    {
        var cursor = _displayService.GetCursorPosition();
        var display = _displayService.GetDisplayContainingPoint(cursor)
            ?? _displayService.GetDisplays().FirstOrDefault();

        if (display is null)
        {
            return null;
        }

        return new ActiveWindowContext(
            WindowId: 0,
            ProcessId: 0,
            ProcessName: "unknown",
            ExecutablePath: null,
            WindowTitle: "fallback",
            Bounds: display.Bounds,
            DisplayId: display.Id,
            DpiScale: display.DpiScale,
            IsForegroundConfirmed: false,
            FallbackReason: fallbackReason);
    }

    private void PublishOverlay()
    {
        ActiveSession? session;
        lock (_sync)
        {
            session = _session;
        }

        if (session is null)
        {
            OverlayStateChanged?.Invoke(null);
            return;
        }

        var hints = HintLabelGenerator.ApplyInputFilter(
            session.Bindings.Select(binding => (binding.Label, binding.Candidate)),
            session.Input);

        var state = new OverlayViewState(
            session.SessionId,
            session.TargetBounds,
            session.Display,
            session.Input,
            session.PendingAction,
            hints,
            _settings.ContinuousMode,
            session.Target);

        OverlayStateChanged?.Invoke(state);
    }

    private void PublishDiagnostics(
        string message,
        SessionState state,
        long? captureMs = null,
        long? analyzeMs = null,
        long? overlayReadyMs = null,
        int candidateCount = 0,
        ActiveWindowContext? context = null,
        PermissionSnapshot? permissions = null)
    {
        DiagnosticsChanged?.Invoke(new SessionDiagnostics(
            DateTimeOffset.UtcNow,
            state.ToString(),
            message,
            captureMs,
            analyzeMs,
            overlayReadyMs,
            candidateCount,
            context,
            permissions));
    }

    private void StartSuppressionMonitor()
    {
        _suppressionCts = new CancellationTokenSource();
        _suppressionTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.5));
            while (!_suppressionCts.IsCancellationRequested)
            {
                try
                {
                    var hasNext = await timer.WaitForNextTickAsync(_suppressionCts.Token);
                    if (!hasNext)
                    {
                        break;
                    }

                    await RefreshHotkeyRegistrationAsync(_suppressionCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Suppression monitor tick failed.", ex);
                }
            }
        });
    }
}
