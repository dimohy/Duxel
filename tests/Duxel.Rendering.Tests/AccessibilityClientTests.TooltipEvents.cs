using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

// ABI is pinned to Microsoft win32metadata 15fc870a5e96df7c560877ff30136c0fb5a21e9b UIAutomationClient.h.
internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckTooltipEvents(nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_CLOSE_ACTION") is { } action)
        {
            CheckSingleTooltipClose(automation, root, condition, found, owned, check, action);
            return;
        }
        check(found.ContainsKey("Tooltip event target") && found.ContainsKey("Move focus")
            && found.ContainsKey("Remove target") && found.ContainsKey("Finish"),
            "external client discovers actual event fixture controls");
        ReadTooltipEvents(automation, root, owned, "Event help", sink =>
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(found["Tooltip event target"], 3))(found["Tooltip event target"]));
            var opened = sink.Wait(20000);
            check(opened.Name == "Event help" && opened.ControlType == 50022 && !opened.Focusable && opened.Id.Length != 0,
                "external opened event caches the actual ToolTip name, role, focusability and identity");
            var surface = OnlyTooltipChild(found["Tooltip event target"], condition, owned, check);
            check(Text(surface, 30005) == "Event help" && !Boolean(surface, 30017) && Boolean(surface, 30016),
                "external tooltip appears in control view and is absent from content view");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(found["Move focus"], 3))(found["Move focus"]));
            var closed = sink.Wait(20001);
            check(closed.Name == opened.Name && closed.Id == opened.Id && closed.ControlType == 50022,
                "external closed event retains cached identity after source removal");
            check(TooltipElementUnavailable(surface), "external retained tooltip element becomes unavailable after closing");
            check(sink.TargetEvents == 2, "unchanged frames do not duplicate native tooltip events");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(found["Tooltip event target"], 3))(found["Tooltip event target"]));
            var reopened = sink.Wait(20000);
            check(reopened.Id == opened.Id && reopened.Name == opened.Name, "external reopened surface preserves logical source identity");
            var remove = Pattern(found["Remove target"], 10000);
            owned.Add(remove);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(remove, 3))(remove));
            var removedClose = sink.Wait(20001);
            check(removedClose.Id == opened.Id && removedClose.ControlType == 50022,
                "external closed event survives removal of both the tooltip and its target ancestry");
            check(TooltipElementUnavailable(found["Tooltip event target"]) && sink.TargetEvents == 4,
                "external removed target is unavailable and both lifecycles deliver exactly four events");
        });
        var finish = Pattern(found["Finish"], 10000);
        owned.Add(finish);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(finish, 3))(finish));
    }

    private static void CheckSingleTooltipClose(nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check, string action)
    {
        var target = found["Tooltip event target"];
        var control = action switch
        {
            "focus" => found["Move focus"],
            "remove" => found["Remove target"],
            "dismiss" => found["Dismiss tooltip"],
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        var finish = Pattern(found["Finish"], 10000);
        owned.Add(finish);
        try
        {
            ReadTooltipEvents(automation, root, owned, "Event help", sink =>
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(target, 3))(target));
                var opened = sink.Wait(20000);
                var surface = OnlyTooltipChild(target, condition, owned, check);
                check(opened.ControlType == 50022 && !opened.Focusable && !opened.Offscreen
                    && opened.Id.Length != 0 && Text(surface, 30011) == opened.Id, "opened event identifies the live tooltip");
                if (action == "focus")
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(control, 3))(control));
                else
                {
                    var invoke = Pattern(control, 10000);
                    owned.Add(invoke);
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(invoke, 3))(invoke));
                }
                var deadline = Environment.TickCount64 + 5000;
                while (!TooltipElementUnavailable(surface) || action == "remove" && !TooltipElementUnavailable(target))
                {
                    if (Environment.TickCount64 >= deadline)
                        throw new TimeoutException("The live providers did not reach their expected removed state.");
                    Thread.Sleep(10);
                }
                check(TooltipElementUnavailable(target) == (action == "remove"),
                    "the target becomes unavailable exactly when the action removes it");
                var closed = sink.Wait(20001);
                check(closed == opened with { EventId = 20001, Offscreen = true }
                    && sink.TargetEvents == 2, "one native close reports the original identity and offscreen state");
            });
        }
        finally
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(finish, 3))(finish));
        }
    }

    private static void CheckTooltipHostVisibility(nint window, nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, string coordination, Action<bool, string> check, bool shutdown, bool requireShutdownEvent)
    {
        check(found.TryGetValue("Target", out var target) && Text(target, 30013) == "Visible help",
            "external UIA sees target HelpText before the controlled tooltip opens");
        void Signal(string marker) => File.WriteAllText(Path.Combine(coordination, marker), string.Empty);
        void WaitFor(Func<bool> predicate, string message)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= deadline)
                {
                    throw new TimeoutException(message);
                }
                Thread.Sleep(10);
            }
        }
        var eventRoot = root;
        GetWindowThreadProcessId(window, out var processId);
        if (!shutdown && Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_DESKTOP_SCOPE") == "1")
        {
            const int getRootElementSlot = 5;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, getRootElementSlot))(automation, &eventRoot));
            owned.Add(eventRoot);
        }
        Console.WriteLine($"Tooltip subscription : {(eventRoot == root ? "window" : "desktop")}, process {processId}");
        ReadAutomationEvents(automation, eventRoot, owned, "Visible help", [20000, 20001], sink =>
        {
            Signal("subscribed");
            var opened = sink.Wait(20000);
            check(opened.ControlType == 50022 && !opened.Focusable && !opened.Offscreen && opened.Id.Length != 0,
                "external UIA receives the on-screen ToolTip identity on open");
            var surface = OnlyTooltipChild(target, condition, owned, check);
            Signal("first-open");
            if (!shutdown && Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_CLOSE_ACTION") == "session-dismiss")
            {
                try
                {
                    WaitFor(() => TooltipElementUnavailable(surface), "Retained tooltip did not become unavailable after dismissal.");
                    check(Text(target, 30013) == opened.Name, "ordinary dismissal preserves target HelpText while the session runs");
                    var closed = sink.Wait(20001);
                    check(closed == opened with { EventId = 20001, Offscreen = true } && sink.TargetEvents == 2,
                        "the application session delivers exactly one close with cached identity and offscreen state");
                }
                finally
                {
                    Signal("dismissed");
                }
                return;
            }
            if (shutdown)
            {
                if (requireShutdownEvent)
                {
                    var closed = sink.Wait(20001);
                    check(closed with { EventId = 20000, Offscreen = false } == opened && closed.Offscreen,
                        "window close delivers the cached identity and offscreen state for its visible tooltip");
                    Signal("shutdown-observed");
                }
                WaitFor(() => File.Exists(Path.Combine(coordination, "host-disposed")), "Host did not dispose the HWND.");
                check(TooltipElementUnavailable(surface), "closing the window invalidates its live tooltip provider");
                if (requireShutdownEvent)
                {
                    check(sink.TargetEvents == 2, "closing the window delivers exactly one external close event");
                }
                return;
            }
            var minimized = sink.Wait(20001);
            check(minimized with { EventId = 20000, Offscreen = false } == opened && minimized.Offscreen,
                "minimizing delivers a close with the cached ToolTip identity and offscreen state");
            WaitFor(() => TooltipElementUnavailable(surface), "Retained tooltip did not become unavailable on minimize.");
            check(Text(target, 30013) == opened.Name, "minimizing removes the live surface and preserves target HelpText");
            Signal("minimized");
            var restored = sink.Wait(20000);
            check(restored == opened, "restoring the HWND delivers one open for the same logical tooltip");
            var restoredSurface = OnlyTooltipChild(target, condition, owned, check);
            Signal("restored");
            WaitFor(() => TooltipElementUnavailable(restoredSurface), "Retained tooltip did not become unavailable on hide.");
            check(TooltipElementUnavailable(restoredSurface), "hiding removes the live tooltip provider");
            var hidden = sink.Wait(20001);
            check(hidden == minimized, "hiding the HWND delivers the same cached close properties");
            Signal("hidden");
            WaitFor(() => File.Exists(Path.Combine(coordination, "host-restored")), "Host did not finish the final show.");
            check(sink.TargetEvents == 4 && Text(target, 30013) == opened.Name,
                "accepted dismissal stays closed after show with four external lifecycle events total");
        }, eventRoot == root ? null : processId);
        Signal("client-done");
    }

    private static nint OnlyTooltipChild(nint target, nint condition, List<nint> owned, Action<bool, string> check)
    {
        nint children = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(target, 6))(target, 2, condition, &children));
        owned.Add(children);
        var count = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(children, 3))(children, &count));
        check(count == 1, "external target owns exactly one live tooltip child");
        nint surface = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(children, 4))(children, 0, &surface));
        owned.Add(surface);
        return surface;
    }

    private static void CheckTooltipProperties(nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        var target = found["Tooltip event target"];
        var update = Pattern(found["Change tooltip text"], 10000);
        var finish = Pattern(found["Finish"], 10000);
        owned.Add(update);
        owned.Add(finish);
        ReadTooltipEvents(automation, root, owned, "Event help", lifecycle =>
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(target, 3))(target));
            var opened = lifecycle.Wait(20000);
            var surface = OnlyTooltipChild(target, condition, owned, check);
            var targetId = Text(target, 30011);
            check(Text(target, 30013) == "Event help" && Text(surface, 30005) == "Event help" && opened.Id == Text(surface, 30011),
                "external UIA reads the original HelpText and visible Name before subscribing");

            ReadPropertyChanges(automation, target, 5, owned, [30005, 30013], sink =>
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(update, 3))(update));
                var first = sink.Wait();
                var second = sink.Wait();
                var received = new[] { first, second };
                check(received.Contains(new TooltipNativeProperty(30013, targetId, "Updated event help")),
                    "external UIA receives changed HelpText from the retained target");
                check(received.Contains(new TooltipNativeProperty(30005, opened.Id, "Updated event help")),
                    "external UIA receives changed Name from the same visible tooltip");
                check(Text(target, 30013) == "Updated event help" && Text(surface, 30005) == "Updated event help",
                    "current providers return the same values as the property change payloads");
            });
        });
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(finish, 3))(finish));
    }

    private static void ReadPropertyChanges(nint automation, nint element, int scope, List<nint> owned,
        ReadOnlySpan<int> properties, Action<TooltipPropertySink> read)
    {
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, 30011));
        var sink = new TooltipPropertySink();
        var handler = (nint)ComInterfaceMarshaller<ITooltipPropertyHandler>.ConvertToUnmanaged(sink);
        var registered = false;
        try
        {
            fixed (int* propertyIds = properties)
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint, int*, int, int>)Slot(automation, 34))(
                    automation, element, scope, cache, handler, propertyIds, properties.Length));
            }
            registered = true;
            read(sink);
        }
        finally
        {
            if (registered)
                Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(automation, 36))(automation, element, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
    }

    private static bool TooltipElementUnavailable(nint element)
    {
        UiaVariant value = default;
        var hr = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30005, &value);
        Ok(UiaNative.VariantClear(ref value));
        return hr == UiaNative.Unavailable;
    }

    private static void CheckTooltipScroll(nint automation, nint root, nint condition, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        var target = found["Tooltip event target"];
        ReadTooltipEvents(automation, root, owned, "Event help", sink =>
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(target, 3))(target));
            var opened = sink.Wait(20000);
            var surface = OnlyTooltipChild(target, condition, owned, check);
            var scroll = Pattern(surface, 10004);
            owned.Add(scroll);
            double ReadPercent(int slot)
            {
                double value = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(scroll, slot))(scroll, &value));
                return value;
            }
            check(ReadPercent(5) == 0 && ReadPercent(6) == 0 && ReadPercent(7) == 50 && ReadPercent(8) == 25
                && Boolean(surface, 30057) && Boolean(surface, 30058),
                "external Scroll pattern exposes both current positions and measured view sizes");
            ReadPropertyChanges(automation, surface, 1, owned, [30034, 30053, 30054, 30055, 30056, 30057, 30058], sink =>
            {
                void CheckChanges(string name, params (int Property, object Value)[] expected)
                {
                    var changes = expected.Select(_ => sink.Wait()).ToHashSet();
                    check(changes.SetEquals(expected.Select(value => new TooltipNativeProperty(value.Property, opened.Id, value.Value))), name);
                }

                Ok(((delegate* unmanaged[Stdcall]<nint, double, double, int>)Slot(scroll, 4))(scroll, 50, 75));
                check(ReadPercent(5) == 50 && ReadPercent(6) == 75,
                    "external SetScrollPercent moves both axes before returning");
                CheckChanges("external UIA receives both changed percentages from the same tooltip", (30053, 50d), (30055, 75d));

                Ok(((delegate* unmanaged[Stdcall]<nint, double, double, int>)Slot(scroll, 4))(scroll, 50, 75));
                Ok(((delegate* unmanaged[Stdcall]<nint, int, int, int>)Slot(scroll, 3))(scroll, 4, 0));
                var horizontal = ReadPercent(5);
                var vertical = ReadPercent(6);
                check(Math.Abs(horizontal - 76d / 120 * 100) < .001 && Math.Abs(vertical - 35d / 84 * 100) < .001,
                    "external Scroll uses the real small step and large viewport step");
                CheckChanges("external UIA suppresses the unchanged request and then reports the step", (30053, horizontal), (30055, vertical));
                var invalid = ((delegate* unmanaged[Stdcall]<nint, double, double, int>)Slot(scroll, 4))(scroll, double.NaN, -1);
                check(invalid < 0, "external Scroll rejects invalid percentages");

                var shrink = Pattern(found["Change tooltip text"], 10000);
                owned.Add(shrink);
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(shrink, 3))(shrink));
                CheckChanges("the retained tooltip reports fit for both axes and withdraws Scroll availability",
                    (30034, false), (30053, -1d), (30054, 100d), (30055, -1d), (30056, 100d), (30057, false), (30058, false));
                nint removed = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(surface, 16))(surface, 10004, &removed));
                if (removed != 0)
                    owned.Add(removed);
                check(Text(surface, 30005) == "Fit" && Text(surface, 30011) == opened.Id && removed == 0 && !Boolean(surface, 30034),
                    "the retained tooltip drops its Scroll pattern when its content fits");
            });
        });
        var finish = Pattern(found["Finish"], 10000);
        owned.Add(finish);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(finish, 3))(finish));
    }

    private static void ReadTooltipEvents(nint automation, nint root, List<nint> owned, string text, Action<TooltipEventSink> read)
        => ReadPopupEvents(automation, root, owned, text, 20000, 20001, read);

    private static void ReadPopupEvents(nint automation, nint root, List<nint> owned, string text,
        int openedEvent, int closedEvent, Action<TooltipEventSink> read)
        => ReadAutomationEvents(automation, root, owned, text, [openedEvent, closedEvent], read);

    private static void ReadAutomationEvents(nint automation, nint root, List<nint> owned, string text,
        int[] eventIds, Action<TooltipEventSink> read, uint? processId = null)
    {
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        foreach (var property in new[] { 30003, 30005, 30009, 30011, 30022 })
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, property));
        }
        if (processId.HasValue)
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, 30002));
        var sink = new TooltipEventSink(text, processId);
        var handler = (nint)ComInterfaceMarshaller<ITooltipEventHandler>.ConvertToUnmanaged(sink);
        var registered = new List<int>();
        try
        {
            foreach (var eventId in eventIds)
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, int, nint, nint, int>)Slot(automation, 32))(
                    automation, eventId, root, 4, cache, handler));
                registered.Add(eventId);
            }
            read(sink);
        }
        finally
        {
            for (var i = registered.Count - 1; i >= 0; i--)
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint, int>)Slot(automation, 33))(automation, registered[i], root, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
    }
}

[GeneratedComInterface]
[Guid("146c3c17-f12e-4e22-8c27-f894b9b79c69")]
internal partial interface ITooltipEventHandler
{
    [PreserveSig]
    int HandleAutomationEvent(nint sender, int eventId);
}

internal readonly record struct TooltipNativeEvent(int EventId, string Name, int ControlType, bool Focusable, string Id, bool Offscreen);

[GeneratedComClass]
internal sealed unsafe partial class TooltipEventSink(string text, uint? processId) : ITooltipEventHandler
{
    private readonly ConcurrentQueue<TooltipNativeEvent> _events = new();
    private Exception? _failure;
    private int _targetEvents;
    internal int TargetEvents => Volatile.Read(ref _targetEvents);

    public int HandleAutomationEvent(nint sender, int eventId)
    {
        try
        {
            var name = CachedText(sender, 30005);
            if (name != text)
            {
                return 0;
            }
            if (processId is { } expectedProcess)
            {
                var process = Cached(sender, 30002);
                if (process.Type != 3)
                    throw new InvalidOperationException("The desktop tooltip event has no cached process ID.");
                if (process.Data.Integer != expectedProcess)
                    return 0;
            }
            Console.WriteLine($"NativeTooltipEvent : id = {eventId}, name = {name}");
            var role = Cached(sender, 30003);
            var focus = Cached(sender, 30009);
            var id = CachedText(sender, 30011);
            var offscreen = Cached(sender, 30022);
            if (role.Type != 3 || focus.Type != 11 || offscreen.Type != 11)
            {
                throw new InvalidOperationException("The cached tooltip event has invalid property types.");
            }
            Interlocked.Increment(ref _targetEvents);
            _events.Enqueue(new(eventId, name, role.Data.Integer, focus.Data.Boolean != 0, id, offscreen.Data.Boolean != 0));
            return 0;
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref _failure, error, null);
            return Marshal.GetHRForException(error);
        }
    }

    internal TooltipNativeEvent Wait(int eventId)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure)
            {
                throw new InvalidOperationException("The native tooltip event callback failed.", failure);
            }
            if (_events.TryDequeue(out var result))
            {
                if (result.EventId != eventId)
                {
                    throw new InvalidOperationException("Unexpected tooltip event order.");
                }
                return result;
            }
            Thread.Sleep(10);
        }
        throw new TimeoutException("The native tooltip event was not delivered.");
    }

    private static UiaVariant Cached(nint sender, int property)
    {
        var slot = (*(nint**)sender)[12];
        UiaVariant value = default;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)slot)(sender, property, &value));
        return value;
    }

    internal static string CachedText(nint sender, int property)
    {
        var value = Cached(sender, property);
        try
        {
            if (value.Type != 8)
            {
                throw new InvalidOperationException("The cached tooltip text is not a BSTR.");
            }
            return value.Data.Pointer == 0 ? string.Empty : Marshal.PtrToStringBSTR(value.Data.Pointer);
        }
        finally
        {
            Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref value));
        }
    }
}

[GeneratedComInterface]
[Guid("40cd37d4-c756-4b0c-8c6f-bddfeeb13b50")]
internal partial interface ITooltipPropertyHandler
{
    [PreserveSig]
    int HandlePropertyChangedEvent(nint sender, int propertyId, UiaVariant newValue);
}

internal readonly record struct TooltipNativeProperty(int PropertyId, string SourceId, object Value);

[GeneratedComClass]
internal sealed partial class TooltipPropertySink : ITooltipPropertyHandler
{
    private readonly ConcurrentQueue<TooltipNativeProperty> _events = new();
    private Exception? _failure;

    public int HandlePropertyChangedEvent(nint sender, int propertyId, UiaVariant newValue)
    {
        try
        {
            // UI Automation owns the callback's VARIANT and its BSTR.
            object value = newValue.Type switch
            {
                3 => newValue.Data.Integer,
                5 => newValue.Data.Real,
                8 => Marshal.PtrToStringBSTR(newValue.Data.Pointer),
                11 => newValue.Data.Boolean != 0,
                _ => throw new InvalidOperationException($"Unexpected property VARIANT type : {newValue.Type}")
            };
            var source = TooltipEventSink.CachedText(sender, 30011);
            Console.WriteLine($"NativeTooltipProperty : id = {propertyId}, value = {value}");
            _events.Enqueue(new(propertyId, source, value));
            return 0;
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            return Marshal.GetHRForException(exception);
        }
    }

    internal TooltipNativeProperty Wait()
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure)
            {
                throw new InvalidOperationException("The native tooltip property callback failed.", failure);
            }
            if (_events.TryDequeue(out var result))
            {
                return result;
            }
            Thread.Sleep(10);
        }
        throw new TimeoutException("The native tooltip property event was not delivered.");
    }
}
