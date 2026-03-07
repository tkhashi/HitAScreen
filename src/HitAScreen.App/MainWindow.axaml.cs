using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HitAScreen.Core;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public partial class MainWindow : Window
{
    private readonly ScreenSearchOrchestrator _orchestrator;
    private readonly IPermissionService _permissionService;
    private readonly IAppLogger _logger;
    private readonly Dictionary<string, HotkeyChord> _shortcutDraft = new(StringComparer.Ordinal);

    private readonly TextBox _hotkeyTextBox;
    private readonly TextBox _monitorLeftHotkeyTextBox;
    private readonly TextBox _monitorRightHotkeyTextBox;
    private readonly TextBox _reanalyzeHotkeyTextBox;
    private readonly TextBox _actionLeftHotkeyTextBox;
    private readonly TextBox _actionRightHotkeyTextBox;
    private readonly TextBox _actionDoubleHotkeyTextBox;
    private readonly TextBox _actionFocusHotkeyTextBox;

    private readonly ComboBox _targetComboBox;
    private readonly TextBox _labelCharsetTextBox;
    private readonly TextBox _labelScaleTextBox;
    private readonly TextBox _excludedAxRolesTextBox;
    private readonly TextBox _labelNormalColorTextBox;
    private readonly TextBox _labelMatchedColorTextBox;
    private readonly TextBox _labelOpacityTextBox;
    private readonly TextBox _labelWidthTextBox;
    private readonly TextBox _labelHeightTextBox;
    private readonly TextBox _labelFontSizeTextBox;
    private readonly CheckBox _suppressFullscreenCheckBox;
    private readonly CheckBox _continuousModeCheckBox;
    private readonly CheckBox _debugLoggingCheckBox;
    private readonly CheckBox _launchAtLoginCheckBox;
    private readonly TextBox _suppressedProcessTextBox;
    private readonly TextBlock _statusTextBlock;
    private readonly TextBox _diagnosticsTextBox;
    private readonly TextBlock _accessibilityPermissionTextBlock;
    private readonly TextBlock _inputMonitoringPermissionTextBlock;
    private readonly TextBlock _screenRecordingPermissionTextBlock;

    public MainWindow(ScreenSearchOrchestrator orchestrator, IPermissionService permissionService, IAppLogger logger)
    {
        _orchestrator = orchestrator;
        _permissionService = permissionService;
        _logger = logger;

        InitializeComponent();

        _hotkeyTextBox = RequireTextBox("HotkeyTextBox");
        _monitorLeftHotkeyTextBox = RequireTextBox("MonitorLeftHotkeyTextBox");
        _monitorRightHotkeyTextBox = RequireTextBox("MonitorRightHotkeyTextBox");
        _reanalyzeHotkeyTextBox = RequireTextBox("ReanalyzeHotkeyTextBox");
        _actionLeftHotkeyTextBox = RequireTextBox("ActionLeftHotkeyTextBox");
        _actionRightHotkeyTextBox = RequireTextBox("ActionRightHotkeyTextBox");
        _actionDoubleHotkeyTextBox = RequireTextBox("ActionDoubleHotkeyTextBox");
        _actionFocusHotkeyTextBox = RequireTextBox("ActionFocusHotkeyTextBox");

        _targetComboBox = this.FindControl<ComboBox>("TargetComboBox") ?? throw new InvalidOperationException("TargetComboBox not found.");
        _labelCharsetTextBox = RequireTextBox("LabelCharsetTextBox");
        _labelScaleTextBox = RequireTextBox("LabelScaleTextBox");
        _excludedAxRolesTextBox = RequireTextBox("ExcludedAxRolesTextBox");
        _labelNormalColorTextBox = RequireTextBox("LabelNormalColorTextBox");
        _labelMatchedColorTextBox = RequireTextBox("LabelMatchedColorTextBox");
        _labelOpacityTextBox = RequireTextBox("LabelOpacityTextBox");
        _labelWidthTextBox = RequireTextBox("LabelWidthTextBox");
        _labelHeightTextBox = RequireTextBox("LabelHeightTextBox");
        _labelFontSizeTextBox = RequireTextBox("LabelFontSizeTextBox");
        _suppressFullscreenCheckBox = RequireCheckBox("SuppressFullscreenCheckBox");
        _continuousModeCheckBox = RequireCheckBox("ContinuousModeCheckBox");
        _debugLoggingCheckBox = RequireCheckBox("DebugLoggingCheckBox");
        _launchAtLoginCheckBox = RequireCheckBox("LaunchAtLoginCheckBox");
        _suppressedProcessTextBox = RequireTextBox("SuppressedProcessTextBox");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock") ?? throw new InvalidOperationException("StatusTextBlock not found.");
        _diagnosticsTextBox = RequireTextBox("DiagnosticsTextBox");
        _accessibilityPermissionTextBlock = this.FindControl<TextBlock>("AccessibilityPermissionTextBlock") ?? throw new InvalidOperationException("AccessibilityPermissionTextBlock not found.");
        _inputMonitoringPermissionTextBlock = this.FindControl<TextBlock>("InputMonitoringPermissionTextBlock") ?? throw new InvalidOperationException("InputMonitoringPermissionTextBlock not found.");
        _screenRecordingPermissionTextBlock = this.FindControl<TextBlock>("ScreenRecordingPermissionTextBlock") ?? throw new InvalidOperationException("ScreenRecordingPermissionTextBlock not found.");

        var saveButton = RequireButton("SaveSettingsButton");
        var requestPermissionButton = RequireButton("RequestPermissionButton");
        var refreshPermissionButton = RequireButton("RefreshPermissionButton");
        var openAccessibilitySettingsButton = RequireButton("OpenAccessibilitySettingsButton");
        var openInputMonitoringSettingsButton = RequireButton("OpenInputMonitoringSettingsButton");
        var openScreenRecordingSettingsButton = RequireButton("OpenScreenRecordingSettingsButton");
        var startButton = RequireButton("StartSessionButton");
        var reanalyzeButton = RequireButton("ReanalyzeButton");
        var cancelButton = RequireButton("CancelSessionButton");

        saveButton.Click += SaveSettingsButtonOnClick;
        requestPermissionButton.Click += RequestPermissionButtonOnClick;
        refreshPermissionButton.Click += (_, _) => RefreshPermissionStatus();
        openAccessibilitySettingsButton.Click += (_, _) => OpenPermissionSettings(PermissionArea.Accessibility);
        openInputMonitoringSettingsButton.Click += (_, _) => OpenPermissionSettings(PermissionArea.InputMonitoring);
        openScreenRecordingSettingsButton.Click += (_, _) => OpenPermissionSettings(PermissionArea.ScreenRecording);
        startButton.Click += (_, _) => _orchestrator.StartSession();
        reanalyzeButton.Click += (_, _) => _orchestrator.Reanalyze();
        cancelButton.Click += (_, _) => _orchestrator.CancelSession();

        RegisterShortcutCapture(_hotkeyTextBox);
        RegisterShortcutCapture(_monitorLeftHotkeyTextBox);
        RegisterShortcutCapture(_monitorRightHotkeyTextBox);
        RegisterShortcutCapture(_reanalyzeHotkeyTextBox);
        RegisterShortcutCapture(_actionLeftHotkeyTextBox);
        RegisterShortcutCapture(_actionRightHotkeyTextBox);
        RegisterShortcutCapture(_actionDoubleHotkeyTextBox);
        RegisterShortcutCapture(_actionFocusHotkeyTextBox);

        LoadSettings(orchestrator.Settings);
        RefreshPermissionStatus();
    }

    public void LoadSettings(UserSettings settings)
    {
        var normalized = UserSettingsNormalizer.Normalize(settings);

        SetShortcutDraft(_hotkeyTextBox, normalized.Hotkey);
        SetShortcutDraft(_monitorLeftHotkeyTextBox, normalized.OverlayHotkeys.SwitchMonitorLeft);
        SetShortcutDraft(_monitorRightHotkeyTextBox, normalized.OverlayHotkeys.SwitchMonitorRight);
        SetShortcutDraft(_reanalyzeHotkeyTextBox, normalized.OverlayHotkeys.Reanalyze);
        SetShortcutDraft(_actionLeftHotkeyTextBox, normalized.OverlayHotkeys.ActionLeftClick);
        SetShortcutDraft(_actionRightHotkeyTextBox, normalized.OverlayHotkeys.ActionRightClick);
        SetShortcutDraft(_actionDoubleHotkeyTextBox, normalized.OverlayHotkeys.ActionDoubleClick);
        SetShortcutDraft(_actionFocusHotkeyTextBox, normalized.OverlayHotkeys.ActionFocus);

        _targetComboBox.SelectedIndex = normalized.DefaultAnalysisTarget == AnalysisTarget.ActiveWindow ? 1 : 0;
        _labelCharsetTextBox.Text = normalized.LabelCharacterSet;
        _labelScaleTextBox.Text = normalized.LabelScale.ToString("0.##");
        _excludedAxRolesTextBox.Text = string.Join(", ", normalized.ExcludedAxRoles);

        _labelNormalColorTextBox.Text = normalized.LabelAppearance.NormalBackgroundColor;
        _labelMatchedColorTextBox.Text = normalized.LabelAppearance.MatchedBackgroundColor;
        _labelOpacityTextBox.Text = normalized.LabelAppearance.Opacity.ToString("0.##");
        _labelWidthTextBox.Text = normalized.LabelAppearance.LabelWidth.ToString("0.##");
        _labelHeightTextBox.Text = normalized.LabelAppearance.LabelHeight.ToString("0.##");
        _labelFontSizeTextBox.Text = normalized.LabelAppearance.FontSize.ToString("0.##");

        _suppressFullscreenCheckBox.IsChecked = normalized.SuppressInFullscreen;
        _continuousModeCheckBox.IsChecked = normalized.ContinuousMode;
        _debugLoggingCheckBox.IsChecked = normalized.DebugLoggingEnabled;
        _launchAtLoginCheckBox.IsChecked = normalized.LaunchAtLogin;
        _suppressedProcessTextBox.Text = string.Join(",", normalized.SuppressedProcesses);
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
            var current = _orchestrator.Settings;
            var target = _targetComboBox.SelectedIndex == 1 ? AnalysisTarget.ActiveWindow : AnalysisTarget.ActiveMonitor;
            var suppressed = (_suppressedProcessTextBox.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var excludedRoles = (_excludedAxRolesTextBox.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var overlayHotkeys = current.OverlayHotkeys with
            {
                SwitchMonitorLeft = GetShortcutDraft(_monitorLeftHotkeyTextBox, current.OverlayHotkeys.SwitchMonitorLeft),
                SwitchMonitorRight = GetShortcutDraft(_monitorRightHotkeyTextBox, current.OverlayHotkeys.SwitchMonitorRight),
                Reanalyze = GetShortcutDraft(_reanalyzeHotkeyTextBox, current.OverlayHotkeys.Reanalyze),
                ActionLeftClick = GetShortcutDraft(_actionLeftHotkeyTextBox, current.OverlayHotkeys.ActionLeftClick),
                ActionRightClick = GetShortcutDraft(_actionRightHotkeyTextBox, current.OverlayHotkeys.ActionRightClick),
                ActionDoubleClick = GetShortcutDraft(_actionDoubleHotkeyTextBox, current.OverlayHotkeys.ActionDoubleClick),
                ActionFocus = GetShortcutDraft(_actionFocusHotkeyTextBox, current.OverlayHotkeys.ActionFocus)
            };

            var settings = current with
            {
                Hotkey = GetShortcutDraft(_hotkeyTextBox, current.Hotkey),
                OverlayHotkeys = overlayHotkeys,
                DefaultAnalysisTarget = target,
                LabelCharacterSet = string.IsNullOrWhiteSpace(_labelCharsetTextBox.Text)
                    ? "ASDFGHJKLQWERTYUIOPZXCVBNM"
                    : _labelCharsetTextBox.Text,
                LabelScale = ParseDoubleWithClamp(_labelScaleTextBox.Text, 1.0, 0.5, 3.0),
                LabelAppearance = new LabelAppearanceSettings
                {
                    NormalBackgroundColor = _labelNormalColorTextBox.Text ?? "#5A5A5A",
                    MatchedBackgroundColor = _labelMatchedColorTextBox.Text ?? "#FFD65C",
                    Opacity = ParseDoubleWithClamp(_labelOpacityTextBox.Text, 0.85, 0.1, 1.0),
                    LabelWidth = ParseDoubleWithClamp(_labelWidthTextBox.Text, 44, 20, 180),
                    LabelHeight = ParseDoubleWithClamp(_labelHeightTextBox.Text, 26, 16, 120),
                    FontSize = ParseDoubleWithClamp(_labelFontSizeTextBox.Text, 14, 8, 48)
                },
                ExcludedAxRoles = excludedRoles,
                SuppressInFullscreen = _suppressFullscreenCheckBox.IsChecked ?? false,
                ContinuousMode = _continuousModeCheckBox.IsChecked ?? false,
                DebugLoggingEnabled = _debugLoggingCheckBox.IsChecked ?? false,
                LaunchAtLogin = _launchAtLoginCheckBox.IsChecked ?? false,
                SuppressedProcesses = suppressed
            };

            await _orchestrator.UpdateSettingsAsync(settings);
            _statusTextBlock.Text = "Settings saved.";
            RefreshPermissionStatus();
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
        UpdatePermissionStatus(snapshot);
        _statusTextBlock.Text = $"Permissions ax={snapshot.AccessibilityGranted}, input={snapshot.InputMonitoringGranted}, screen={snapshot.ScreenRecordingGranted}";
    }

    private void RefreshPermissionStatus()
    {
        UpdatePermissionStatus(_permissionService.GetCurrentStatus());
    }

    private void UpdatePermissionStatus(PermissionSnapshot snapshot)
    {
        _accessibilityPermissionTextBlock.Text = $"Accessibility: {(snapshot.AccessibilityGranted ? "許可済み" : "未許可")}";
        _inputMonitoringPermissionTextBlock.Text = $"Input Monitoring: {(snapshot.InputMonitoringGranted ? "許可済み" : "未許可")}";
        _screenRecordingPermissionTextBlock.Text = $"Screen Recording: {(snapshot.ScreenRecordingGranted ? "許可済み" : "未許可")}";
    }

    private void OpenPermissionSettings(PermissionArea area)
    {
        if (_permissionService.OpenSystemSettings(area, out var error))
        {
            _statusTextBlock.Text = $"{area} の設定画面を開きました。";
            return;
        }

        _statusTextBlock.Text = string.IsNullOrWhiteSpace(error)
            ? $"{area} の設定画面を開けませんでした。"
            : error;
    }

    private void RegisterShortcutCapture(TextBox textBox)
    {
        textBox.KeyDown += ShortcutTextBoxOnKeyDown;
    }

    private void ShortcutTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!KeyBindingCatalog.TryCreateChordFromAvalonia(e, out var chord))
        {
            _statusTextBlock.Text = "このキーはショートカットに設定できません。";
            return;
        }

        SetShortcutDraft(textBox, chord);
        _statusTextBlock.Text = $"{ResolveShortcutLabel(textBox)} を {chord.DisplayText} に設定しました。保存で反映されます。";
        e.Handled = true;
    }

    private void SetShortcutDraft(TextBox textBox, HotkeyChord chord)
    {
        _shortcutDraft[textBox.Name ?? string.Empty] = chord;
        textBox.Text = chord.DisplayText;
    }

    private HotkeyChord GetShortcutDraft(TextBox textBox, HotkeyChord fallback)
    {
        var key = textBox.Name ?? string.Empty;
        return _shortcutDraft.TryGetValue(key, out var value) ? value : fallback;
    }

    private static double ParseDoubleWithClamp(string? raw, double fallback, double min, double max)
    {
        if (!double.TryParse(raw, out var parsed))
        {
            parsed = fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    private string ResolveShortcutLabel(TextBox textBox)
    {
        return textBox.Name switch
        {
            "HotkeyTextBox" => "起動ホットキー",
            "MonitorLeftHotkeyTextBox" => "モニタ左切替",
            "MonitorRightHotkeyTextBox" => "モニタ右切替",
            "ReanalyzeHotkeyTextBox" => "再解析",
            "ActionLeftHotkeyTextBox" => "左クリックアクション",
            "ActionRightHotkeyTextBox" => "右クリックアクション",
            "ActionDoubleHotkeyTextBox" => "ダブルクリックアクション",
            "ActionFocusHotkeyTextBox" => "フォーカスアクション",
            _ => "ショートカット"
        };
    }

    private TextBox RequireTextBox(string name) =>
        this.FindControl<TextBox>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private CheckBox RequireCheckBox(string name) =>
        this.FindControl<CheckBox>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private Button RequireButton(string name) =>
        this.FindControl<Button>(name) ?? throw new InvalidOperationException($"{name} not found.");
}
