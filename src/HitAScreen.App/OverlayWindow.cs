using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public sealed class OverlayWindow : Window
{
    private const double FrameSideMargin = 6;
    private const double FrameBottomMargin = 6;
    private const double FrameTopMargin = 6;
    private const double PrimaryDisplayTopMargin = 42;
    private const double IdleFrameThickness = 2;
    private const double PreparingFrameThickness = 4;
    private const double StaticFramePhase = 0.34;
    private const double FrameAnimationStep = 0.09;
    private const double TwoPi = Math.PI * 2;

    private readonly Grid _root;
    private readonly Border _frameBorder;
    private readonly LinearGradientBrush _frameBrush;
    private readonly DispatcherTimer _frameAnimationTimer;
    private readonly Canvas _hintCanvas;
    private readonly TextBlock _statusText;
    private OverlayViewState? _lastState;
    private LabelAppearanceSettings? _lastAppearance;
    private double _lastLabelScale = 1.0;
    private double _frameAnimationPhase = StaticFramePhase;
    private bool _isPreparingVisualActive;

    public OverlayWindow()
    {
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        ShowActivated = false;
        Focusable = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        IsHitTestVisible = true;

        _root = new Grid
        {
            IsHitTestVisible = true
        };

        _frameBrush = CreateFrameBrush();
        _frameBorder = new Border
        {
            BorderBrush = _frameBrush,
            BorderThickness = new Thickness(IdleFrameThickness),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(FrameSideMargin, FrameTopMargin, FrameSideMargin, FrameBottomMargin),
            BoxShadow = CreateIdleFrameShadow(),
            IsHitTestVisible = false
        };

        _frameAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _frameAnimationTimer.Tick += OnFrameAnimationTick;

        _hintCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };

        _statusText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14,
            Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };

        _root.Children.Add(_frameBorder);
        _root.Children.Add(_hintCanvas);
        _root.Children.Add(_statusText);

        Content = _root;

        KeyDown += OnKeyDown;
        PointerPressed += OnPointerPressed;
        Opened += (_, _) =>
        {
            MacAppInterop.ConfigureOverlayMouseEvents(this, ignoreMouseEvents: false);
            ApplyFrameGradient(StaticFramePhase);
            Dispatcher.UIThread.Post(ReapplyLastRender, DispatcherPriority.Loaded);
        };
        Closed += (_, _) => _frameAnimationTimer.Stop();
    }

    public event Action<char>? CharacterTyped;
    public event Action? BackspacePressed;
    public event Action? EnterPressed;
    public event Action? EscapePressed;
    public event Action<MonitorSwitchDirection>? MonitorSwitchRequested;
    public event Action<UiActionType>? ActionSelected;
    public event Action? ReanalyzeRequested;
    public event Action? PointerCancelRequested;

    public void StopVisualEffects()
    {
        _frameAnimationTimer.Stop();
        _frameAnimationPhase = StaticFramePhase;
        ApplyFrameGradient(_frameAnimationPhase);
    }

    public void Render(OverlayViewState state, LabelAppearanceSettings appearance, double labelScale)
    {
        _lastState = state;
        _lastAppearance = appearance;
        _lastLabelScale = labelScale;

        var desktopScale = ResolveDesktopScale(state);
        var x = (int)Math.Round(state.OverlayBounds.X);
        var y = (int)Math.Round(state.OverlayBounds.Y);
        var width = Math.Max(1, (int)Math.Round(state.OverlayBounds.Width));
        var height = Math.Max(1, (int)Math.Round(state.OverlayBounds.Height));

        Width = width / desktopScale;
        Height = height / desktopScale;
        Position = new PixelPoint(x, y);
        ApplyFrameMarginForDisplay(state.TargetDisplay.IsPrimary);

        _hintCanvas.Children.Clear();
        ApplyFrameVisualStyle(state.IsPreparing);
        UpdateFrameAnimation(state.IsPreparing);

        if (state.IsPreparing)
        {
            _statusText.Text = "解析準備中...  ESC またはクリックでキャンセル";
            return;
        }

        var normalizedOpacity = Math.Clamp(appearance.Opacity, 0.1, 1.0);
        var normalBackground = WithOpacity(ParseColor(appearance.NormalBackgroundColor, Color.FromRgb(90, 90, 90)), normalizedOpacity);
        var matchedBackground = WithOpacity(ParseColor(appearance.MatchedBackgroundColor, Color.FromRgb(255, 214, 92)), normalizedOpacity);
        var normalForeground = ResolveReadableBrush(normalBackground);
        var matchedForeground = ResolveReadableBrush(matchedBackground);
        var metrics = LabelAppearanceMetrics.Calculate(appearance, labelScale);

        foreach (var hint in state.Hints)
        {
            var left = (hint.Bounds.X - state.OverlayBounds.X) / desktopScale;
            var top = (hint.Bounds.Y - state.OverlayBounds.Y) / desktopScale;
            var textBlock = new TextBlock
            {
                Text = hint.Label,
                FontWeight = FontWeight.Bold,
                FontSize = metrics.FontSize,
                Foreground = hint.MatchesInput ? matchedForeground : normalForeground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var border = new Border
            {
                Background = hint.MatchesInput
                    ? new SolidColorBrush(matchedBackground)
                    : new SolidColorBrush(normalBackground),
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 12, 20, 26)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                MinWidth = metrics.MinWidth,
                MinHeight = metrics.MinHeight,
                Padding = new Thickness(metrics.HorizontalPadding, metrics.VerticalPadding),
                Child = textBlock
            };

            Canvas.SetLeft(border, Math.Max(0, left));
            Canvas.SetTop(border, Math.Max(0, top));
            _hintCanvas.Children.Add(border);
        }

        _statusText.Text =
            $"Input: {state.Input}  Action: {state.PendingAction}  Target: {state.Target}  Hints: {state.Hints.Count}\n" +
            "Keys: ESC cancel, Enter confirm, Backspace delete, その他の操作キーは設定値を参照";
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerCancelRequested?.Invoke();
        e.Handled = true;
    }

    private void OnFrameAnimationTick(object? sender, EventArgs e)
    {
        _frameAnimationPhase = (_frameAnimationPhase + FrameAnimationStep) % TwoPi;
        ApplyFrameGradient(_frameAnimationPhase);
    }

    private void UpdateFrameAnimation(bool isPreparing)
    {
        if (isPreparing)
        {
            if (!_frameAnimationTimer.IsEnabled)
            {
                _frameAnimationTimer.Start();
            }

            return;
        }

        if (_frameAnimationTimer.IsEnabled)
        {
            _frameAnimationTimer.Stop();
        }

        _frameAnimationPhase = StaticFramePhase;
        ApplyFrameGradient(_frameAnimationPhase);
    }

    private void ApplyFrameGradient(double phase)
    {
        var startX = 0.5 + (0.45 * Math.Cos(phase));
        var startY = 0.5 + (0.45 * Math.Sin(phase));
        var endX = 1.0 - startX;
        var endY = 1.0 - startY;

        _frameBrush.StartPoint = new RelativePoint(startX, startY, RelativeUnit.Relative);
        _frameBrush.EndPoint = new RelativePoint(endX, endY, RelativeUnit.Relative);
    }

    private void ApplyFrameVisualStyle(bool isPreparing)
    {
        if (_isPreparingVisualActive == isPreparing)
        {
            return;
        }

        _isPreparingVisualActive = isPreparing;
        if (isPreparing)
        {
            _frameBorder.BorderThickness = new Thickness(PreparingFrameThickness);
            _frameBorder.BoxShadow = CreatePreparingFrameShadow();
            return;
        }

        _frameBorder.BorderThickness = new Thickness(IdleFrameThickness);
        _frameBorder.BoxShadow = CreateIdleFrameShadow();
    }

    private void ApplyFrameMarginForDisplay(bool isPrimaryDisplay)
    {
        var topMargin = isPrimaryDisplay ? PrimaryDisplayTopMargin : FrameTopMargin;
        _frameBorder.Margin = new Thickness(FrameSideMargin, topMargin, FrameSideMargin, FrameBottomMargin);
    }

    private void ReapplyLastRender()
    {
        if (_lastState is null || _lastAppearance is null)
        {
            return;
        }

        Render(_lastState, _lastAppearance, _lastLabelScale);
    }

    private double ResolveDesktopScale(OverlayViewState state)
    {
        if (DesktopScaling > 0)
        {
            return Math.Max(1.0, DesktopScaling);
        }

        return Math.Max(1.0, state.TargetDisplay.DpiScale);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                EscapePressed?.Invoke();
                e.Handled = true;
                return;
            case Key.Back:
                BackspacePressed?.Invoke();
                e.Handled = true;
                return;
            case Key.Enter:
                EnterPressed?.Invoke();
                e.Handled = true;
                return;
            case Key.Left:
                MonitorSwitchRequested?.Invoke(MonitorSwitchDirection.Left);
                e.Handled = true;
                return;
            case Key.Right:
                MonitorSwitchRequested?.Invoke(MonitorSwitchDirection.Right);
                e.Handled = true;
                return;
            case Key.Tab:
                ReanalyzeRequested?.Invoke();
                e.Handled = true;
                return;
            case Key.F1:
                ActionSelected?.Invoke(UiActionType.LeftClick);
                e.Handled = true;
                return;
            case Key.F2:
                ActionSelected?.Invoke(UiActionType.RightClick);
                e.Handled = true;
                return;
            case Key.F3:
                ActionSelected?.Invoke(UiActionType.DoubleClick);
                e.Handled = true;
                return;
            case Key.F4:
                ActionSelected?.Invoke(UiActionType.Focus);
                e.Handled = true;
                return;
        }

        if (TryMapKeyToCharacter(e.Key, out var ch))
        {
            CharacterTyped?.Invoke(ch);
            e.Handled = true;
        }
    }

    private static bool TryMapKeyToCharacter(Key key, out char character)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            character = (char)('A' + (key - Key.A));
            return true;
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            character = (char)('0' + (key - Key.D0));
            return true;
        }

        character = default;
        return false;
    }

    private static Color ParseColor(string raw, Color fallback)
    {
        return Color.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static Color WithOpacity(Color color, double opacity)
    {
        var alpha = (byte)Math.Clamp((int)Math.Round(color.A * opacity), 0, 255);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static IBrush ResolveReadableBrush(Color color)
    {
        var luminance = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;
        return luminance >= 0.58 ? Brushes.Black : Brushes.White;
    }

    private static LinearGradientBrush CreateFrameBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1.0, 1.0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(175, 241, 161, 167), 0.00),
                new GradientStop(Color.FromArgb(175, 240, 199, 152), 0.18),
                new GradientStop(Color.FromArgb(175, 187, 220, 156), 0.36),
                new GradientStop(Color.FromArgb(175, 150, 220, 210), 0.54),
                new GradientStop(Color.FromArgb(175, 160, 186, 237), 0.72),
                new GradientStop(Color.FromArgb(175, 214, 171, 234), 0.88),
                new GradientStop(Color.FromArgb(175, 241, 161, 167), 1.00)
            }
        };
    }

    private static BoxShadows CreatePreparingFrameShadow()
    {
        return new BoxShadows(
            new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 0,
                Blur = 16,
                Spread = 1,
                Color = Color.FromArgb(120, 196, 210, 255)
            },
            new[]
            {
                new BoxShadow
                {
                    OffsetX = 0,
                    OffsetY = 0,
                    Blur = 32,
                    Spread = 4,
                    Color = Color.FromArgb(80, 236, 191, 222)
                }
            });
    }

    private static BoxShadows CreateIdleFrameShadow()
    {
        return new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 0,
            Blur = 8,
            Spread = 0,
            Color = Color.FromArgb(45, 140, 150, 186)
        });
    }
}
