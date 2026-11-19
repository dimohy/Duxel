using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeValue
    {
        internal nuint Type, Data0, Data1;
    }
    private sealed record TextChange(string Type, int Start, int Length, string Text);
    [StructLayout(LayoutKind.Sequential)]
    private struct EventPrefix
    {
        internal nint Type, Source;
        internal int Detail1, Detail2;
        internal NativeValue AnyData;
    }
    private sealed class EventWatch : IDisposable
    {
        private readonly GCHandle _self;
        private readonly NativeObject _listener;
        private readonly List<string> _registered = [];
        internal nint Button, Input, Slider, Selection;
        internal nint AnnouncementSource;
        internal nint TooltipTarget;
        internal readonly List<NativeAnnouncementEvent> Announcements = [];
        internal readonly List<(string Type, int Index)> TooltipChildren = [];
        internal int Names, Focus, Values, Selections, TextSelections, Carets, LastCaret;
        internal int TextAttributes;
        internal readonly List<TextChange> TextChanges = [];
        internal Exception? Failure;
        internal EventWatch(bool announcements = false, bool tooltipLifecycle = false)
        {
            _self = GCHandle.Alloc(this);
            try
            {
                _listener = new(atspi_event_listener_new((nint)(delegate* unmanaged[Cdecl]<nint, nint, void>)&Observe, GCHandle.ToIntPtr(_self), 0));
            }
            catch { _self.Free(); throw; }
            try
            {
                var types = tooltipLifecycle ? new[] { "object:children-changed" }
                    : announcements ? new[] { "object:announcement" }
                    : new[] { "object:property-change:accessible-name", "object:state-changed:focused", "object:property-change:accessible-value", "object:selection-changed", "object:text-caret-moved", "object:text-selection-changed", "object:text-changed", "object:text-attributes-changed" };
                foreach (var type in types)
                {
                    nint error = 0;
                    var registered = atspi_event_listener_register(_listener.Value, type, &error);
                    Error(ref error);
                    if (registered == 0)
                        throw new InvalidOperationException("The native event listener could not register " + type);
                    _registered.Add(type);
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Check()
        {
            if (Failure is not null)
                throw new InvalidOperationException("The independent native event callback failed.", Failure);
        }
        public void Dispose()
        {
            List<Exception> errors = [];
            foreach (var type in _registered)
            {
                nint error = 0;
                atspi_event_listener_deregister(_listener.Value, type, &error);
                try
                {
                    Error(ref error);
                }
                catch (Exception failure) { errors.Add(failure); }
            }
            _registered.Clear();
            _listener.Dispose();
            _self.Free();
            if (errors.Count != 0)
                throw new AggregateException(errors);
        }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Observe(nint nativeEvent, nint data)
    {
        var watch = (EventWatch)GCHandle.FromIntPtr(data).Target!;
        try
        {
            var value = *(EventPrefix*)nativeEvent;
            var type = Marshal.PtrToStringUTF8(value.Type);
            if (watch.TooltipTarget != 0 && value.Source == watch.TooltipTarget
                && type is "object:children-changed:add" or "object:children-changed:remove")
                watch.TooltipChildren.Add((type, value.Detail1));
            if (watch.AnnouncementSource != 0 && value.Source == watch.AnnouncementSource && type == "object:announcement")
            {
                var text = g_value_get_string((nint)(&((EventPrefix*)nativeEvent)->AnyData));
                watch.Announcements.Add(new(value.Detail1, value.Detail2,
                    Marshal.PtrToStringUTF8(text) ?? throw new InvalidDataException("Missing native announcement payload.")));
            }
            if (watch.Input != 0 && value.Source == watch.Input && type == "object:text-attributes-changed")
                Interlocked.Increment(ref watch.TextAttributes);
            if (watch.Button != 0 && value.Source == watch.Button && type == "object:property-change:accessible-name")
                Interlocked.Increment(ref watch.Names);
            if (watch.Input != 0 && value.Source == watch.Input && type == "object:state-changed:focused")
                Interlocked.Increment(ref watch.Focus);
            if (watch.Slider != 0 && value.Source == watch.Slider && type == "object:property-change:accessible-value")
                Interlocked.Increment(ref watch.Values);
            if (watch.Selection != 0 && value.Source == watch.Selection && type == "object:selection-changed")
                Interlocked.Increment(ref watch.Selections);
            if (watch.Input != 0 && value.Source == watch.Input && type == "object:text-selection-changed")
                Interlocked.Increment(ref watch.TextSelections);
            if (watch.Input != 0 && value.Source == watch.Input && type == "object:text-caret-moved")
            {
                watch.LastCaret = value.Detail1;
                Interlocked.Increment(ref watch.Carets);
            }
            if (watch.Input != 0 && value.Source == watch.Input && type is "object:text-changed:insert" or "object:text-changed:delete")
            {
                var text = g_value_get_string((nint)(&((EventPrefix*)nativeEvent)->AnyData));
                watch.TextChanges.Add(new(type, value.Detail1, value.Detail2,
                    Marshal.PtrToStringUTF8(text) ?? throw new InvalidDataException("Missing native text-change payload.")));
            }
        }
        catch (Exception error) { watch.Failure = error; }
        finally { g_boxed_free(atspi_event_get_type(), nativeEvent); }
    }
    [LibraryImport(Library)] private static partial nint atspi_event_listener_new(nint callback, nint data, nint destroyed);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial int atspi_event_listener_register(nint listener, string type, nint* error);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial int atspi_event_listener_deregister(nint listener, string type, nint* error);
    [LibraryImport(Library)] private static partial nuint atspi_event_get_type();
    [LibraryImport(Library)] private static partial void atspi_accessible_set_cache_mask(nint accessible, uint mask);
    [LibraryImport("libgobject-2.0.so.0")] private static partial void g_boxed_free(nuint type, nint value);
    [LibraryImport("libgobject-2.0.so.0")] private static partial nint g_value_get_string(nint value);
}
