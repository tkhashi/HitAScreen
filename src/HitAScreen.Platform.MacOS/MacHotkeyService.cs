using System.Runtime.InteropServices;
using System.Threading;
using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Platform.MacOS;

public sealed class MacHotkeyService : IHotkeyService
{
    private readonly object _sync = new();
    private readonly CGEventTapCallback _callback;

    private Thread? _thread;
    private IntPtr _runLoop;
    private IntPtr _tap;
    private IntPtr _source;
    private ManualResetEventSlim? _registrationSignal;
    private string? _registrationFailureReason;
    private bool _disposed;
    private HotkeyChord _hotkey;
    private DateTimeOffset _lastTriggeredAt = DateTimeOffset.MinValue;
    private int _suppressKeyPropagation;

    public MacHotkeyService()
    {
        _callback = OnEvent;
        _hotkey = new HotkeyChord(46, Command: true, Control: false, Option: false, Shift: true, DisplayText: "Cmd+Shift+M");
    }

    public bool IsRegistered { get; private set; }
    public bool SuppressKeyPropagation
    {
        get => Volatile.Read(ref _suppressKeyPropagation) == 1;
        set => Volatile.Write(ref _suppressKeyPropagation, value ? 1 : 0);
    }

    public event Action? HotkeyPressed;
    public event Action<GlobalKeyEvent>? KeyPressed;

    public HotkeyRegistrationResult Register(HotkeyChord chord)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new HotkeyRegistrationResult(false, "macOS only");
        }

        ManualResetEventSlim registrationSignal;
        lock (_sync)
        {
            _hotkey = chord;

            UnregisterInternal();
            _registrationFailureReason = "event tap の初期化に失敗しました。";
            registrationSignal = new ManualResetEventSlim(false);
            _registrationSignal = registrationSignal;

            _thread = new Thread(EventLoopMain)
            {
                IsBackground = true,
                Name = "hitascreen-hotkey-listener"
            };
            _thread.Start();
        }

        if (!registrationSignal.Wait(TimeSpan.FromSeconds(1.5)))
        {
            lock (_sync)
            {
                _registrationFailureReason = "ホットキー登録の初期化がタイムアウトしました。";
            }

            Unregister();
            registrationSignal.Dispose();
            return new HotkeyRegistrationResult(false, "ホットキー登録がタイムアウトしました。");
        }

        lock (_sync)
        {
            _registrationSignal = null;
            if (IsRegistered)
            {
                registrationSignal.Dispose();
                return new HotkeyRegistrationResult(true);
            }

            var reason = _registrationFailureReason ?? "ホットキー登録に失敗しました。";
            registrationSignal.Dispose();
            return new HotkeyRegistrationResult(false, reason);
        }
    }

    public void Unregister()
    {
        lock (_sync)
        {
            UnregisterInternal();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unregister();
    }

    private void EventLoopMain()
    {
        var mask = 1UL << (int)EventType.KeyDown;

        var tap = CGEventTapCreate(
            CGEventTapLocation.Session,
            CGEventTapPlacement.HeadInsert,
            CGEventTapOptions.Default,
            mask,
            _callback,
            IntPtr.Zero);

        if (tap == IntPtr.Zero)
        {
            SignalRegistrationFailed("event tap を作成できませんでした（権限不足または競合の可能性があります）。");
            return;
        }

        var source = CFMachPortCreateRunLoopSource(IntPtr.Zero, tap, 0);
        if (source == IntPtr.Zero)
        {
            CFRelease(tap);
            SignalRegistrationFailed("run loop source の作成に失敗しました。");
            return;
        }

        var runLoop = CFRunLoopGetCurrent();
        if (runLoop == IntPtr.Zero)
        {
            CFRelease(source);
            CFRelease(tap);
            SignalRegistrationFailed("run loop の初期化に失敗しました。");
            return;
        }

        lock (_sync)
        {
            _tap = tap;
            _source = source;
            _runLoop = runLoop;
        }

        CFRunLoopAddSource(runLoop, source, CFRunLoopModeDefault);
        CGEventTapEnable(tap, true);
        IsRegistered = true;
        _registrationSignal?.Set();

        while (!_disposed)
        {
            var result = CFRunLoopRunInMode(CFRunLoopModeDefault, 0.5, false);
            if (result is CFRunLoopRunResult.Stopped or CFRunLoopRunResult.Finished)
            {
                break;
            }
        }

        lock (_sync)
        {
            if (_source != IntPtr.Zero)
            {
                CFRelease(_source);
                _source = IntPtr.Zero;
            }

            if (_tap != IntPtr.Zero)
            {
                CFRelease(_tap);
                _tap = IntPtr.Zero;
            }

            _runLoop = IntPtr.Zero;
            IsRegistered = false;
            _registrationFailureReason = "ホットキー登録が解除されました。";
        }
    }

    private void UnregisterInternal()
    {
        if (_runLoop != IntPtr.Zero)
        {
            CFRunLoopStop(_runLoop);
        }

        if (_thread is not null && _thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromMilliseconds(300));
        }

        _thread = null;
        IsRegistered = false;
        _registrationSignal?.Set();
        _registrationSignal = null;
    }

    private void SignalRegistrationFailed(string reason)
    {
        lock (_sync)
        {
            IsRegistered = false;
            _registrationFailureReason = reason;
            _registrationSignal?.Set();
        }
    }

    private IntPtr OnEvent(IntPtr proxy, uint eventTypeRaw, IntPtr cgEvent, IntPtr userInfo)
    {
        var eventType = (EventType)eventTypeRaw;
        if (eventType is EventType.TapDisabledByTimeout or EventType.TapDisabledByUserInput)
        {
            IntPtr tap;
            lock (_sync)
            {
                tap = _tap;
            }

            if (tap != IntPtr.Zero)
            {
                CGEventTapEnable(tap, true);
            }

            return cgEvent;
        }

        if (eventType != EventType.KeyDown)
        {
            return cgEvent;
        }

        var keyCode = (int)CGEventGetIntegerValueField(cgEvent, CGEventField.KeyboardEventKeycode);
        var flags = CGEventGetFlags(cgEvent);

        var isHotkey = IsMatch(keyCode, flags, _hotkey);
        if (isHotkey)
        {
            if (DateTimeOffset.UtcNow - _lastTriggeredAt > TimeSpan.FromMilliseconds(250))
            {
                _lastTriggeredAt = DateTimeOffset.UtcNow;
                var handler = HotkeyPressed;
                if (handler is not null)
                {
                    _ = ThreadPool.UnsafeQueueUserWorkItem(
                        static state => ((Action)state!).Invoke(),
                        handler,
                        preferLocal: false);
                }
            }

            return IntPtr.Zero;
        }

        var evt = new GlobalKeyEvent(
            keyCode,
            Command: (flags & (ulong)ModifierFlag.Command) != 0,
            Control: (flags & (ulong)ModifierFlag.Control) != 0,
            Option: (flags & (ulong)ModifierFlag.Option) != 0,
            Shift: (flags & (ulong)ModifierFlag.Shift) != 0);
        KeyPressed?.Invoke(evt);

        return SuppressKeyPropagation ? IntPtr.Zero : cgEvent;
    }

    private static bool IsMatch(int keyCode, ulong flags, HotkeyChord chord)
    {
        if (keyCode != chord.KeyCode)
        {
            return false;
        }

        return HasFlag(flags, ModifierFlag.Command, chord.Command)
            && HasFlag(flags, ModifierFlag.Control, chord.Control)
            && HasFlag(flags, ModifierFlag.Option, chord.Option)
            && HasFlag(flags, ModifierFlag.Shift, chord.Shift);
    }

    private static bool HasFlag(ulong flags, ModifierFlag flag, bool required)
    {
        var isSet = (flags & (ulong)flag) == (ulong)flag;
        return required ? isSet : !isSet;
    }

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int Utf8Encoding = 0x08000100;
    private static readonly IntPtr CFRunLoopModeDefault = CFStringCreateWithCString(IntPtr.Zero, "kCFRunLoopDefaultMode", Utf8Encoding);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventTapCreate(
        CGEventTapLocation tap,
        CGEventTapPlacement place,
        CGEventTapOptions options,
        ulong eventsOfInterest,
        CGEventTapCallback callback,
        IntPtr userInfo);

    [DllImport(CoreGraphics)]
    private static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport(CoreGraphics)]
    private static extern long CGEventGetIntegerValueField(IntPtr cgEvent, CGEventField field);

    [DllImport(CoreGraphics)]
    private static extern ulong CGEventGetFlags(IntPtr cgEvent);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, int order);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopAddSource(IntPtr runLoop, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    private static extern CFRunLoopRunResult CFRunLoopRunInMode(IntPtr mode, double seconds, bool returnAfterSourceHandled);

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopStop(IntPtr runLoop);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr obj);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string str, int encoding);

    private delegate IntPtr CGEventTapCallback(IntPtr proxy, uint eventType, IntPtr cgEvent, IntPtr userInfo);

    private enum CGEventTapLocation : uint
    {
        Session = 1
    }

    private enum CGEventTapPlacement : uint
    {
        HeadInsert = 0
    }

    private enum CGEventTapOptions : uint
    {
        Default = 0,
        ListenOnly = 1
    }

    private enum EventType : uint
    {
        KeyDown = 10,
        TapDisabledByTimeout = 0xFFFFFFFE,
        TapDisabledByUserInput = 0xFFFFFFFF
    }

    private enum CGEventField
    {
        KeyboardEventKeycode = 9
    }

    [Flags]
    private enum ModifierFlag : ulong
    {
        Shift = 1UL << 17,
        Control = 1UL << 18,
        Option = 1UL << 19,
        Command = 1UL << 20
    }

    private enum CFRunLoopRunResult
    {
        Finished = 1,
        Stopped = 2,
        TimedOut = 3,
        HandledSource = 4
    }
}
