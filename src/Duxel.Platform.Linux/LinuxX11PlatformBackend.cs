using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Linux;

public sealed record LinuxX11PlatformOptions(int Width = 1280, int Height = 720, string Title = "Duxel", bool Visible = true)
{
    public bool CenterOnScreen
    {
        get; init;
    }
    public Action? FrameInvalidated
    {
        get; init;
    }
    public LinuxAtSpiOptions? Accessibility
    {
        get; init;
    }
}

/// <summary>Owns an X11 connection, window, committed UTF-8 input and a Vulkan surface source.</summary>
public sealed unsafe partial class LinuxX11PlatformBackend : IPlatformBackend, IPlatformEventWakeBackend, IPlatformPresentationInvalidationBackend
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly object _wakeLock = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly LinuxX11PlatformOptions _options;
    private readonly InputState _input = new();
    private readonly nuint[] _cursors = new nuint[9];
    private nint _display, _inputMethod, _inputContext;
    private nuint _window, _protocols, _deleteWindow;
    private int _wakeDescriptor = -1;
    private int _connection;
    private bool _disposed, _shouldClose, _hidden;
    private long _hideSequence;
    private long _presentationInvalidationSequence;
    private UiMouseCursor? _cursor;
    private PlatformSize _size;
    private readonly SurfaceSource _surface;
    private readonly LinuxPlatformTextBackend _text = new();

    static LinuxX11PlatformBackend()
    {
        if (!OperatingSystem.IsLinux() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The X11 backend requires 64-bit Linux.");
        if (X11Native.XInitThreads() == 0)
            throw new InvalidOperationException("Xlib thread initialization failed.");
        if (X11Native.setlocale(0, "") == 0 || X11Native.XSetLocaleModifiers("") == 0)
            throw new InvalidOperationException("X11 could not initialize the environment's input locale.");
    }

    public LinuxX11PlatformBackend(LinuxX11PlatformOptions? options = null)
    {
        _options = options ?? new();
        _options.Accessibility?.Validate();
        if (_options.Width <= 0 || _options.Height <= 0 || string.IsNullOrWhiteSpace(_options.Title))
            throw new ArgumentException("X11 window dimensions and title must be valid.", nameof(options));
        _size = new(_options.Width, _options.Height);
        _hidden = !_options.Visible;
        _surface = new(this);
        try
        {
            _display = X11Native.XOpenDisplay(0);
            if (_display == 0)
                throw new InvalidOperationException("XOpenDisplay failed. Configure an available X11 DISPLAY.");
            _connection = X11Native.XConnectionNumber(_display);
            _wakeDescriptor = X11Native.eventfd(0, 0x800 | 0x80000);
            if (_wakeDescriptor < 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "eventfd failed.");
            if (X11Native.XkbSetDetectableAutoRepeat(_display, 1, out var detectable) == 0 || detectable == 0
                || X11Native.XkbGetAutoRepeatRate(_display, 0x100, out var delay, out var interval) == 0 || interval == 0)
                throw new NotSupportedException("X11 requires detectable XKB repeat and a valid keyboard repeat rate.");
            _input.Repeat = new(delay / 1000d, interval / 1000d);
            var screen = X11Native.XDefaultScreen(_display);
            InitializeDesktopSettings(screen);
            _size = new(checked((int)MathF.Round(_options.Width * ContentScale)), checked((int)MathF.Round(_options.Height * ContentScale)));
            var x = _options.CenterOnScreen ? (X11Native.XDisplayWidth(_display, screen) - _size.Width) / 2 : 0;
            var y = _options.CenterOnScreen ? (X11Native.XDisplayHeight(_display, screen) - _size.Height) / 2 : 0;
            _window = X11Native.XCreateSimpleWindow(_display, X11Native.XDefaultRootWindow(_display), x, y,
                (uint)_size.Width, (uint)_size.Height, 0, 0, 0);
            if (_window == 0)
                throw new InvalidOperationException("XCreateSimpleWindow failed.");
            X11Native.XSelectInput(_display, _window, 1 | 2 | 4 | 8 | 16 | 32 | 64 | 32768 | 131072 | 2097152);
            X11Native.XStoreName(_display, _window, _options.Title);
            var title = Utf8.GetBytes(_options.Title);
            fixed (byte* bytes = title)
                X11Native.XChangeProperty(_display, _window, X11Native.XInternAtom(_display, "_NET_WM_NAME", 0),
                    X11Native.XInternAtom(_display, "UTF8_STRING", 0), 8, 0, bytes, title.Length);
            _protocols = X11Native.XInternAtom(_display, "WM_PROTOCOLS", 0);
            _deleteWindow = X11Native.XInternAtom(_display, "WM_DELETE_WINDOW", 0);
            var delete = _deleteWindow;
            if (X11Native.XSetWMProtocols(_display, _window, &delete, 1) == 0)
                throw new InvalidOperationException("XSetWMProtocols failed.");
            _inputMethod = X11Native.XOpenIM(_display, 0, 0, 0);
            if (_inputMethod == 0)
                throw new InvalidOperationException("XOpenIM failed for the configured input locale.");
            fixed (byte* style = "inputStyle\0"u8, client = "clientWindow\0"u8, focus = "focusWindow\0"u8)
                _inputContext = X11Native.XCreateIC(_inputMethod, style, 0x408, client, _window, focus, _window, 0);
            if (_inputContext == 0)
                throw new InvalidOperationException("XCreateIC failed for committed-text input.");
            UpdateAccessibilityGeometry();
            if (_options.Accessibility is { Enabled: true } accessibility)
            {
                var displayName = Marshal.PtrToStringUTF8(X11Native.XDisplayString(_display))
                    ?? throw new InvalidOperationException("The X11 display name is unavailable.");
                var window = checked((uint)_window);
                bool FocusWindow()
                {
                    using var connection = new XcbConnection(displayName);
                    return connection.FocusWindow(window);
                }
                _atSpi = new(_accessibilityTree, _options.Title, accessibility, () => Volatile.Read(ref _accessibilityGeometry), WakeEvents, FocusWindow);
            }
            if (_options.Visible)
                X11Native.XMapWindow(_display, _window);
            X11Native.XFlush(_display);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public nuint WindowHandle => _window;
    public long PresentationInvalidationSequence => Volatile.Read(ref _presentationInvalidationSequence);
    public PlatformSize WindowSize => _size;
    public PlatformSize FramebufferSize => _size;
    public bool IsInteractingResize => false;
    public bool ShouldClose => _shouldClose;
    public double TimeSeconds => _clock.Elapsed.TotalSeconds;
    public IInputBackend Input => _input;
    public IVulkanSurfaceSource VulkanSurface => _surface;
    public IPlatformTextBackend? TextBackend => _text;
    public UiHostVisibility HostVisibility => new(_hidden, _hideSequence);

    private void CheckOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("X11 window operations require the creating thread.");
        _atSpi?.ThrowIfFaulted();
    }

    public void PollEvents()
    {
        CheckOwner();
        _input.BeginFrame();
        var changed = false;
        while (X11Native.XPending(_display) > 0)
        {
            X11Native.XNextEvent(_display, out var value);
            if (X11Native.XFilterEvent(ref value, _window) != 0)
                continue;
            var settingsEvent = IsDesktopSettingsEvent(in value);
            if (value.Type is 17 or 28 or 33 && value.Input.Window != _window && !settingsEvent)
                continue;
            changed = true;
            if (settingsEvent)
            {
                RefreshDesktopSettings();
                UpdateAccessibilityGeometry();
                Interlocked.Increment(ref _presentationInvalidationSequence);
                continue;
            }
            var inputStart = _input.BeginEvent();
            switch (value.Type)
            {
                case 12 when value.Input.Window == _window:
                    Interlocked.Increment(ref _presentationInvalidationSequence);
                    break;
                case 2:
                case 3:
                    ProcessKey(ref value.Input, value.Type == 2);
                    break;
                case 4:
                case 5:
                    _input.Position = new(value.Input.X, value.Input.Y);
                    _input.Modifiers = InputState.ModifiersFrom(value.Input.State);
                    _input.Button(value.Input.Detail, value.Type == 4);
                    break;
                case 6:
                    _input.Position = new(value.Input.X, value.Input.Y);
                    _input.Modifiers = InputState.ModifiersFrom(value.Input.State);
                    break;
                case 7:
                    _input.Position = new(value.Input.X, value.Input.Y);
                    _input.Modifiers = InputState.ModifiersFrom(value.CrossingState);
                    break;
                case 8:
                    _input.Position = _input.HasPressedButton ? new(value.Input.X, value.Input.Y) : new(-1, -1);
                    _input.Modifiers = InputState.ModifiersFrom(value.CrossingState);
                    break;
                case 9:
                    _input.IsWindowInactive = false;
                    _accessibilityFocused = true;
                    UpdateAccessibilityGeometry();
                    X11Native.XSetICFocus(_inputContext);
                    break;
                case 10:
                    _input.IsWindowInactive = true;
                    _accessibilityFocused = false;
                    UpdateAccessibilityGeometry();
                    X11Native.XUnsetICFocus(_inputContext);
                    _input.LoseFocus();
                    break;
                case 17 when value.Configure.Window == _window:
                    _window = 0;
                    _shouldClose = true;
                    _accessibilityMapped = false;
                    UpdateAccessibilityGeometry();
                    break;
                case 18 when value.Configure.Window == _window:
                    Interlocked.Increment(ref _presentationInvalidationSequence);
                    _accessibilityMapped = false;
                    if (!_hidden)
                        _hideSequence++;
                    _hidden = true;
                    UpdateAccessibilityGeometry();
                    break;
                case 19 when value.Configure.Window == _window:
                    Interlocked.Increment(ref _presentationInvalidationSequence);
                    _accessibilityMapped = true;
                    _hidden = false;
                    UpdateAccessibilityGeometry();
                    break;
                case 22 when value.Configure.Window == _window:
                    Interlocked.Increment(ref _presentationInvalidationSequence);
                    _size = new(value.Configure.Width, value.Configure.Height);
                    UpdateAccessibilityGeometry();
                    break;
                case 33 when value.Client.Window == _window && value.Client.MessageType == _protocols && value.Client.Format == 32
                    && (nuint)value.Client.Data0 == _deleteWindow:
                    _shouldClose = true;
                    break;
            }
            _input.EndEvent(inputStart, ContentScale);
        }
        if (changed)
            _options.FrameInvalidated?.Invoke();
    }

    private void ProcessKey(ref X11Native.InputEvent value, bool down)
    {
        var symbol = X11Native.XLookupKeysym(ref value, 0);
        _input.Key(symbol, down, value.State);
        if (!down || (_input.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
            return;
        byte* buffer = stackalloc byte[256];
        var count = X11Native.Xutf8LookupString(_inputContext, ref value, buffer, 256, out _, out var status);
        if (status == -1)
        {
            var expanded = new byte[count];
            fixed (byte* bytes = expanded)
            {
                count = X11Native.Xutf8LookupString(_inputContext, ref value, bytes, expanded.Length, out _, out status);
                if (status == -1)
                    throw new InvalidOperationException("XIM text size changed during lookup.");
                AddText(new(bytes, count), status);
            }
        }
        else
            AddText(new(buffer, count), status);
    }

    private void AddText(ReadOnlySpan<byte> bytes, int status)
    {
        if (status is not (2 or 4))
            return;
        foreach (var rune in Utf8.GetString(bytes).EnumerateRunes())
            if (rune.Value >= 32 && rune.Value != 127)
                _input.Characters.Add(new((uint)rune.Value));
    }

    public void WakeEvents()
    {
        lock (_wakeLock)
        {
            if (_wakeDescriptor < 0)
                return;
            ulong signal = 1;
            if (X11Native.write(_wakeDescriptor, &signal, 8) != 8 && Marshal.GetLastPInvokeError() != 11)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "eventfd signal failed.");
        }
    }

    public void WaitEvents(int timeoutMilliseconds)
    {
        CheckOwner();
        if (timeoutMilliseconds < -1)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        WaitForNativeEvents(timeoutMilliseconds > 0 ? timeoutMilliseconds : -1);
        PollEvents();
    }

    private void WaitForNativeEvents(int timeoutMilliseconds)
    {
        if (X11Native.XPending(_display) > 0)
            return;
        var descriptors = stackalloc X11Native.PollDescriptor[2];
        descriptors[0] = new()
        {
            FileDescriptor = _connection,
            Events = 1
        };
        descriptors[1] = new()
        {
            FileDescriptor = _wakeDescriptor,
            Events = 1
        };
        var status = X11Native.poll(descriptors, 2, timeoutMilliseconds);
        if (status < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == 4)
                return;
            throw new Win32Exception(error, "X11 event wait failed.");
        }
        if (((descriptors[0].ReturnedEvents | descriptors[1].ReturnedEvents) & (8 | 16 | 32)) != 0)
            throw new IOException("X11 event descriptors closed or failed.");
        if ((descriptors[1].ReturnedEvents & 1) != 0)
        {
            ulong signal;
            if (X11Native.read(_wakeDescriptor, &signal, 8) != 8)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "eventfd drain failed.");
        }
    }

    public void SetMouseCursor(UiMouseCursor cursor)
    {
        CheckOwner();
        if ((uint)cursor >= _cursors.Length)
            throw new ArgumentOutOfRangeException(nameof(cursor));
        if (_cursor == cursor)
            return;
        var index = (int)cursor;
        if (_cursors[index] == 0)
        {
            uint shape = cursor switch
            {
                UiMouseCursor.Arrow => 68,
                UiMouseCursor.TextInput => 152,
                UiMouseCursor.ResizeAll => 52,
                UiMouseCursor.ResizeNS => 116,
                UiMouseCursor.ResizeEW => 108,
                UiMouseCursor.ResizeNESW => 12,
                UiMouseCursor.ResizeNWSE => 14,
                UiMouseCursor.Hand => 60,
                UiMouseCursor.NotAllowed => 88,
                _ => throw new ArgumentOutOfRangeException(nameof(cursor))
            };
            _cursors[index] = X11Native.XCreateFontCursor(_display, shape);
            if (_cursors[index] == 0)
                throw new InvalidOperationException("X11 cursor creation failed.");
        }
        X11Native.XDefineCursor(_display, _window, _cursors[index]);
        _cursor = cursor;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("X11 disposal requires the creating thread.");
        _disposed = true;
        _atSpi?.Dispose();
        _accessibilityTree.Dispose();
        _text.Dispose();
        _surface.Dispose();
        lock (_wakeLock)
        {
            var descriptor = _wakeDescriptor;
            _wakeDescriptor = -1;
            if (descriptor >= 0)
                X11Native.close(descriptor);
        }
        if (_inputContext != 0)
            X11Native.XDestroyIC(_inputContext);
        if (_inputMethod != 0)
            X11Native.XCloseIM(_inputMethod);
        if (_display != 0)
        {
            foreach (var cursor in _cursors)
            {
                if (cursor != 0)
                    X11Native.XFreeCursor(_display, cursor);
            }
            if (_window != 0)
                X11Native.XDestroyWindow(_display, _window);
            X11Native.XCloseDisplay(_display);
        }
        _inputContext = _inputMethod = _display = 0;
        _window = 0;
        _atSpi?.ThrowIfFaulted();
    }
}
