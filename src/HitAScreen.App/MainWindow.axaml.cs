using Avalonia.Controls;
using Avalonia.Interactivity;
using HitAScreen.Core;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public partial class MainWindow : Window
{
    private readonly ScreenSearchOrchestrator _orchestrator;
    private readonly IPermissionService _permissionService;
    private readonly IAppLogger _logger;

    private readonly TextBox _hotkeyTextBox;
    private readonly ComboBox _targetComboBox;
    private readonly TextBox _labelCharsetTextBox;
    private readonly TextBox _labelScaleTextBox;
    private readonly CheckBox _suppressFullscreenCheckBox;
    private readonly CheckBox _continuousModeCheckBox;
    private readonly CheckBox _debugLoggingCheckBox;
    private readonly TextBox _suppressedProcessTextBox;
    private readonly TextBlock _statusTextBlock;
    private readonly TextBox _diagnosticsTextBox;

    public MainWindow(ScreenSearchOrchestrator orchestrator, IPermissionService permissionService, IAppLogger logger)
    {
        _orchestrator = orchestrator;
        _permissionService = permissionService;
        _logger = logger;

        InitializeComponent();

        _hotkeyTextBox = this.FindControl<TextBox>("HotkeyTextBox") ?? throw new InvalidOperationException("HotkeyTextBox not found.");
        _targetComboBox = this.FindControl<ComboBox>("TargetComboBox") ?? throw new InvalidOperationException("TargetComboBox not found.");
        _labelCharsetTextBox = this.FindControl<TextBox>("LabelCharsetTextBox") ?? throw new InvalidOperationException("LabelCharsetTextBox not found.");
        _labelScaleTextBox = this.FindControl<TextBox>("LabelScaleTextBox") ?? throw new InvalidOperationException("LabelScaleTextBox not found.");
        _suppressFullscreenCheckBox = this.FindControl<CheckBox>("SuppressFullscreenCheckBox") ?? throw new InvalidOperationException("SuppressFullscreenCheckBox not found.");
        _continuousModeCheckBox = this.FindControl<CheckBox>("ContinuousModeCheckBox") ?? throw new InvalidOperationException("ContinuousModeCheckBox not found.");
        _debugLoggingCheckBox = this.FindControl<CheckBox>("DebugLoggingCheckBox") ?? throw new InvalidOperationException("DebugLoggingCheckBox not found.");
        _suppressedProcessTextBox = this.FindControl<TextBox>("SuppressedProcessTextBox") ?? throw new InvalidOperationException("SuppressedProcessTextBox not found.");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock") ?? throw new InvalidOperationException("StatusTextBlock not found.");
        _diagnosticsTextBox = this.FindControl<TextBox>("DiagnosticsTextBox") ?? throw new InvalidOperationException("DiagnosticsTextBox not found.");

        var saveButton = this.FindControl<Button>("SaveSettingsButton") ?? throw new InvalidOperationException("SaveSettingsButton not found.");
        var requestPermissionButton = this.FindControl<Button>("RequestPermissionButton") ?? throw new InvalidOperationException("RequestPermissionButton not found.");
        var startButton = this.FindControl<Button>("StartSessionButton") ?? throw new InvalidOperationException("StartSessionButton not found.");
        var reanalyzeButton = this.FindControl<Button>("ReanalyzeButton") ?? throw new InvalidOperationException("ReanalyzeButton not found.");
        var cancelButton = this.FindControl<Button>("CancelSessionButton") ?? throw new InvalidOperationException("CancelSessionButton not found.");

        saveButton.Click += SaveSettingsButtonOnClick;
        requestPermissionButton.Click += RequestPermissionButtonOnClick;
        startButton.Click += (_, _) => _orchestrator.StartSession();
        reanalyzeButton.Click += (_, _) => _orchestrator.Reanalyze();
        cancelButton.Click += (_, _) => _orchestrator.CancelSession();

        LoadSettings(orchestrator.Settings);
    }

    public void LoadSettings(UserSettings settings)
    {
        _hotkeyTextBox.Text = settings.Hotkey.DisplayText;
        _targetComboBox.SelectedIndex = settings.DefaultAnalysisTarget == AnalysisTarget.ActiveWindow ? 1 : 0;
        _labelCharsetTextBox.Text = settings.LabelCharacterSet;
        _labelScaleTextBox.Text = settings.LabelScale.ToString("0.##");
        _suppressFullscreenCheckBox.IsChecked = settings.SuppressInFullscreen;
        _continuousModeCheckBox.IsChecked = settings.ContinuousMode;
        _debugLoggingCheckBox.IsChecked = settings.DebugLoggingEnabled;
        _suppressedProcessTextBox.Text = string.Join(",", settings.SuppressedProcesses);
    }

    public void AppendDiagnostics(SessionDiagnostics diagnostics)
    {
        var line = $"[{diagnostics.Timestamp:HH:mm:ss}] {diagnostics.State} {diagnostics.Message} " +
                   $"capture={diagnostics.CaptureContextMs?.ToString() ?? "-"}ms analyze={diagnostics.AnalyzeMs?.ToString() ?? "-"}ms " +
                   $"overlay={diagnostics.OverlayReadyMs?.ToString() ?? "-"}ms candidates={diagnostics.CandidateCount}";

        if (!string.IsNullOrWhiteSpace(diagnostics.Context?.ProcessName))
        {
            line += $" process={diagnostics.Context.ProcessName}";
        }

        if (diagnostics.Permissions is not null)
        {
            line += $" perms(ax={diagnostics.Permissions.AccessibilityGranted},input={diagnostics.Permissions.InputMonitoringGranted},screen={diagnostics.Permissions.ScreenRecordingGranted})";
        }

        _diagnosticsTextBox.Text = string.Concat(line, Environment.NewLine, _diagnosticsTextBox.Text ?? string.Empty);
    }

    private async void SaveSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parsedScale = 1.0;
            if (!double.TryParse(_labelScaleTextBox.Text, out parsedScale))
            {
                parsedScale = 1.0;
            }

            parsedScale = Math.Clamp(parsedScale, 0.5, 3.0);

            var target = _targetComboBox.SelectedIndex == 1 ? AnalysisTarget.ActiveWindow : AnalysisTarget.ActiveMonitor;
            var suppressed = (_suppressedProcessTextBox.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var settings = _orchestrator.Settings with
            {
                DefaultAnalysisTarget = target,
                LabelCharacterSet = string.IsNullOrWhiteSpace(_labelCharsetTextBox.Text)
                    ? "ASDFGHJKLQWERTYUIOPZXCVBNM"
                    : _labelCharsetTextBox.Text,
                LabelScale = parsedScale,
                SuppressInFullscreen = _suppressFullscreenCheckBox.IsChecked ?? false,
                ContinuousMode = _continuousModeCheckBox.IsChecked ?? false,
                DebugLoggingEnabled = _debugLoggingCheckBox.IsChecked ?? false,
                SuppressedProcesses = suppressed
            };

            await _orchestrator.UpdateSettingsAsync(settings);
            _statusTextBlock.Text = "Settings saved.";
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings.", ex);
            _statusTextBlock.Text = $"Save failed: {ex.Message}";
        }
    }

    private void RequestPermissionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var snapshot = _permissionService.RequestMissingPermissions();
        _statusTextBlock.Text = $"Permissions ax={snapshot.AccessibilityGranted}, input={snapshot.InputMonitoringGranted}, screen={snapshot.ScreenRecordingGranted}";
    }
}
