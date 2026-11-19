using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckTabSelection(nint automation, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        if (found.ContainsKey("Legacy event tabs"))
        {
            CheckLegacyTabSelection(automation, root, found, owned, check);
            return;
        }
        check(found.ContainsKey("Event tabs") && found.ContainsKey("Alpha") && found.ContainsKey("Beta"),
            "external UIA discovers the actual tab-list owner and child tabs");
        var alphaPanel = CheckControlledPanel(found["Alpha"], owned, check);
        check(NoControlledPanel(found["Beta"], owned), "external inactive tab has no fabricated controlled panel");
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        foreach (var property in new[] { 30003, 30005, 30011, 30079 })
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, property));
        var sink = new TabSelectionSink();
        var handler = (nint)ComInterfaceMarshaller<ITooltipEventHandler>.ConvertToUnmanaged(sink);
        var registered = new List<int>();
        try
        {
            foreach (var eventId in new[] { 20012, 20011, 20013 })
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, int, nint, nint, int>)Slot(automation, 32))(
                    automation, eventId, root, 4, cache, handler));
                registered.Add(eventId);
            }
            ReadRelationshipChanges(automation, root, owned, new[] { 30104 }, relationships =>
            ReadPropertyChanges(automation, root, 4, owned, new[] { 30079 }, properties =>
            {
                var beta = Pattern(found["Beta"], 10010);
                owned.Add(beta);
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(beta, 3))(beta));
                var selected = sink.Wait(20012);
                check(selected.Name == "Beta" && selected.Role == 50019 && selected.Selected == true,
                    "external ElementSelected event caches the actual selected tab after publication");
                var changed = new[] { properties.Wait(), properties.Wait() }.ToHashSet();
                check(changed.SetEquals(new[]
                {
                    new TooltipNativeProperty(30079, Text(found["Alpha"], 30011), false),
                    new TooltipNativeProperty(30079, Text(found["Beta"], 30011), true)
                }), "external IsSelected property events match both changed tab identities and values");
                check(Boolean(found["Beta"], 30079) && !Boolean(found["Alpha"], 30079),
                    "external current tab properties match the received selection event");
                var betaPanel = CheckControlledPanel(found["Beta"], owned, check);
                var betaPanelId = Text(betaPanel, 30011);
                var switched = new[] { WaitRelationship(relationships, owned), WaitRelationship(relationships, owned) };
                check(switched.All(x => x.Property == 30104) && switched.Select(x => (x.Source, x.TargetId)).ToHashSet().SetEquals(
                    new[] { (Text(found["Alpha"], 30011), (string?)null), (Text(found["Beta"], 30011), (string?)betaPanelId) }),
                    "actual generated tabs emit both controller changes with the selected live panel identity");
                check(TooltipElementUnavailable(alphaPanel) && NoControlledPanel(found["Alpha"], owned),
                    "external switching removes the old panel and its inactive controller relationship");
                Invoke("Clear selection");
                var removed = sink.Wait(20011);
                check(removed.Name == "Beta" && removed.Selected == false && properties.Wait().Value is false,
                    "external explicit empty selection receives a deselected property and removal event");
                check(TooltipElementUnavailable(betaPanel) && NoControlledPanel(found["Beta"], owned),
                    "external empty selection exposes no old body or controlled element");
                var cleared = WaitRelationship(relationships, owned);
                check(cleared.Source == Text(found["Beta"], 30011) && cleared.TargetId is null && cleared.Element == 0,
                    "actual tab deselection emits the empty controlled-element array");
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(beta, 3))(beta));
                check(sink.Wait(20012).Selected == true && properties.Wait().Value is true,
                    "external reselection receives one committed selected event and property value");
                var renewedPanel = CheckControlledPanel(found["Beta"], owned, check);
                check(Text(renewedPanel, 30011) == betaPanelId && TooltipElementUnavailable(betaPanel),
                    "external return preserves the panel key while the previous provider stays unavailable");
                var restored = WaitRelationship(relationships, owned);
                check(restored.Source == Text(found["Beta"], 30011) && restored.TargetId == betaPanelId && restored.TargetName == "Beta",
                    "actual tab reselection emits its recreated live panel through the property payload");
                Invoke("Remove selected");
                var invalidated = sink.Wait(20013);
                check(invalidated.Name == "Event tabs" && invalidated.Role == 50018 && invalidated.Selected is null,
                    "external removal of the selected item invalidates the surviving Tab container");
                check(TooltipElementUnavailable(found["Beta"]) && sink.Count == 4,
                    "external removed tab is unavailable and idle frames do not duplicate selection events");
                check(relationships.Count == 4, "actual tab removal adds no unavailable-source controller event or idle duplicate");
            }));
        }
        finally
        {
            foreach (var eventId in registered)
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint, int>)Slot(automation, 33))(automation, eventId, root, handler));
            Marshal.Release(handler);
            GC.KeepAlive(sink);
        }
        Invoke("Finish");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            owned.Add(pattern);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
        }
    }
}

internal readonly record struct TabNativeSelectionEvent(int EventId, string Name, int Role, bool? Selected);
[GeneratedComClass]
internal sealed unsafe partial class TabSelectionSink : ITooltipEventHandler
{
    private readonly ConcurrentQueue<TabNativeSelectionEvent> _events = new();
    private Exception? _failure;
    private int _count;
    internal int Count => Volatile.Read(ref _count);
    public int HandleAutomationEvent(nint sender, int eventId)
    {
        try
        {
            var name = TooltipEventSink.CachedText(sender, 30005);
            var role = Cached(sender, 30003);
            if (role.Type != 3) throw new InvalidOperationException("Selection event role is not an integer.");
            bool? selected = null;
            if (role.Data.Integer is 50019 or 50011 or 50013)
            {
                var state = Cached(sender, 30079);
                if (state.Type != 11) throw new InvalidOperationException("Selection event state is not Boolean.");
                selected = state.Data.Boolean != 0;
            }
            Interlocked.Increment(ref _count);
            _events.Enqueue(new(eventId, name, role.Data.Integer, selected));
            return 0;
        }
        catch (Exception failure) { Interlocked.CompareExchange(ref _failure, failure, null); return Marshal.GetHRForException(failure); }
    }
    internal TabNativeSelectionEvent Wait(int eventId)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure) throw new InvalidOperationException("Native selection callback failed.", failure);
            if (_events.TryDequeue(out var result))
            {
                if (result.EventId != eventId) throw new InvalidOperationException("Unexpected native selection event order.");
                return result;
            }
            Thread.Sleep(10);
        }
        throw new TimeoutException("Native selection event was not delivered.");
    }
    private static UiaVariant Cached(nint sender, int property)
    {
        UiaVariant value = default;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)(*(nint**)sender)[12])(sender, property, &value));
        return value;
    }
}
