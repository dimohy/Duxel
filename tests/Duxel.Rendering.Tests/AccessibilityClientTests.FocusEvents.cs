using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckFocusEvents(nint automation, nint root, Dictionary<string, nint> found, List<nint> owned,
        Action<bool, string> check, string coordinationDirectory)
    {
        check(new[] { "Focus tabs", "Alpha", "Beta", "Alpha body action", "Logical focus : none" }.All(found.ContainsKey),
            "external client discovers actual generated tabs and their body controls");
        var ids = new[] { "Alpha", "Beta", "Alpha body action" }.Select(name => Text(found[name], 30011)).ToHashSet();
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        foreach (var property in new[] { 30002, 30005, 30008, 30011 })
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, property));
        GetWindowThreadProcessId(WindowFromRoot(), out var processId);
        var sink = new FocusEventSink(ids, processId);
        var handler = (nint)ComInterfaceMarshaller<IFocusEventHandler>.ConvertToUnmanaged(sink);
        // UIAutomationClient.h includes both property-handler overloads before these slots.
        const int addFocusHandlerSlot = 39;
        const int removeFocusHandlerSlot = 40;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(automation, addFocusHandlerSlot))(automation, cache, handler));
        try
        {
            ReadPropertyChanges(automation, root, 4, owned, new[] { 30008 }, properties =>
            {
                Focus("Alpha");
                check(sink.Wait().Name == "Alpha" && Property() == (Id("Alpha"), true),
                    "external SetFocus emits the committed actual tab and keyboard-focus property");
                check(FocusedName() == "Alpha" && Boolean(found["Alpha"], 30008) && !Boolean(found["Focus tabs"], 30008),
                    "native GetFocusedElement identifies the child while its nonfocusable parent stays unfocused");
                Focus("Alpha");
                Command("right");
                check(sink.Wait().Name == "Beta" && new[] { Property(), Property() }.ToHashSet().SetEquals(
                    new[] { (Id("Alpha"), false), (Id("Beta"), true) }),
                    "actual arrow navigation emits one focus transfer with both changed native properties");
                check(Boolean(found["Alpha"], 30079) && !Boolean(found["Beta"], 30079) && sink.Count == 2,
                    "manual focus navigation preserves selection and an unchanged SetFocus emits no duplicate");
                Command("peer");
                check(Property() == (Id("Beta"), false) && FocusedName() == "Duxel owned focus peer",
                    "moving to the owned peer withdraws the original tab's real keyboard focus");
                Command("left");
                WaitUntil(() => Text(found["Logical focus : none"], 30005) == "Logical focus : Alpha");
                check(!Boolean(found["Alpha"], 30008) && !Boolean(found["Beta"], 30008) && sink.Count == 2,
                    "background logical focus changes cannot emit a global virtual focus event or claim keyboard focus");
                Focus("Alpha");
                check(sink.Wait().Name == "Alpha" && Property() == (Id("Alpha"), true) && FocusedName() == "Alpha",
                    "native window reactivation restores the current virtual focus without a new logical change");
                Focus("Alpha body action");
                check(sink.Wait().Name == "Alpha body action" && new[] { Property(), Property() }.ToHashSet().SetEquals(
                    new[] { (Id("Alpha"), false), (Id("Alpha body action"), true) }),
                    "panel body focus uses the actual control rather than the selected tab or parent");
                sink.ClearRootEvents();
                Command("remove");
                WaitUntil(() => TooltipElementUnavailable(found["Alpha body action"]));
                check(FocusedName() == "Duxel owned focus event fixture" && Boolean(root, 30008),
                    "removing focused content leaves real native focus on the surviving window root");
                check(sink.WaitRoot().Name == "Duxel owned focus event fixture" && sink.Count == 4,
                    "child removal reports the root transition without an unavailable child event or idle duplicate");

                (string Id, bool Value) Property()
                {
                    var deadline = Environment.TickCount64 + 5000;
                    while (Environment.TickCount64 < deadline)
                    {
                        var change = properties.Wait();
                        if (ids.Contains(change.SourceId) && change.Value is bool value) return (change.SourceId, value);
                    }
                    throw new TimeoutException("Expected owned virtual keyboard-focus property.");
                }
            });
        }
        finally
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(automation, removeFocusHandlerSlot))(automation, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
        Command("finish");

        string Id(string name) => Text(found[name], 30011);
        nint WindowFromRoot()
        {
            UiaVariant value = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(root, 10))(root, 30020, &value));
            try
            {
                if (value.Type != 3) throw new InvalidOperationException("The focus fixture root must expose its native HWND.");
                return value.Data.Integer;
            }
            finally { Ok(UiaNative.VariantClear(ref value)); }
        }
        void Focus(string name) => Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(found[name], 3))(found[name]));
        void Command(string command)
        {
            // Coordination must not call UIA Invoke on a different focusable control.
            var temporary = Path.Combine(coordinationDirectory, "command.pending");
            File.WriteAllText(temporary, command);
            File.Move(temporary, Path.Combine(coordinationDirectory, "command.txt"), overwrite : true);
        }
        string FocusedName()
        {
            nint element = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 8))(automation, &element));
            if (element == 0) throw new InvalidOperationException("The owned window has no UIA focused element.");
            try { return Text(element, 30005); }
            finally { Marshal.Release(element); }
        }
        static void WaitUntil(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= deadline) throw new TimeoutException("The owned focus fixture did not commit its expected state.");
                Thread.Sleep(5);
            }
        }
    }

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextW(nint window, char* value, int length);
}

[GeneratedComInterface]
[Guid("c270f6b5-5c69-4290-9745-7a7f97169468")]
internal partial interface IFocusEventHandler
{
    [PreserveSig]
    int HandleFocusChangedEvent(nint sender);
}

internal readonly record struct NativeFocusEvent(string Id, string Name, bool Focused);

[GeneratedComClass]
internal sealed unsafe partial class FocusEventSink(HashSet<string> ids, uint processId) : IFocusEventHandler
{
    private readonly ConcurrentQueue<NativeFocusEvent> _events = new();
    private readonly ConcurrentQueue<NativeFocusEvent> _rootEvents = new();
    private Exception? _failure;
    private int _count;
    internal int Count => Volatile.Read(ref _count);

    public int HandleFocusChangedEvent(nint sender)
    {
        try
        {
            UiaVariant process = default;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)(*(nint**)sender)[12])(sender, 30002, &process));
            try { if (process.Type != 3 || process.Data.Integer != processId) return 0; }
            finally { Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref process)); }
            var id = TooltipEventSink.CachedText(sender, 30011);
            var name = TooltipEventSink.CachedText(sender, 30005);
            if (!ids.Contains(id) && name != "Duxel owned focus event fixture") return 0;
            UiaVariant focused = default;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)(*(nint**)sender)[12])(sender, 30008, &focused));
            try
            {
                if (focused.Type != 11 || focused.Data.Boolean == 0)
                    throw new InvalidOperationException("A cached focus event must identify a currently focused target.");
                var item = new NativeFocusEvent(id, name, true);
                Console.WriteLine($"NativeFocus : {name} / {id}");
                if (ids.Contains(id))
                {
                    Interlocked.Increment(ref _count);
                    _events.Enqueue(item);
                }
                else _rootEvents.Enqueue(item);
            }
            finally { Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref focused)); }
            return 0;
        }
        catch (Exception failure)
        {
            Interlocked.CompareExchange(ref _failure, failure, null);
            return Marshal.GetHRForException(failure);
        }
    }

    internal void ClearRootEvents() { while (_rootEvents.TryDequeue(out _)) { } }
    internal NativeFocusEvent Wait() => Wait(_events);
    internal NativeFocusEvent WaitRoot() => Wait(_rootEvents);
    private NativeFocusEvent Wait(ConcurrentQueue<NativeFocusEvent> events)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure) throw new InvalidOperationException("Native focus callback failed.", failure);
            if (events.TryDequeue(out var item)) return item;
            Thread.Sleep(5);
        }
        throw new TimeoutException("Expected native focus event was not delivered.");
    }
}
