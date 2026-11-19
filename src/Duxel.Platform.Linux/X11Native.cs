using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

// Xlib's long, XID and event padding use the LP64 ABI on Linux x64/ARM64.
internal static unsafe partial class X11Native
{
    [LibraryImport("libX11.so.6")] internal static partial nint XDisplayString(nint display);
    [LibraryImport("libX11.so.6")] internal static partial int XTranslateCoordinates(nint display, nuint source, nuint destination, int sourceX, int sourceY, out int x, out int y, out nuint child);
    private const string Library = "libX11.so.6";

    [StructLayout(LayoutKind.Sequential)]
    internal struct InputEvent
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Window, Root, Subwindow, Time;
        internal int X, Y, RootX, RootY;
        internal uint State, Detail;
        internal int SameScreen;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ConfigureEvent
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Event, Window;
        internal int X, Y, Width, Height, BorderWidth;
        internal nuint Above;
        internal int OverrideRedirect;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ClientMessage
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Window, MessageType;
        internal int Format;
        internal nint Data0, Data1, Data2, Data3, Data4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct Event
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(0)] internal InputEvent Input;
        [FieldOffset(0)] internal ConfigureEvent Configure;
        [FieldOffset(0)] internal ClientMessage Client;
        [FieldOffset(0)] internal PropertyEvent Property;
        [FieldOffset(96)] internal uint CrossingState;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyEvent
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display;
        internal nuint Window, Atom, Time;
        internal int State;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ResourceValue
    {
        internal uint Size; internal nint Address;
    }
    [LibraryImport(Library)] internal static partial int XGrabServer(nint display);
    [LibraryImport(Library)] internal static partial int XUngrabServer(nint display);
    [LibraryImport(Library)] internal static partial nuint XGetSelectionOwner(nint display, nuint selection);
    [LibraryImport(Library)] internal static partial int XGetWindowProperty(nint display, nuint window, nuint property, nint offset, nint length, int delete, nuint requestedType, out nuint actualType, out int format, out nuint count, out nuint remaining, out nint data);
    [LibraryImport(Library)] internal static partial int XFree(nint data);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint XrmGetStringDatabase(string data);
    [LibraryImport(Library)] internal static partial void XrmInitialize();
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int XrmGetResource(nint database, string name, string className, out nint type, out ResourceValue value);
    [LibraryImport(Library)] internal static partial void XrmDestroyDatabase(nint database);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PollDescriptor
    {
        internal int FileDescriptor;
        internal short Events, ReturnedEvents;
    }

    [LibraryImport(Library)] internal static partial int XInitThreads();
    [LibraryImport(Library)] internal static partial nint XOpenDisplay(nint name);
    [LibraryImport(Library)] internal static partial int XCloseDisplay(nint display);
    [LibraryImport(Library)] internal static partial nuint XDefaultRootWindow(nint display);
    [LibraryImport(Library)] internal static partial int XDefaultScreen(nint display);
    [LibraryImport(Library)] internal static partial int XDisplayWidth(nint display, int screen);
    [LibraryImport(Library)] internal static partial int XDisplayHeight(nint display, int screen);
    [LibraryImport(Library)] internal static partial int XConnectionNumber(nint display);
    [LibraryImport(Library)] internal static partial nuint XCreateSimpleWindow(nint display, nuint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
    [LibraryImport(Library)] internal static partial int XDestroyWindow(nint display, nuint window);
    [LibraryImport(Library)] internal static partial int XSelectInput(nint display, nuint window, nint mask);
    [LibraryImport(Library)] internal static partial int XMapWindow(nint display, nuint window);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial int XStoreName(nint display, nuint window, string name);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial nuint XInternAtom(nint display, string name, int onlyIfExists);
    [LibraryImport(Library)] internal static partial int XSetWMProtocols(nint display, nuint window, nuint* protocols, int count);
    [LibraryImport(Library)] internal static partial int XChangeProperty(nint display, nuint window, nuint property, nuint type, int format, int mode, byte* data, int count);
    [LibraryImport(Library)] internal static partial int XPending(nint display);
    [LibraryImport(Library)] internal static partial int XNextEvent(nint display, out Event value);
    [LibraryImport(Library)] internal static partial int XFlush(nint display);
    [LibraryImport(Library)] internal static partial nuint XLookupKeysym(ref InputEvent value, int index);
    [LibraryImport(Library)] internal static partial nuint XCreateFontCursor(nint display, uint shape);
    [LibraryImport(Library)] internal static partial int XDefineCursor(nint display, nuint window, nuint cursor);
    [LibraryImport(Library)] internal static partial int XFreeCursor(nint display, nuint cursor);
    [LibraryImport(Library)] internal static partial int XkbSetDetectableAutoRepeat(nint display, int detectable, out int supported);
    [LibraryImport(Library)] internal static partial int XkbGetAutoRepeatRate(nint display, uint device, out uint delay, out uint interval);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint XSetLocaleModifiers(string modifiers);
    [LibraryImport(Library)] internal static partial nint XOpenIM(nint display, nint database, nint name, nint className);
    [LibraryImport(Library)] internal static partial int XCloseIM(nint method);
    // XCreateIC's variadic arguments contain only pointer-sized integer values.
    [LibraryImport(Library)] internal static partial nint XCreateIC(nint method, byte* name1, nuint value1, byte* name2, nuint value2, byte* name3, nuint value3, nint end);
    [LibraryImport(Library)] internal static partial void XDestroyIC(nint context);
    [LibraryImport(Library)] internal static partial void XSetICFocus(nint context);
    [LibraryImport(Library)] internal static partial void XUnsetICFocus(nint context);
    [LibraryImport(Library)] internal static partial int XFilterEvent(ref Event value, nuint window);
    [LibraryImport(Library)] internal static partial int Xutf8LookupString(nint context, ref InputEvent value, byte* buffer, int length, out nuint symbol, out int status);
    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)] internal static partial nint setlocale(int category, string locale);
    [LibraryImport("libc.so.6", SetLastError = true)] internal static partial int eventfd(uint value, int flags);
    [LibraryImport("libc.so.6", SetLastError = true)] internal static partial nint read(int descriptor, void* buffer, nuint count);
    [LibraryImport("libc.so.6", SetLastError = true)] internal static partial nint write(int descriptor, void* buffer, nuint count);
    [LibraryImport("libc.so.6", SetLastError = true)] internal static partial int close(int descriptor);
    [LibraryImport("libc.so.6", SetLastError = true)] internal static partial int poll(PollDescriptor* descriptors, nuint count, int timeout);
}
