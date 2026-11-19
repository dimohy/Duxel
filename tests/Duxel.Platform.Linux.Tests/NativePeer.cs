using System.Runtime.InteropServices;

internal sealed unsafe partial class NativePeer : IDisposable
{
    private readonly nint _display = OpenDisplay(0);
    private readonly nuint _window;

    internal NativePeer(nuint window)
    {
        if (_display == 0)
            throw new InvalidOperationException("External fixture X11 connection unavailable.");
        _window = window;
    }

    // Independent Xlib LP64 wire-client structures exercise the backend through the server.
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct Event
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(16)] internal int SendEvent;
        [FieldOffset(24)] internal nint Display;
        [FieldOffset(32)] internal nuint Window;
        [FieldOffset(40)] internal nuint RootOrMessage;
        [FieldOffset(48)] internal int Format;
        [FieldOffset(56)] internal nint Data0;
        [FieldOffset(64)] internal int X;
        [FieldOffset(68)] internal int Y;
        [FieldOffset(80)] internal uint State;
        [FieldOffset(84)] internal uint Detail;
        [FieldOffset(88)] internal int SameScreen;
        [FieldOffset(96)] internal uint CrossingState;
    }

    internal void Pointer(int type, int x, int y, uint detail = 0, uint state = 0, bool flush = true)
    {
        var value = Input(type);
        value.X = x;
        value.Y = y;
        value.Detail = detail;
        if (type is 7 or 8)
            value.CrossingState = state;
        else
            value.State = state;
        Send(ref value, type switch
        {
            4 => 4,
            5 => 8,
            6 => 64,
            7 => 16,
            8 => 32,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        }, flush);
    }

    internal void Key(bool down, nuint symbol, uint state = 0)
        => SendKey(down, symbol, state, flush: true);

    internal void KeyBurst(ReadOnlySpan<nuint> symbols)
    {
        foreach (var symbol in symbols)
        {
            SendKey(true, symbol, 0, flush: false);
            SendKey(false, symbol, 0, flush: false);
        }
        Flush(_display);
    }

    private void SendKey(bool down, nuint symbol, uint state, bool flush)
    {
        var value = Input(down ? 2 : 3);
        value.State = state;
        value.Detail = KeysymToKeycode(_display, symbol);
        if (value.Detail == 0)
            throw new InvalidOperationException("Fixture key symbol has no actual keycode.");
        Send(ref value, down ? 1 : 2, flush);
    }

    private Event Input(int type) => new()
    {
        Type = type,
        SendEvent = 1,
        Display = _display,
        Window = _window,
        RootOrMessage = DefaultRootWindow(_display),
        SameScreen = 1
    };

    internal void Close()
    {
        var value = new Event
        {
            Type = 33,
            SendEvent = 1,
            Display = _display,
            Window = _window,
            RootOrMessage = InternAtom(_display, "WM_PROTOCOLS", 0),
            Format = 32,
            Data0 = (nint)InternAtom(_display, "WM_DELETE_WINDOW", 0)
        };
        Send(ref value, 0);
    }

    private void Send(ref Event value, nint mask, bool flush = true)
    {
        if (SendEvent(_display, _window, 0, mask, ref value) == 0)
            throw new InvalidOperationException("Owned X11 event could not be sent.");
        if (flush)
            Flush(_display);
    }

    public void Dispose() => CloseDisplay(_display);

    internal bool IsInitiallyCentered()
    {
        if (Geometry(_display, _window, out _, out var x, out var y, out var width, out var height, out _, out _) == 0)
            throw new InvalidOperationException("Owned unmapped window geometry is unavailable.");
        var screen = GeometryScreen(_display);
        return x == (GeometryWidth(_display, screen) - (int)width) / 2
            && y == (GeometryHeight(_display, screen) - (int)height) / 2;
    }

    [LibraryImport("libX11.so.6", EntryPoint = "XGetGeometry")] private static partial int Geometry(nint display, nuint window, out nuint root, out int x, out int y, out uint width, out uint height, out uint border, out uint depth);
    [LibraryImport("libX11.so.6", EntryPoint = "XDefaultScreen")] private static partial int GeometryScreen(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XDisplayWidth")] private static partial int GeometryWidth(nint display, int screen);
    [LibraryImport("libX11.so.6", EntryPoint = "XDisplayHeight")] private static partial int GeometryHeight(nint display, int screen);

    [LibraryImport("libX11.so.6", EntryPoint = "XOpenDisplay")] private static partial nint OpenDisplay(nint name);
    [LibraryImport("libX11.so.6", EntryPoint = "XCloseDisplay")] private static partial int CloseDisplay(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")] private static partial nuint DefaultRootWindow(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XSendEvent")] private static partial int SendEvent(nint display, nuint window, int propagate, nint mask, ref Event value);
    [LibraryImport("libX11.so.6", EntryPoint = "XFlush")] private static partial int Flush(nint display);
    [LibraryImport("libX11.so.6", EntryPoint = "XKeysymToKeycode")] private static partial byte KeysymToKeycode(nint display, nuint symbol);
    [LibraryImport("libX11.so.6", EntryPoint = "XInternAtom", StringMarshalling = StringMarshalling.Utf8)] private static partial nuint InternAtom(nint display, string name, int onlyIfExists);
}
