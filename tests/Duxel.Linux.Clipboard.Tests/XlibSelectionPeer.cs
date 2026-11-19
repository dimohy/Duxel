using System.Runtime.InteropServices;
using System.Text;

// Independent Xlib/LP64 oracle. Production uses XCB's 32-bit wire events instead.
internal sealed unsafe partial class XlibSelectionPeer : IDisposable
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _stop;
    private readonly string _selection;
    private readonly byte[] _text;
    private readonly bool _silent;
    internal ManualResetEventSlim Requested { get; } = new(false);
    private Exception? _failure;

    static XlibSelectionPeer()
    {
        if (XInitThreads() == 0)
            throw new InvalidOperationException("Independent Xlib oracle thread initialization failed.");
    }
    internal XlibSelectionPeer(string selection, string text, bool silent = false)
    {
        _selection = selection;
        _text = Encoding.UTF8.GetBytes(text);
        _silent = silent;
        _thread = new Thread(Serve) { IsBackground = true, Name = "IndependentXlibSelection" };
        _thread.Start();
        _ready.Task.GetAwaiter().GetResult();
    }
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct Event
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(16)] internal int Sent;
        [FieldOffset(24)] internal nint Display;
        [FieldOffset(32)] internal nuint A;
        [FieldOffset(40)] internal nuint B;
        [FieldOffset(48)] internal nuint C;
        [FieldOffset(56)] internal nuint D;
        [FieldOffset(64)] internal nuint E;
        [FieldOffset(72)] internal nuint F;
        [FieldOffset(56)] internal int State;
    }

    private sealed class Scope : IDisposable
    {
        internal readonly nint Display = XOpenDisplay(0);
        internal readonly nuint Window;
        internal Scope()
        {
            if (Display == 0)
                throw new InvalidOperationException("Independent Xlib connection failed.");
            Window = XCreateSimpleWindow(Display, XDefaultRootWindow(Display), 0, 0, 1, 1, 0, 0, 0);
            XSelectInput(Display, Window, 1 << 22);
        }
        internal nuint Atom(string name) => XInternAtom(Display, name, 0);
        internal Event Next(long deadline)
        {
            while (Environment.TickCount64 < deadline)
            {
                if (XPending(Display) != 0)
                {
                    XNextEvent(Display, out var e);
                    return e;
                }
                Thread.Sleep(1);
            }
            throw new TimeoutException("Independent Xlib selection event timed out.");
        }
        public void Dispose() => XCloseDisplay(Display);
    }

    internal sealed record Value(bool Accepted, bool Incremental, nuint Type, int Format, byte[] Bytes, uint[] Words);
    private static Value Property(Scope c, nuint window, nuint atom)
    {
        var status = XGetWindowProperty(c.Display, window, atom, 0, 4 * 1024 * 1024, 1, 0,
            out var type, out var format, out var count, out var after, out var data);
        try
        {
            if (status != 0 || after != 0 || count > 16 * 1024 * 1024)
                throw new InvalidOperationException("Independent property bounds failed.");
            if (format == 8)
                return new(true, false, type, format, new ReadOnlySpan<byte>((void*)data, checked((int)count)).ToArray(), []);
            if (format != 32)
                throw new InvalidOperationException("Unexpected independent property format.");
            var words = new uint[checked((int)count)];
            for (var i = 0; i < words.Length; i++)
                words[i] = checked((uint)((nuint*)data)[i]);
            return new(true, false, type, format, [], words);
        }
        finally { if (data != 0) XFree(data); }
    }

    internal static Value Read(string selection, string target, bool abandonIncremental = false)
    {
        using var c = new Scope();
        var property = c.Atom("_DUXEL_INDEPENDENT_READ");
        var targetAtom = c.Atom(target);
        XConvertSelection(c.Display, c.Atom(selection), targetAtom, property, c.Window, 0);
        XFlush(c.Display);
        var deadline = Environment.TickCount64 + 5000;
        while (true)
        {
            var e = c.Next(deadline);
            if (e.Type != 31 || e.A != c.Window || e.C != targetAtom)
                continue;
            if (e.D == 0)
                return new(false, false, 0, 0, [], []);
            var value = Property(c, c.Window, e.D);
            if (value.Type != c.Atom("INCR"))
                return value;
            if (abandonIncremental)
                return value with
                {
                    Incremental = true
                };
            using var bytes = new MemoryStream();
            while (true)
            {
                e = c.Next(deadline);
                if (e.Type != 28 || e.A != c.Window || e.B != property || e.State != 0)
                    continue;
                value = Property(c, c.Window, property);
                if (value.Bytes.Length == 0)
                    return value with
                    {
                        Incremental = true,
                        Bytes = bytes.ToArray()
                    };
                bytes.Write(value.Bytes);
            }
        }
    }

    internal static uint[] Targets(string selection)
    {
        var value = Read(selection, "TARGETS");
        if (!value.Accepted || value.Format != 32)
            throw new InvalidOperationException("Independent TARGETS request failed.");
        return value.Words;
    }

    internal static bool Multiple(string selection, string expected, bool oversized = false)
    {
        using var c = new Scope();
        var control = c.Atom("_DUXEL_MULTIPLE_CONTROL");
        var text = c.Atom("_DUXEL_MULTIPLE_TEXT");
        var time = c.Atom("_DUXEL_MULTIPLE_TIME");
        var missing = c.Atom("_DUXEL_MULTIPLE_MISSING");
        var utf8 = c.Atom("UTF8_STRING");
        var multiple = c.Atom("MULTIPLE");
        nuint[] pairs = oversized ? new nuint[20000] :
            [utf8, text, c.Atom("TIMESTAMP"), time, missing, missing, multiple, missing, utf8, text, utf8, control];
        fixed (nuint* data = pairs)
            XChangeProperty(c.Display, c.Window, control, c.Atom("ATOM_PAIR"), 32, 0, (byte*)data, pairs.Length);
        XConvertSelection(c.Display, c.Atom(selection), multiple, control, c.Window, 0);
        XFlush(c.Display);
        while (true)
        {
            var e = c.Next(Environment.TickCount64 + 5000);
            if (e.Type != 31 || e.A != c.Window || e.C != multiple)
                continue;
            if (oversized)
                return e.D == 0;
            if (e.D != control)
                return false;
            var results = Property(c, c.Window, control).Words;
            return results.Length == 12 && results[1] == text && results[3] == time
                && results[5] == 0 && results[7] == 0 && results[9] == 0 && results[11] == 0
                && Encoding.UTF8.GetString(Property(c, c.Window, text).Bytes) == expected
                && Property(c, c.Window, time).Words.Length == 1;
        }
    }

    internal static uint Atom(string name)
    {
        using var scope = new Scope();
        return checked((uint)scope.Atom(name));
    }

    private void Serve()
    {
        try
        {
            using var c = new Scope();
            var selection = c.Atom(_selection);
            var utf8 = c.Atom("UTF8_STRING");
            var targets = c.Atom("TARGETS");
            var incr = c.Atom("INCR");
            XSetSelectionOwner(c.Display, selection, c.Window, 0);
            if (XGetSelectionOwner(c.Display, selection) != c.Window)
                throw new InvalidOperationException("Independent owner could not acquire its private selection.");
            _ready.SetResult();
            var outgoing = new Dictionary<(nuint Window, nuint Property), int>();
            nuint* supported = stackalloc nuint[2] { targets, utf8 };
            while (!Volatile.Read(ref _stop))
            {
                if (XPending(c.Display) == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }
                XNextEvent(c.Display, out var e);
                if (e.Type == 30)
                {
                    Requested.Set();
                    if (_silent)
                        continue;
                    var property = e.E == 0 ? e.D : e.E;
                    if (e.D == targets)
                    {
                        XChangeProperty(c.Display, e.B, property, c.Atom("ATOM"), 32, 0, (byte*)supported, 2);
                    }
                    else if (e.D == utf8 && _text.Length > 4093)
                    {
                        XSelectInput(c.Display, e.B, 1 << 22);
                        nuint length = (nuint)_text.Length;
                        XChangeProperty(c.Display, e.B, property, incr, 32, 0, (byte*)&length, 1);
                        outgoing.Add((e.B, property), 0);
                    }
                    else if (e.D == utf8)
                    {
                        fixed (byte* bytes = _text)
                            XChangeProperty(c.Display, e.B, property, utf8, 8, 0, bytes, _text.Length);
                    }
                    else
                        property = 0;
                    var notification = new Event { Type = 31, Sent = 1, Display = c.Display, A = e.B, B = selection, C = e.D, D = property, E = e.F };
                    XSendEvent(c.Display, e.B, 0, 0, ref notification);
                    XFlush(c.Display);
                }
                else if (e.Type == 28 && e.State == 1 && outgoing.TryGetValue((e.A, e.B), out var offset))
                {
                    var count = Math.Min(4093, _text.Length - offset);
                    fixed (byte* bytes = _text)
                        XChangeProperty(c.Display, e.A, e.B, utf8, 8, 0, bytes + offset, count);
                    if (count == 0)
                        outgoing.Remove((e.A, e.B));
                    else
                        outgoing[(e.A, e.B)] = offset + count;
                    XFlush(c.Display);
                }
            }
        }
        catch (Exception error) { _failure = error; _ready.TrySetException(error); }
    }

    public void Dispose()
    {
        Volatile.Write(ref _stop, true);
        _thread.Join();
        Requested.Dispose();
        if (_failure is not null)
            throw new InvalidOperationException("Independent selection oracle failed.", _failure);
    }

    [LibraryImport("libX11.so.6")] private static partial int XInitThreads();
    [LibraryImport("libX11.so.6")] private static partial nint XOpenDisplay(nint name);
    [LibraryImport("libX11.so.6")] private static partial int XCloseDisplay(nint display);
    [LibraryImport("libX11.so.6")] private static partial nuint XDefaultRootWindow(nint display);
    [LibraryImport("libX11.so.6")] private static partial nuint XCreateSimpleWindow(nint display, nuint parent, int x, int y, uint width, uint height, uint border, nuint borderPixel, nuint background);
    [LibraryImport("libX11.so.6")] private static partial int XSelectInput(nint display, nuint window, nint mask);
    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)] private static partial nuint XInternAtom(nint display, string name, int only);
    [LibraryImport("libX11.so.6")] private static partial int XSetSelectionOwner(nint display, nuint selection, nuint owner, nuint time);
    [LibraryImport("libX11.so.6")] private static partial nuint XGetSelectionOwner(nint display, nuint selection);
    [LibraryImport("libX11.so.6")] private static partial int XConvertSelection(nint display, nuint selection, nuint target, nuint property, nuint window, nuint time);
    [LibraryImport("libX11.so.6")] private static partial int XPending(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XNextEvent(nint display, out Event value);
    [LibraryImport("libX11.so.6")] private static partial int XFlush(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XSendEvent(nint display, nuint window, int propagate, nint mask, ref Event value);
    [LibraryImport("libX11.so.6")] private static partial int XChangeProperty(nint display, nuint window, nuint property, nuint type, int format, int mode, byte* data, int count);
    [LibraryImport("libX11.so.6")] private static partial int XGetWindowProperty(nint display, nuint window, nuint property, nint offset, nint length, int delete, nuint type, out nuint actual, out int format, out nuint count, out nuint after, out nint data);
    [LibraryImport("libX11.so.6")] private static partial int XFree(nint pointer);
}
