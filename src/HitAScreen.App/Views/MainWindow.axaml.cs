using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using HitAScreen.Core;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public partial class MainWindow : Window
{
    private const string HotkeyId = "Hotkey";
    private const string MonitorLeftId = "MonitorLeft";
    private const string MonitorRightId = "MonitorRight";
    private const string ReanalyzeId = "Reanalyze";
    private const string ActionLeftId = "ActionLeft";
    private const string ActionRightId = "ActionRight";
    private const string ActionDoubleId = "ActionDouble";
    private const string ActionFocusId = "ActionFocus";
    private const int MaxRecentColorCount = 10;

    private static readonly string[] ShortcutIds =
    [
        HotkeyId,
        MonitorLeftId,
        MonitorRightId,
        ReanalyzeId,
        ActionLeftId,
        ActionRightId,
        ActionDoubleId,
        ActionFocusId
    ];

    private static readonly string[] ExcludableAxRoles =
    [
        "AXGroup",
        "AXButton",
        "AXLink",
        "AXMenuItem",
        "AXTextField",
        "AXCheckBox",
        "AXRadioButton",
        "AXPopUpButton",
        "AXDisclosureTriangle"
    ];

    private readonly ScreenSearchOrchestrator _orchestrator;
    private readonly IPermissionService _permissionService;
    private readonly ILaunchAtLoginService _launchAtLoginService;
    private readonly IAppLogger _logger;

    private readonly Dictionary<string, HotkeyChord> _shortcutDraft = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shortcutLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _shortcutButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _shortcutErrorTextBlocks = new(StringComparer.Ordinal);
    private readonly List<string> _recentLabelColors = [];

    private readonly ComboBox _targetComboBox;
    private readonly TextBox _labelCharsetTextBox;
    private readonly Slider _labelScaleSlider;
    private readonly TextBlock _labelScaleValueTextBlock;
    private readonly Dictionary<string, CheckBox> _excludedRoleCheckBoxes = new(StringComparer.Ordinal);
    private readonly ColorPicker _labelNormalColorPicker;
    private readonly TextBlock _labelNormalColorValueTextBlock;
    private readonly ColorPicker _labelMatchedColorPicker;
    private readonly TextBlock _labelMatchedColorValueTextBlock;
    private readonly Slider _labelOpacitySlider;
    private readonly TextBlock _labelOpacityValueTextBlock;
    private readonly WrapPanel _normalRecentColorsPanel;
    private readonly WrapPanel _matchedRecentColorsPanel;
    private readonly Grid _previewLayoutGrid;
    private readonly Border _previewNormalLabelBorder;
    private readonly Border _previewMatchedLabelBorder;
    private readonly TextBlock _previewNormalLabelTextBlock;
    private readonly TextBlock _previewMatchedLabelTextBlock;
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

    private string? _capturingShortcutId;
    private bool _suspendAppearanceEvents;
    private double _baseLabelFontSize = 14;

    public MainWindow(
        ScreenSearchOrchestrator orchestrator,
        IPermissionService permissionService,
        ILaunchAtLoginService launchAtLoginService,
        IAppLogger logger)
    {
        _orchestrator = orchestrator;
        _permissionService = permissionService;
        _launchAtLoginService = launchAtLoginService;
        _logger = logger;

        InitializeComponent();

        _targetComboBox = this.FindControl<ComboBox>("TargetComboBox") ?? throw new InvalidOperationException("TargetComboBox not found.");
        _labelCharsetTextBox = RequireTextBox("LabelCharsetTextBox");
        _labelScaleSlider = RequireSlider("LabelScaleSlider");
        _labelScaleValueTextBlock = RequireTextBlock("LabelScaleValueTextBlock");
        _excludedRoleCheckBoxes["AXGroup"] = RequireCheckBox("ExcludeAxGroupCheckBox");
        _excludedRoleCheckBoxes["AXButton"] = RequireCheckBox("ExcludeAxButtonCheckBox");
        _excludedRoleCheckBoxes["AXLink"] = RequireCheckBox("ExcludeAxLinkCheckBox");
        _excludedRoleCheckBoxes["AXMenuItem"] = RequireCheckBox("ExcludeAxMenuItemCheckBox");
        _excludedRoleCheckBoxes["AXTextField"] = RequireCheckBox("ExcludeAxTextFieldCheckBox");
        _excludedRoleCheckBoxes["AXCheckBox"] = RequireCheckBox("ExcludeAxCheckBoxCheckBox");
        _excludedRoleCheckBoxes["AXRadioButton"] = RequireCheckBox("ExcludeAxRadioButtonCheckBox");
        _excludedRoleCheckBoxes["AXPopUpButton"] = RequireCheckBox("ExcludeAxPopUpButtonCheckBox");
        _excludedRoleCheckBoxes["AXDisclosureTriangle"] = RequireCheckBox("ExcludeAxDisclosureTriangleCheckBox");
        _labelNormalColorPicker = RequireColorPicker("LabelNormalColorPicker");
        _labelNormalColorValueTextBlock = RequireTextBlock("LabelNormalColorValueTextBlock");
        _labelMatchedColorPicker = RequireColorPicker("LabelMatchedColorPicker");
        _labelMatchedColorValueTextBlock = RequireTextBlock("LabelMatchedColorValueTextBlock");
        _labelOpacitySlider = RequireSlider("LabelOpacitySlider");
        _labelOpacityValueTextBlock = RequireTextBlock("LabelOpacityValueTextBlock");
        _normalRecentColorsPanel = RequireWrapPanel("NormalRecentColorsPanel");
        _matchedRecentColorsPanel = RequireWrapPanel("MatchedRecentColorsPanel");
        _previewLayoutGrid = RequireGrid("PreviewLayoutGrid");
        _previewNormalLabelBorder = RequireBorder("PreviewNormalLabelBorder");
        _previewMatchedLabelBorder = RequireBorder("PreviewMatchedLabelBorder");
        _previewNormalLabelTextBlock = RequireTextBlock("PreviewNormalLabelTextBlock");
        _previewMatchedLabelTextBlock = RequireTextBlock("PreviewMatchedLabelTextBlock");
        _suppressFullscreenCheckBox = RequireCheckBox("SuppressFullscreenCheckBox");
        _continuousModeCheckBox = RequireCheckBox("ContinuousModeCheckBox");
        _debugLoggingCheckBox = RequireCheckBox("DebugLoggingCheckBox");
        _launchAtLoginCheckBox = RequireCheckBox("LaunchAtLoginCheckBox");
        _suppressedProcessTextBox = RequireTextBox("SuppressedProcessTextBox");
        _statusTextBlock = RequireTextBlock("StatusTextBlock");
        _diagnosticsTextBox = RequireTextBox("DiagnosticsTextBox");
        _accessibilityPermissionTextBlock = RequireTextBlock("AccessibilityPermissionTextBlock");
        _inputMonitoringPermissionTextBlock = RequireTextBlock("InputMonitoringPermissionTextBlock");
        _screenRecordingPermissionTextBlock = RequireTextBlock("ScreenRecordingPermissionTextBlock");

        RegisterShortcutControls();
        AddHandler(KeyDownEvent, ShortcutCaptureOnKeyDown, RoutingStrategies.Tunnel);

        RegisterAppearanceHandlers();

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

        LoadSettings(orchestrator.Settings);
        RefreshPermissionStatus();
    }

    public void LoadSettings(UserSettings settings)
    {
        var normalized = UserSettingsNormalizer.Normalize(settings);

        SetShortcutDraft(HotkeyId, normalized.Hotkey);
        SetShortcutDraft(MonitorLeftId, normalized.OverlayHotkeys.SwitchMonitorLeft);
        SetShortcutDraft(MonitorRightId, normalized.OverlayHotkeys.SwitchMonitorRight);
        SetShortcutDraft(ReanalyzeId, normalized.OverlayHotkeys.Reanalyze);
        SetShortcutDraft(ActionLeftId, normalized.OverlayHotkeys.ActionLeftClick);
        SetShortcutDraft(ActionRightId, normalized.OverlayHotkeys.ActionRightClick);
        SetShortcutDraft(ActionDoubleId, normalized.OverlayHotkeys.ActionDoubleClick);
        SetShortcutDraft(ActionFocusId, normalized.OverlayHotkeys.ActionFocus);
        UpdateShortcutConflictErrors();

        _targetComboBox.SelectedIndex = normalized.DefaultAnalysisTarget == AnalysisTarget.ActiveWindow ? 1 : 0;
        _labelCharsetTextBox.Text = normalized.LabelCharacterSet;
        SetExcludedRoleCheckStates(normalized.ExcludedAxRoles);

        _suspendAppearanceEvents = true;
        _labelScaleSlider.Value = normalized.LabelScale;
        _labelOpacitySlider.Value = normalized.LabelAppearance.Opacity;
        _baseLabelFontSize = normalized.LabelAppearance.FontSize;
        _labelNormalColorPicker.Color = ParseColorOrFallback(normalized.LabelAppearance.NormalBackgroundColor, "#5A5A5A");
        _labelMatchedColorPicker.Color = ParseColorOrFallback(normalized.LabelAppearance.MatchedBackgroundColor, "#FFD65C");
        InitializeRecentColors(
            normalized.RecentLabelColors,
            ToColorHex(_labelNormalColorPicker.Color),
            ToColorHex(_labelMatchedColorPicker.Color));
        _suspendAppearanceEvents = false;

        _suppressFullscreenCheckBox.IsChecked = normalized.SuppressInFullscreen;
        _continuousModeCheckBox.IsChecked = normalized.ContinuousMode;
        _debugLoggingCheckBox.IsChecked = normalized.DebugLoggingEnabled;
        _launchAtLoginCheckBox.IsChecked = normalized.LaunchAtLogin || _launchAtLoginService.IsEnabled();
        _suppressedProcessTextBox.Text = string.Join(",", normalized.SuppressedProcesses);

        RefreshRecentColorButtons();
        UpdateAppearancePreview();
    }

    public void AppendDiagnostics(SessionDiagnostics diagnostics)
    {
        var line = $"[{diagnostics.Timestamp:HH:mm:ss}] {diagnostics.State} {diagnostics.Message} " +
                   $"capture={diagnostics.CaptureContextMs?.ToString() ?? "-"}ms analyze={diagnostics.AnalyzeMs?.ToString() ?? "-"}ms " +
                   $"overlay={diagnostics.OverlayReadyMs?.ToString() ?? "-"}ms candidates={diagnostics.CandidateCount}";

        if (!string.IsNullOrWhiteSpace(diagnostics.Context?.ProcessName))
        {
            line += $" process={diagnostics.Context.ProcessName}";
            line += $" pid={diagnostics.Context.ProcessId}";
            line += $" windowId={diagnostics.Context.WindowId}";
            line += $" title={TrimWithLimit(diagnostics.Context.WindowTitle, 18)}";
            line += $" bounds=({diagnostics.Context.Bounds.X:0},{diagnostics.Context.Bounds.Y:0},{diagnostics.Context.Bounds.Width:0},{diagnostics.Context.Bounds.Height:0})";
            line += $" display={diagnostics.Context.DisplayId ?? "-"}";
            if (!string.IsNullOrWhiteSpace(diagnostics.Context.FallbackReason))
            {
                line += $" fallback={diagnostics.Context.FallbackReason}";
            }
        }

        if (diagnostics.Permissions is not null)
        {
            line += $" perms(ax={diagnostics.Permissions.AccessibilityGranted},input={diagnostics.Permissions.InputMonitoringGranted},screen={diagnostics.Permissions.ScreenRecordingGranted})";
            if (!string.IsNullOrWhiteSpace(diagnostics.Permissions.Note))
            {
                line += $" perm-note={diagnostics.Permissions.Note}";
            }
        }

        _diagnosticsTextBox.Text = string.Concat(line, Environment.NewLine, _diagnosticsTextBox.Text ?? string.Empty);
    }

    private void RegisterAppearanceHandlers()
    {
        _labelScaleSlider.ValueChanged += AppearanceSliderOnValueChanged;
        _labelOpacitySlider.ValueChanged += AppearanceSliderOnValueChanged;
        _labelNormalColorPicker.ColorChanged += LabelNormalColorPickerOnColorChanged;
        _labelMatchedColorPicker.ColorChanged += LabelMatchedColorPickerOnColorChanged;
    }

    private void AppearanceSliderOnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suspendAppearanceEvents)
        {
            return;
        }

        UpdateAppearancePreview();
    }

    private void LabelNormalColorPickerOnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        if (_suspendAppearanceEvents)
        {
            return;
        }

        AddRecentColor(ToColorHex(e.NewColor));
        UpdateAppearancePreview();
    }

    private void LabelMatchedColorPickerOnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        if (_suspendAppearanceEvents)
        {
            return;
        }

        AddRecentColor(ToColorHex(e.NewColor));
        UpdateAppearancePreview();
    }

    private void UpdateAppearancePreview()
    {
        // UI-02 例外: 設定値(色・透明度・スケール)に応じて実行時に見た目を算出する必要がある。
        var scale = _labelScaleSlider.Value;
        var opacity = _labelOpacitySlider.Value;

        _labelScaleValueTextBlock.Text = scale.ToString("0.00");
        _labelOpacityValueTextBlock.Text = opacity.ToString("0.00");

        var normalColor = _labelNormalColorPicker.Color;
        var matchedColor = _labelMatchedColorPicker.Color;
        _labelNormalColorValueTextBlock.Text = ToColorHex(normalColor);
        _labelMatchedColorValueTextBlock.Text = ToColorHex(matchedColor);

        var appearance = new LabelAppearanceSettings { FontSize = _baseLabelFontSize };
        var metrics = LabelAppearanceMetrics.Calculate(appearance, scale);

        _previewNormalLabelBorder.Width = double.NaN;
        _previewNormalLabelBorder.Height = double.NaN;
        _previewMatchedLabelBorder.Width = double.NaN;
        _previewMatchedLabelBorder.Height = double.NaN;
        _previewNormalLabelBorder.MinWidth = metrics.MinWidth;
        _previewNormalLabelBorder.MinHeight = metrics.MinHeight;
        _previewMatchedLabelBorder.MinWidth = metrics.MinWidth;
        _previewMatchedLabelBorder.MinHeight = metrics.MinHeight;
        _previewNormalLabelBorder.Padding = new Thickness(metrics.HorizontalPadding, metrics.VerticalPadding);
        _previewMatchedLabelBorder.Padding = new Thickness(metrics.HorizontalPadding, metrics.VerticalPadding);

        _previewNormalLabelTextBlock.FontSize = metrics.FontSize;
        _previewMatchedLabelTextBlock.FontSize = metrics.FontSize;

        var normalPreviewColor = WithOpacity(normalColor, opacity);
        var matchedPreviewColor = WithOpacity(matchedColor, opacity);

        _previewNormalLabelBorder.Background = new SolidColorBrush(normalPreviewColor);
        _previewMatchedLabelBorder.Background = new SolidColorBrush(matchedPreviewColor);

        _previewNormalLabelTextBlock.Foreground = ResolveReadableBrush(normalPreviewColor);
        _previewMatchedLabelTextBlock.Foreground = ResolveReadableBrush(matchedPreviewColor);

        _previewLayoutGrid.ColumnSpacing = Math.Clamp(metrics.HorizontalPadding * 0.8, 8, 20);
    }

    private static IBrush ResolveReadableBrush(Color color)
    {
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;
        return luminance >= 0.58 ? Brushes.Black : Brushes.White;
    }

    private void InitializeRecentColors(IReadOnlyList<string> savedColors, params string[] mustInclude)
    {
        _recentLabelColors.Clear();

        foreach (var color in savedColors)
        {
            AddRecentColor(color, insertAtFront: false, refresh: false);
        }

        foreach (var color in mustInclude)
        {
            AddRecentColor(color, insertAtFront: true, refresh: false);
        }
    }

    private void AddRecentColor(string colorHex, bool insertAtFront = true, bool refresh = true)
    {
        var normalized = NormalizeColorHex(colorHex);
        if (normalized is null)
        {
            return;
        }

        _recentLabelColors.RemoveAll(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));
        if (insertAtFront)
        {
            _recentLabelColors.Insert(0, normalized);
        }
        else
        {
            _recentLabelColors.Add(normalized);
        }

        if (_recentLabelColors.Count > MaxRecentColorCount)
        {
            _recentLabelColors.RemoveRange(MaxRecentColorCount, _recentLabelColors.Count - MaxRecentColorCount);
        }

        if (refresh)
        {
            RefreshRecentColorButtons();
        }
    }

    private void RefreshRecentColorButtons()
    {
        RebuildRecentColorButtons(_normalRecentColorsPanel, color => _labelNormalColorPicker.Color = color);
        RebuildRecentColorButtons(_matchedRecentColorsPanel, color => _labelMatchedColorPicker.Color = color);
    }

    private void RebuildRecentColorButtons(WrapPanel panel, Action<Color> applyColor)
    {
        // UI-02 例外: ユーザーが選択した色履歴から実行時にボタンを動的生成する。
        panel.Children.Clear();

        foreach (var colorHex in _recentLabelColors)
        {
            if (!Color.TryParse(colorHex, out var color))
            {
                continue;
            }

            var button = new Button
            {
                Classes = { "recent-color-swatch" },
                Background = new SolidColorBrush(color)
            };

            ToolTip.SetTip(button, colorHex);
            button.Click += (_, _) => applyColor(color);
            panel.Children.Add(button);
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "履歴なし",
                Classes = { "recent-color-empty" }
            });
        }
    }

    private static string? NormalizeColorHex(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var raw = color.Trim();
        if (!raw.StartsWith('#'))
        {
            raw = "#" + raw;
        }

        if (!Color.TryParse(raw, out var parsed))
        {
            return null;
        }

        return ToColorHex(parsed);
    }

    private static Color ParseColorOrFallback(string raw, string fallback)
    {
        if (Color.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        if (Color.TryParse(fallback, out var fallbackColor))
        {
            return fallbackColor;
        }

        return Colors.Gray;
    }

    private static string ToColorHex(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static Color WithOpacity(Color color, double opacity)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(color.A * opacity), 0, 255);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private void RegisterShortcutControls()
    {
        var defaults = UserSettingsNormalizer.Normalize(new UserSettings());

        RegisterShortcutControl(HotkeyId, "起動ホットキー", RequireButton("HotkeyCaptureButton"), RequireButton("HotkeyResetButton"), RequireTextBlock("HotkeyErrorTextBlock"), defaults.Hotkey);
        RegisterShortcutControl(MonitorLeftId, "モニタ左切替", RequireButton("MonitorLeftCaptureButton"), RequireButton("MonitorLeftResetButton"), RequireTextBlock("MonitorLeftErrorTextBlock"), defaults.OverlayHotkeys.SwitchMonitorLeft);
        RegisterShortcutControl(MonitorRightId, "モニタ右切替", RequireButton("MonitorRightCaptureButton"), RequireButton("MonitorRightResetButton"), RequireTextBlock("MonitorRightErrorTextBlock"), defaults.OverlayHotkeys.SwitchMonitorRight);
        RegisterShortcutControl(ReanalyzeId, "再解析", RequireButton("ReanalyzeCaptureButton"), RequireButton("ReanalyzeResetButton"), RequireTextBlock("ReanalyzeErrorTextBlock"), defaults.OverlayHotkeys.Reanalyze);
        RegisterShortcutControl(ActionLeftId, "左クリックアクション", RequireButton("ActionLeftCaptureButton"), RequireButton("ActionLeftResetButton"), RequireTextBlock("ActionLeftErrorTextBlock"), defaults.OverlayHotkeys.ActionLeftClick);
        RegisterShortcutControl(ActionRightId, "右クリックアクション", RequireButton("ActionRightCaptureButton"), RequireButton("ActionRightResetButton"), RequireTextBlock("ActionRightErrorTextBlock"), defaults.OverlayHotkeys.ActionRightClick);
        RegisterShortcutControl(ActionDoubleId, "ダブルクリックアクション", RequireButton("ActionDoubleCaptureButton"), RequireButton("ActionDoubleResetButton"), RequireTextBlock("ActionDoubleErrorTextBlock"), defaults.OverlayHotkeys.ActionDoubleClick);
        RegisterShortcutControl(ActionFocusId, "フォーカスアクション", RequireButton("ActionFocusCaptureButton"), RequireButton("ActionFocusResetButton"), RequireTextBlock("ActionFocusErrorTextBlock"), defaults.OverlayHotkeys.ActionFocus);
    }

    private void RegisterShortcutControl(
        string shortcutId,
        string label,
        Button captureButton,
        Button resetButton,
        TextBlock errorTextBlock,
        HotkeyChord defaultChord)
    {
        _shortcutLabels[shortcutId] = label;
        _shortcutButtons[shortcutId] = captureButton;
        _shortcutErrorTextBlocks[shortcutId] = errorTextBlock;

        captureButton.Click += (_, _) => BeginShortcutCapture(shortcutId);
        resetButton.Click += (_, _) =>
        {
            SetShortcutDraft(shortcutId, defaultChord);
            UpdateShortcutConflictErrors();
            _statusTextBlock.Text = $"{label} を既定値 {defaultChord.DisplayText} に戻しました。保存で反映されます。";
        };
    }

    private void BeginShortcutCapture(string shortcutId)
    {
        if (_capturingShortcutId is not null && _capturingShortcutId != shortcutId)
        {
            EndShortcutCapture();
        }

        _capturingShortcutId = shortcutId;

        if (_shortcutButtons.TryGetValue(shortcutId, out var button))
        {
            button.Content = "入力待ち... (Escでキャンセル)";
        }

        ClearShortcutError(shortcutId);
        _statusTextBlock.Text = $"{ResolveShortcutLabel(shortcutId)} の入力待ちです。";
        Focus();
    }

    private void EndShortcutCapture()
    {
        if (_capturingShortcutId is null)
        {
            return;
        }

        var activeShortcutId = _capturingShortcutId;
        _capturingShortcutId = null;
        RefreshShortcutButtonText(activeShortcutId);
    }

    private void ShortcutCaptureOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturingShortcutId is null)
        {
            return;
        }

        var activeShortcutId = _capturingShortcutId;

        if (e.Key == Key.Escape)
        {
            EndShortcutCapture();
            _statusTextBlock.Text = $"{ResolveShortcutLabel(activeShortcutId)} の変更をキャンセルしました。";
            e.Handled = true;
            return;
        }

        if (!KeyBindingCatalog.TryCreateChordFromAvalonia(e, out var chord))
        {
            SetShortcutError(activeShortcutId, "このキーはショートカットに設定できません。別のキーを入力してください。");
            _statusTextBlock.Text = "このキーはショートカットに設定できません。";
            e.Handled = true;
            return;
        }

        if (IsReservedShortcut(chord))
        {
            SetShortcutError(activeShortcutId, "固定キー (ESC/Enter/Backspace) は設定できません。別のキーを入力してください。");
            _statusTextBlock.Text = "固定キー (ESC/Enter/Backspace) は設定できません。";
            e.Handled = true;
            return;
        }

        SetShortcutDraft(activeShortcutId, chord);
        EndShortcutCapture();
        UpdateShortcutConflictErrors();

        if (HasShortcutError(activeShortcutId))
        {
            _statusTextBlock.Text = $"{ResolveShortcutLabel(activeShortcutId)} を {chord.DisplayText} に設定しましたが、競合があります。";
        }
        else
        {
            _statusTextBlock.Text = $"{ResolveShortcutLabel(activeShortcutId)} を {chord.DisplayText} に設定しました。保存で反映されます。";
        }

        e.Handled = true;
    }

    private async void SaveSettingsButtonOnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            EndShortcutCapture();

            AddRecentColor(ToColorHex(_labelNormalColorPicker.Color), refresh: false);
            AddRecentColor(ToColorHex(_labelMatchedColorPicker.Color), refresh: false);
            RefreshRecentColorButtons();

            var current = _orchestrator.Settings;
            var target = _targetComboBox.SelectedIndex == 1 ? AnalysisTarget.ActiveWindow : AnalysisTarget.ActiveMonitor;
            var suppressed = (_suppressedProcessTextBox.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var excludedRoles = GetSelectedExcludedRoles();

            var overlayHotkeys = current.OverlayHotkeys with
            {
                SwitchMonitorLeft = GetShortcutDraft(MonitorLeftId, current.OverlayHotkeys.SwitchMonitorLeft),
                SwitchMonitorRight = GetShortcutDraft(MonitorRightId, current.OverlayHotkeys.SwitchMonitorRight),
                Reanalyze = GetShortcutDraft(ReanalyzeId, current.OverlayHotkeys.Reanalyze),
                ActionLeftClick = GetShortcutDraft(ActionLeftId, current.OverlayHotkeys.ActionLeftClick),
                ActionRightClick = GetShortcutDraft(ActionRightId, current.OverlayHotkeys.ActionRightClick),
                ActionDoubleClick = GetShortcutDraft(ActionDoubleId, current.OverlayHotkeys.ActionDoubleClick),
                ActionFocus = GetShortcutDraft(ActionFocusId, current.OverlayHotkeys.ActionFocus)
            };

            var settings = current with
            {
                Hotkey = GetShortcutDraft(HotkeyId, current.Hotkey),
                OverlayHotkeys = overlayHotkeys,
                DefaultAnalysisTarget = target,
                LabelCharacterSet = string.IsNullOrWhiteSpace(_labelCharsetTextBox.Text)
                    ? "ASDFGHJKLQWERTYUIOPZXCVBNM"
                    : _labelCharsetTextBox.Text,
                LabelScale = Math.Clamp(_labelScaleSlider.Value, 0.5, 3.0),
                LabelAppearance = new LabelAppearanceSettings
                {
                    NormalBackgroundColor = ToColorHex(_labelNormalColorPicker.Color),
                    MatchedBackgroundColor = ToColorHex(_labelMatchedColorPicker.Color),
                    Opacity = Math.Clamp(_labelOpacitySlider.Value, 0.1, 1.0),
                    LabelWidth = current.LabelAppearance.LabelWidth,
                    LabelHeight = current.LabelAppearance.LabelHeight,
                    FontSize = Math.Clamp(_baseLabelFontSize, 8, 48)
                },
                RecentLabelColors = _recentLabelColors.ToArray(),
                ExcludedAxRoles = excludedRoles,
                SuppressInFullscreen = _suppressFullscreenCheckBox.IsChecked ?? false,
                ContinuousMode = _continuousModeCheckBox.IsChecked ?? false,
                DebugLoggingEnabled = _debugLoggingCheckBox.IsChecked ?? false,
                LaunchAtLogin = _launchAtLoginCheckBox.IsChecked ?? false,
                SuppressedProcesses = suppressed
            };

            UpdateShortcutConflictErrors();
            var conflicts = DetectShortcutConflicts(settings);
            if (conflicts.Count > 0)
            {
                _statusTextBlock.Text = "ショートカットの競合を解消してください: " + string.Join(" / ", conflicts);
                return;
            }

            await _orchestrator.UpdateSettingsAsync(settings);

            var messages = new List<string> { "設定を保存しました。" };
            var hotkeyResult = _orchestrator.LastHotkeyRegistrationResult;
            if (!hotkeyResult.Succeeded)
            {
                messages.Add("起動ホットキー登録失敗: " + (hotkeyResult.Reason ?? "unknown"));
            }

            if (!_launchAtLoginService.SetEnabled(settings.LaunchAtLogin, out var launchError))
            {
                messages.Add(string.IsNullOrWhiteSpace(launchError)
                    ? "自動起動設定の更新に失敗しました。"
                    : launchError);
            }

            _statusTextBlock.Text = string.Join(" ", messages);
            RefreshPermissionStatus();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings.", ex);
            _statusTextBlock.Text = $"設定保存に失敗しました: {ex.Message}";
        }
    }

    private void RequestPermissionButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var snapshot = _permissionService.RequestMissingPermissions();
        UpdatePermissionStatus(snapshot);
        var message = $"必須権限 ax={snapshot.AccessibilityGranted}, input={snapshot.InputMonitoringGranted}";
        if (!snapshot.ScreenRecordingGranted)
        {
            message += " / Screen Recording は未許可でも動作可能";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Note))
        {
            message += $" / {snapshot.Note}";
        }

        _statusTextBlock.Text = message;
    }

    private void RefreshPermissionStatus()
    {
        UpdatePermissionStatus(_permissionService.GetCurrentStatus());
    }

    private void UpdatePermissionStatus(PermissionSnapshot snapshot)
    {
        _accessibilityPermissionTextBlock.Text = $"Accessibility: {(snapshot.AccessibilityGranted ? "許可済み" : "未許可")}";
        _inputMonitoringPermissionTextBlock.Text = $"Input Monitoring: {(snapshot.InputMonitoringGranted ? "許可済み" : "未許可")}";
        _screenRecordingPermissionTextBlock.Text = $"Screen Recording (任意): {(snapshot.ScreenRecordingGranted ? "許可済み" : "未許可")}";

        if (!string.IsNullOrWhiteSpace(snapshot.Note))
        {
            _screenRecordingPermissionTextBlock.Text += $" - {snapshot.Note}";
        }
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

    private void SetShortcutDraft(string shortcutId, HotkeyChord chord)
    {
        _shortcutDraft[shortcutId] = chord;
        RefreshShortcutButtonText(shortcutId);
    }

    private void RefreshShortcutButtonText(string shortcutId)
    {
        if (!_shortcutButtons.TryGetValue(shortcutId, out var button))
        {
            return;
        }

        if (_shortcutDraft.TryGetValue(shortcutId, out var chord))
        {
            button.Content = chord.DisplayText;
        }
    }

    private HotkeyChord GetShortcutDraft(string shortcutId, HotkeyChord fallback)
    {
        return _shortcutDraft.TryGetValue(shortcutId, out var value) ? value : fallback;
    }

    private void UpdateShortcutConflictErrors()
    {
        foreach (var shortcutId in ShortcutIds)
        {
            ClearShortcutError(shortcutId);
        }

        var byChord = new Dictionary<(int KeyCode, bool Cmd, bool Ctrl, bool Opt, bool Shift), List<string>>();
        foreach (var (shortcutId, hotkey) in EnumerateDraftHotkeys())
        {
            if (IsReservedShortcut(hotkey))
            {
                SetShortcutError(shortcutId, "固定キー (ESC/Enter/Backspace) は設定できません。");
                continue;
            }

            var key = (hotkey.KeyCode, hotkey.Command, hotkey.Control, hotkey.Option, hotkey.Shift);
            if (!byChord.TryGetValue(key, out var ids))
            {
                ids = [];
                byChord[key] = ids;
            }

            ids.Add(shortcutId);
        }

        foreach (var ids in byChord.Values.Where(static pair => pair.Count > 1))
        {
            foreach (var shortcutId in ids)
            {
                var others = ids
                    .Where(id => id != shortcutId)
                    .Select(ResolveShortcutLabel);
                SetShortcutError(shortcutId, "同一キー割り当て: " + string.Join(" / ", others));
            }
        }
    }

    private IEnumerable<(string ShortcutId, HotkeyChord Hotkey)> EnumerateDraftHotkeys()
    {
        if (_shortcutDraft.TryGetValue(HotkeyId, out var hotkey))
        {
            yield return (HotkeyId, hotkey);
        }

        if (_shortcutDraft.TryGetValue(MonitorLeftId, out var monitorLeft))
        {
            yield return (MonitorLeftId, monitorLeft);
        }

        if (_shortcutDraft.TryGetValue(MonitorRightId, out var monitorRight))
        {
            yield return (MonitorRightId, monitorRight);
        }

        if (_shortcutDraft.TryGetValue(ReanalyzeId, out var reanalyze))
        {
            yield return (ReanalyzeId, reanalyze);
        }

        if (_shortcutDraft.TryGetValue(ActionLeftId, out var actionLeft))
        {
            yield return (ActionLeftId, actionLeft);
        }

        if (_shortcutDraft.TryGetValue(ActionRightId, out var actionRight))
        {
            yield return (ActionRightId, actionRight);
        }

        if (_shortcutDraft.TryGetValue(ActionDoubleId, out var actionDouble))
        {
            yield return (ActionDoubleId, actionDouble);
        }

        if (_shortcutDraft.TryGetValue(ActionFocusId, out var actionFocus))
        {
            yield return (ActionFocusId, actionFocus);
        }
    }

    private void SetShortcutError(string shortcutId, string message)
    {
        if (!_shortcutErrorTextBlocks.TryGetValue(shortcutId, out var textBlock))
        {
            return;
        }

        textBlock.Text = message;
        textBlock.IsVisible = true;
    }

    private void ClearShortcutError(string shortcutId)
    {
        if (!_shortcutErrorTextBlocks.TryGetValue(shortcutId, out var textBlock))
        {
            return;
        }

        textBlock.Text = string.Empty;
        textBlock.IsVisible = false;
    }

    private bool HasShortcutError(string shortcutId)
    {
        if (!_shortcutErrorTextBlocks.TryGetValue(shortcutId, out var textBlock))
        {
            return false;
        }

        return textBlock.IsVisible && !string.IsNullOrWhiteSpace(textBlock.Text);
    }

    private static bool IsReservedShortcut(HotkeyChord hotkey)
    {
        return hotkey.KeyCode is 53 or 51 or 36;
    }

    private string ResolveShortcutLabel(string shortcutId)
    {
        return _shortcutLabels.TryGetValue(shortcutId, out var label) ? label : "ショートカット";
    }

    private static string TrimWithLimit(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private void SetExcludedRoleCheckStates(IReadOnlyList<string> roles)
    {
        var selectedRoles = new HashSet<string>(roles, StringComparer.Ordinal);
        foreach (var role in ExcludableAxRoles)
        {
            if (_excludedRoleCheckBoxes.TryGetValue(role, out var checkBox))
            {
                checkBox.IsChecked = selectedRoles.Contains(role);
            }
        }
    }

    private string[] GetSelectedExcludedRoles()
    {
        return ExcludableAxRoles
            .Where(role => _excludedRoleCheckBoxes.TryGetValue(role, out var checkBox) && (checkBox.IsChecked ?? false))
            .ToArray();
    }

    private static List<string> DetectShortcutConflicts(UserSettings settings)
    {
        var conflicts = new List<string>();
        var byChord = new Dictionary<(int KeyCode, bool Cmd, bool Ctrl, bool Opt, bool Shift), List<string>>();

        AddHotkey("起動ホットキー", settings.Hotkey);
        AddHotkey("モニタ左切替", settings.OverlayHotkeys.SwitchMonitorLeft);
        AddHotkey("モニタ右切替", settings.OverlayHotkeys.SwitchMonitorRight);
        AddHotkey("再解析", settings.OverlayHotkeys.Reanalyze);
        AddHotkey("左クリック", settings.OverlayHotkeys.ActionLeftClick);
        AddHotkey("右クリック", settings.OverlayHotkeys.ActionRightClick);
        AddHotkey("ダブルクリック", settings.OverlayHotkeys.ActionDoubleClick);
        AddHotkey("フォーカス", settings.OverlayHotkeys.ActionFocus);

        foreach (var pair in byChord.Where(static pair => pair.Value.Count > 1))
        {
            conflicts.Add($"同一キー割り当て: {string.Join(" / ", pair.Value)}");
        }

        foreach (var (name, hotkey) in EnumerateHotkeys(settings))
        {
            if (IsReservedShortcut(hotkey))
            {
                conflicts.Add($"{name} は固定キー(ESC/Backspace/Enter)と競合します。");
            }
        }

        return conflicts;

        void AddHotkey(string name, HotkeyChord hotkey)
        {
            var key = (hotkey.KeyCode, hotkey.Command, hotkey.Control, hotkey.Option, hotkey.Shift);
            if (!byChord.TryGetValue(key, out var names))
            {
                names = [];
                byChord[key] = names;
            }

            names.Add(name);
        }
    }

    private static IEnumerable<(string Name, HotkeyChord Chord)> EnumerateHotkeys(UserSettings settings)
    {
        yield return ("起動ホットキー", settings.Hotkey);
        yield return ("モニタ左切替", settings.OverlayHotkeys.SwitchMonitorLeft);
        yield return ("モニタ右切替", settings.OverlayHotkeys.SwitchMonitorRight);
        yield return ("再解析", settings.OverlayHotkeys.Reanalyze);
        yield return ("左クリック", settings.OverlayHotkeys.ActionLeftClick);
        yield return ("右クリック", settings.OverlayHotkeys.ActionRightClick);
        yield return ("ダブルクリック", settings.OverlayHotkeys.ActionDoubleClick);
        yield return ("フォーカス", settings.OverlayHotkeys.ActionFocus);
    }

    private TextBox RequireTextBox(string name) =>
        this.FindControl<TextBox>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private TextBlock RequireTextBlock(string name) =>
        this.FindControl<TextBlock>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private Slider RequireSlider(string name) =>
        this.FindControl<Slider>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private ColorPicker RequireColorPicker(string name) =>
        this.FindControl<ColorPicker>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private WrapPanel RequireWrapPanel(string name) =>
        this.FindControl<WrapPanel>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private Grid RequireGrid(string name) =>
        this.FindControl<Grid>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private Border RequireBorder(string name) =>
        this.FindControl<Border>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private CheckBox RequireCheckBox(string name) =>
        this.FindControl<CheckBox>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private Button RequireButton(string name) =>
        this.FindControl<Button>(name) ?? throw new InvalidOperationException($"{name} not found.");
}
