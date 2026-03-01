using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

public sealed class OverlayWindow : Window
{
    private readonly Grid _root;
    private readonly Canvas _hintCanvas;
    private readonly TextBlock _statusText;

    public OverlayWindow()
    {
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        ShowActivated = false;
        Focusable = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        IsHitTestVisible = false;

        _root = new Grid();
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
            IsHitTestVisible = false
        };

        _root.IsHitTestVisible = false;

        _root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(50, 15, 20, 35))
        });
        _root.Children.Add(_hintCanvas);
        _root.Children.Add(_statusText);

        Content = _root;

        KeyDown += OnKeyDown;
        Opened += (_, _) => MacAppInterop.MakeOverlayClickThrough(this);
    }

    public event Action<char>? CharacterTyped;
    public event Action? BackspacePressed;
    public event Action? EnterPressed;
    public event Action? EscapePressed;
    public event Action<MonitorSwitchDirection>? MonitorSwitchRequested;
    public event Action<UiActionType>? ActionSelected;
    public event Action? ReanalyzeRequested;

    public void Render(OverlayViewState state, double labelScale)
    {
        var x = (int)Math.Round(state.TargetBounds.X);
        var y = (int)Math.Round(state.TargetBounds.Y);
        var width = Math.Max(1, (int)Math.Round(state.TargetBounds.Width));
        var height = Math.Max(1, (int)Math.Round(state.TargetBounds.Height));

        Position = new PixelPoint(x, y);
        Width = width;
        Height = height;

        _hintCanvas.Children.Clear();

        foreach (var hint in state.Hints)
        {
            var left = hint.Bounds.X - state.TargetBounds.X;
            var top = hint.Bounds.Y - state.TargetBounds.Y;

            var border = new Border
            {
                Background = hint.MatchesInput
                    ? new SolidColorBrush(Color.FromArgb(235, 255, 214, 92))
                    : new SolidColorBrush(Color.FromArgb(160, 90, 90, 90)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 12, 20, 26)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Child = new TextBlock
                {
                    Text = hint.Label,
                    FontWeight = FontWeight.Bold,
                    FontSize = 14 * labelScale,
                    Foreground = Brushes.Black
                }
            };

            Canvas.SetLeft(border, Math.Max(0, left));
            Canvas.SetTop(border, Math.Max(0, top));
            _hintCanvas.Children.Add(border);
        }

        _statusText.Text =
            $"Input: {state.Input}  Action: {state.PendingAction}  Target: {state.Target}  Hints: {state.Hints.Count}\n" +
            "Keys: ESC cancel, Enter confirm, Backspace delete, Left/Right switch monitor, Tab reanalyze, F1/F2/F3/F4 action";
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
}
