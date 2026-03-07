using Avalonia.Input;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.App;

internal static class KeyBindingCatalog
{
    private static readonly Dictionary<int, char> InputCharactersByKeyCode = new()
    {
        [0] = 'A', [1] = 'S', [2] = 'D', [3] = 'F', [4] = 'H', [5] = 'G', [6] = 'Z', [7] = 'X',
        [8] = 'C', [9] = 'V', [11] = 'B', [12] = 'Q', [13] = 'W', [14] = 'E', [15] = 'R',
        [16] = 'Y', [17] = 'T', [31] = 'O', [32] = 'U', [34] = 'I', [35] = 'P', [37] = 'L',
        [38] = 'J', [40] = 'K', [45] = 'N', [46] = 'M',
        [18] = '1', [19] = '2', [20] = '3', [21] = '4', [22] = '6', [23] = '5',
        [25] = '9', [26] = '7', [28] = '8', [29] = '0'
    };

    private static readonly Dictionary<Key, int> AvaloniaKeyToMacKeyCode = new()
    {
        [Key.A] = 0,
        [Key.S] = 1,
        [Key.D] = 2,
        [Key.F] = 3,
        [Key.H] = 4,
        [Key.G] = 5,
        [Key.Z] = 6,
        [Key.X] = 7,
        [Key.C] = 8,
        [Key.V] = 9,
        [Key.B] = 11,
        [Key.Q] = 12,
        [Key.W] = 13,
        [Key.E] = 14,
        [Key.R] = 15,
        [Key.Y] = 16,
        [Key.T] = 17,
        [Key.D1] = 18,
        [Key.D2] = 19,
        [Key.D3] = 20,
        [Key.D4] = 21,
        [Key.D6] = 22,
        [Key.D5] = 23,
        [Key.D9] = 25,
        [Key.D7] = 26,
        [Key.D8] = 28,
        [Key.D0] = 29,
        [Key.O] = 31,
        [Key.U] = 32,
        [Key.I] = 34,
        [Key.P] = 35,
        [Key.L] = 37,
        [Key.J] = 38,
        [Key.K] = 40,
        [Key.N] = 45,
        [Key.M] = 46,
        [Key.Tab] = 48,
        [Key.Back] = 51,
        [Key.Escape] = 53,
        [Key.Enter] = 36,
        [Key.Left] = 123,
        [Key.Right] = 124,
        [Key.F1] = 122,
        [Key.F2] = 120,
        [Key.F3] = 99,
        [Key.F4] = 118,
        [Key.F5] = 96,
        [Key.F6] = 97,
        [Key.F7] = 98,
        [Key.F8] = 100,
        [Key.F9] = 101,
        [Key.F10] = 109,
        [Key.F11] = 103,
        [Key.F12] = 111
    };

    private static readonly Dictionary<int, string> DisplayNamesByKeyCode = new()
    {
        [48] = "Tab",
        [51] = "Backspace",
        [53] = "ESC",
        [36] = "Enter",
        [123] = "Left",
        [124] = "Right",
        [122] = "F1",
        [120] = "F2",
        [99] = "F3",
        [118] = "F4",
        [96] = "F5",
        [97] = "F6",
        [98] = "F7",
        [100] = "F8",
        [101] = "F9",
        [109] = "F10",
        [103] = "F11",
        [111] = "F12"
    };

    public static bool TryMapInputCharacter(int keyCode, out char character)
    {
        return InputCharactersByKeyCode.TryGetValue(keyCode, out character);
    }

    public static bool TryCreateChordFromAvalonia(KeyEventArgs e, out HotkeyChord chord)
    {
        if (!TryGetKeyCode(e.Key, out var keyCode))
        {
            chord = default;
            return false;
        }

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var option = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        chord = new HotkeyChord(
            keyCode,
            Command: command,
            Control: control,
            Option: option,
            Shift: shift,
            DisplayText: BuildDisplayText(keyCode, command, control, option, shift));

        return true;
    }

    public static bool IsChordMatch(GlobalKeyEvent key, HotkeyChord chord)
    {
        return key.KeyCode == chord.KeyCode
            && key.Command == chord.Command
            && key.Control == chord.Control
            && key.Option == chord.Option
            && key.Shift == chord.Shift;
    }

    public static string BuildDisplayText(int keyCode, bool command, bool control, bool option, bool shift)
    {
        var parts = new List<string>(5);
        if (command)
        {
            parts.Add("Cmd");
        }

        if (control)
        {
            parts.Add("Ctrl");
        }

        if (option)
        {
            parts.Add("Opt");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        parts.Add(ResolveKeyDisplayName(keyCode));
        return string.Join("+", parts);
    }

    private static bool TryGetKeyCode(Key key, out int keyCode)
    {
        if (key is Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LWin or Key.RWin)
        {
            keyCode = default;
            return false;
        }

        return AvaloniaKeyToMacKeyCode.TryGetValue(key, out keyCode);
    }

    private static string ResolveKeyDisplayName(int keyCode)
    {
        if (DisplayNamesByKeyCode.TryGetValue(keyCode, out var displayName))
        {
            return displayName;
        }

        if (InputCharactersByKeyCode.TryGetValue(keyCode, out var ch))
        {
            return ch.ToString();
        }

        return $"Key{keyCode}";
    }
}
