using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static Duxel.Platform.Linux.XcbNative;

namespace Duxel.Platform.Linux;

internal sealed class XcbProtocolException(byte error, string operation) : InvalidOperationException($"XCB X11 {operation} failed: protocol error {error}.")
{
    internal byte Error { get; } = error;
}

internal sealed class XcbPropertyLimitException() : IOException("X11 property exceeds the configured byte limit.");

// Each instance and its hidden input-only window belong to one thread.
internal sealed unsafe class XcbConnection : IDisposable
{
    internal nint Handle
    {
        get; private set;
    }
    internal uint Window
    {
        get;
    }
    internal int Descriptor
    {
        get;
    }
    internal uint MaximumRequestBytes
    {
        get;
    }
    internal readonly record struct Property(uint Type, byte Format, byte[] Data);

    internal XcbConnection(string display)
    {
        Handle = xcb_connect(display, out var screen);
        try
        {
            CheckConnection();
            var iterator = xcb_setup_roots_iterator(xcb_get_setup(Handle));
            for (var i = 0; i < screen; i++)
                xcb_screen_next(ref iterator);
            if (iterator.Remaining <= 0 || iterator.Data == null)
                throw new InvalidOperationException("The X11 default screen is unavailable.");
            Window = xcb_generate_id(Handle);
            uint events = 1u << 22;
            Check(xcb_create_window_checked(Handle, 0, Window, *(uint*)iterator.Data, 0, 0, 1, 1, 0, 2, 0, 1u << 11, &events), "create window");
            Descriptor = xcb_get_file_descriptor(Handle);
            MaximumRequestBytes = checked(xcb_get_maximum_request_length(Handle) * 4);
            Flush();
        }
        catch { Dispose(); throw; }
    }

    internal void CheckConnection()
    {
        var error = xcb_connection_has_error(Handle);
        if (error != 0)
            throw new IOException("XCB X11 connection failed: " + error);
    }

    private static void CheckError(void* error, string operation)
    {
        if (error == null)
            return;
        var code = ((byte*)error)[1];
        free(error);
        throw new XcbProtocolException(code, operation);
    }

    internal void Check(Cookie cookie, string operation)
    {
        CheckError(xcb_request_check(Handle, cookie), operation);
        CheckConnection();
    }

    internal uint Atom(string name)
    {
        var bytes = new UTF8Encoding(false, true).GetBytes(name);
        if (bytes.Length is 0 or > ushort.MaxValue || name.Contains('\0'))
            throw new ArgumentException("X11 atom names must contain 1..65535 UTF-8 bytes without NUL.", nameof(name));
        void* error = null;
        void* reply;
        fixed (byte* data = bytes)
            reply = xcb_intern_atom_reply(Handle, xcb_intern_atom(Handle, 0, (ushort)bytes.Length, data), &error);
        try
        {
            CheckError(error, "intern atom");
            CheckConnection();
            if (reply == null)
                throw new IOException("XCB returned no atom reply.");
            return *(uint*)((byte*)reply + 8);
        }
        finally { free(reply); }
    }

    internal uint Owner(uint selection)
    {
        void* error = null;
        var reply = xcb_get_selection_owner_reply(Handle, xcb_get_selection_owner(Handle, selection), &error);
        try
        {
            CheckError(error, "query owner");
            CheckConnection();
            if (reply == null)
                throw new IOException("XCB returned no selection-owner reply.");
            return *(uint*)((byte*)reply + 8);
        }
        finally { free(reply); }
    }

    internal bool FocusWindow(uint window)
    {
        try
        {
            Check(xcb_set_input_focus_checked(Handle, 2, window, 0), "focus window");
        }
        catch (XcbProtocolException error) when (error.Error is 3 or 8) { return false; }
        void* failure = null;
        var reply = xcb_get_input_focus_reply(Handle, xcb_get_input_focus(Handle), &failure);
        try
        {
            CheckError(failure, "verify input focus");
            CheckConnection();
            if (reply == null)
                throw new IOException("XCB returned no input-focus reply.");
            return *(uint*)((byte*)reply + 8) == window;
        }
        finally { free(reply); }
    }

    internal void SetOwner(uint selection, uint time) => Check(xcb_set_selection_owner_checked(Handle, Window, selection, time), "acquire ownership");
    internal void Convert(uint selection, uint target, uint property, uint time) => Check(xcb_convert_selection_checked(Handle, Window, selection, target, property, time), "convert selection");
    internal void Delete(uint window, uint property) => Check(xcb_delete_property_checked(Handle, window, property), "delete property");

    internal void Put(uint window, uint property, uint type, byte format, ReadOnlySpan<byte> data)
    {
        if (format is not (8 or 32) || data.Length % (format / 8) != 0)
            throw new ArgumentException("X11 properties must have complete 8/32-bit elements.");
        fixed (byte* pointer = data)
            Check(xcb_change_property_checked(Handle, 0, window, property, type, format, (uint)(data.Length / (format / 8)), pointer), "write property");
    }

    internal void PutAtoms(uint window, uint property, uint type, ReadOnlySpan<uint> data) => Put(window, property, type, 32, MemoryMarshal.AsBytes(data));
    internal void Subscribe(uint window)
    {
        uint events = (1u << 22) | (1u << 17);
        Check(xcb_change_window_attributes_checked(Handle, window, 1u << 11, &events), "subscribe requestor");
    }
    internal void Unsubscribe(uint window)
    {
        uint events = 0;
        Check(xcb_change_window_attributes_checked(Handle, window, 1u << 11, &events), "retire requestor subscription");
    }

    internal Property Read(uint window, uint property, int maximumBytes)
    {
        void* error = null;
        var reply = xcb_get_property_reply(Handle, xcb_get_property(Handle, 1, window, property, 0, 0, (uint)((maximumBytes + 3L) / 4)), &error);
        try
        {
            CheckError(error, "read property");
            CheckConnection();
            if (reply == null)
                throw new IOException("XCB returned no property reply.");
            var bytes = checked((long)reply->Count * reply->Format / 8);
            if (reply->BytesAfter != 0 || bytes > maximumBytes)
                throw new XcbPropertyLimitException();
            var data = new ReadOnlySpan<byte>(reply + 1, checked((int)bytes)).ToArray();
            return new(reply->Type, reply->Format, data);
        }
        finally { free(reply); }
    }

    internal void Notify(uint requestor, uint selection, uint target, uint property, uint time)
    {
        var value = new Event { Type = 31, A = time, B = requestor, C = selection, D = target, E = property };
        Check(xcb_send_event_checked(Handle, 0, requestor, 0, &value), "notify requestor");
    }

    internal bool TryEvent(out Event value)
    {
        var pointer = xcb_poll_for_event(Handle);
        if (pointer == null)
        {
            CheckConnection();
            value = default;
            return false;
        }
        try
        {
            if (pointer->Type == 0)
                throw new XcbProtocolException(((byte*)pointer)[1], "asynchronous request");
            value = *pointer;
            value.Type &= 0x7f;
            return true;
        }
        finally { free(pointer); }
    }

    internal void Flush()
    {
        if (xcb_flush(Handle) <= 0)
        {
            CheckConnection();
            throw new IOException("XCB X11 flush failed.");
        }
    }

    internal void Wait(int milliseconds, int wake = -1)
    {
        Flush();
        var descriptors = stackalloc X11Native.PollDescriptor[2];
        descriptors[0] = new()
        {
            FileDescriptor = Descriptor,
            Events = 1
        };
        descriptors[1] = new()
        {
            FileDescriptor = wake,
            Events = 1
        };
        var result = X11Native.poll(descriptors, wake >= 0 ? 2u : 1u, milliseconds);
        if (result < 0 && Marshal.GetLastPInvokeError() != 4)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "X11 native wait failed.");
        if (((descriptors[0].ReturnedEvents | descriptors[1].ReturnedEvents) & (8 | 16 | 32)) != 0)
            throw new IOException("The X11 event connection closed.");
        if (wake >= 0 && (descriptors[1].ReturnedEvents & 1) != 0)
        {
            ulong signal;
            if (X11Native.read(wake, &signal, 8) != 8)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "X11 wake drain failed.");
        }
    }

    public void Dispose()
    {
        if (Handle != 0)
            xcb_disconnect(Handle);
        Handle = 0;
    }
}
