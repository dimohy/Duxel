using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckRelationshipEvents(nint automation, nint root, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        var source = found["Relationship source"];
        var oldTarget = found["Old target"];
        ReadRelationshipChanges(automation, root, owned, new[] { 30018, 30104 }, sink =>
        {
            for (var phase = 1; phase <= 3; phase++)
            {
                Invoke(found["Advance relation"]);
                var first = WaitRelationship(sink, owned);
                var second = WaitRelationship(sink, owned);
                var events = new[] { first, second };
                var expectedId = phase == 2 ? null : "new";
                check(events.Select(x => x.Property).ToHashSet().SetEquals(new[] { 30018, 30104 })
                    && events.All(x => x.Source == "source" && x.TargetId == expectedId),
                    "external relationship payloads match both committed properties and the exact target : phase " + phase);
                if (phase != 2)
                {
                    var controlled = events.Single(x => x.Property == 30104);
                    check(controlled.TargetName == "New target" && Text(controlled.Element, 30011) == "new",
                        "external relationship payload refers to the actual live new element : phase " + phase);
                    Invoke(controlled.Element);
                }
                else
                {
                    check(events.All(x => x.Element == 0) && NoControlledPanel(source, owned),
                        "external cleared label is null and cleared controller array is empty");
                }
                check(TooltipElementUnavailable(oldTarget), "removed old target stays unavailable after relationship publication : phase " + phase);
            }
            var currentLabel = RelatedProperty(source, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
            check(Text(currentLabel, 30011) == "new" && !NoControlledPanel(source, owned),
                "external current relationship properties match the final received targets");
            Invoke(found["Advance relation"]);
            var deadline = Environment.TickCount64 + 5000;
            while (!TooltipElementUnavailable(source) && Environment.TickCount64 < deadline)
                Thread.Sleep(10);
            check(TooltipElementUnavailable(source), "the asynchronously requested source removal reaches committed publication");
            check(sink.Count == 6, "removing the source emits no dead-source property event or idle duplicates");
        });
        Invoke(found["Finish"]);
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            owned.Add(pattern);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
        }
    }
    private static NativeRelationshipProperty WaitRelationship(RelationshipPropertySink sink, List<nint> owned)
    {
        var result = sink.Wait();
        if (result.Element != 0) owned.Add(result.Element);
        return result;
    }
    private static void ReadRelationshipChanges(nint automation, nint root, List<nint> owned,
        ReadOnlySpan<int> properties, Action<RelationshipPropertySink> read)
    {
        nint cache = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 20))(automation, &cache));
        owned.Add(cache);
        Ok(((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(cache, 3))(cache, 30011));
        var sink = new RelationshipPropertySink();
        var handler = (nint)ComInterfaceMarshaller<ITooltipPropertyHandler>.ConvertToUnmanaged(sink);
        var registered = false;
        try
        {
            fixed (int* ids = properties)
                Ok(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint, int*, int, int>)Slot(automation, 34))(
                    automation, root, 4, cache, handler, ids, properties.Length));
            registered = true;
            read(sink);
        }
        finally
        {
            try
            {
                if (registered)
                    Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(automation, 36))(automation, root, handler));
            }
            finally
            {
                Marshal.Release(handler);
                sink.ReleaseQueuedElements();
                GC.KeepAlive(sink);
            }
        }
    }
}

internal readonly record struct NativeRelationshipProperty(int Property, string Source, string? TargetId, string? TargetName, nint Element);

[GeneratedComClass]
internal sealed unsafe partial class RelationshipPropertySink : ITooltipPropertyHandler
{
    private readonly ConcurrentQueue<NativeRelationshipProperty> _events = new();
    private Exception? _failure;
    private int _count;
    internal int Count => Volatile.Read(ref _count);
    public int HandlePropertyChangedEvent(nint sender, int property, UiaVariant value)
    {
        nint element = 0;
        try
        {
            if (value.Type != 13)
                throw new InvalidOperationException($"Unexpected relationship VARIANT : {property} / {value.Type}");
            if (property == 30018)
            {
                if (value.Data.Pointer != 0)
                {
                    var iid = new Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e");
                    Marshal.ThrowExceptionForHR(Marshal.QueryInterface(value.Data.Pointer, in iid, out element));
                }
            }
            else if (property == 30104)
            {
                var iid = new Guid("14314595-b4bc-4055-95f2-58f2e42c9855");
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(value.Data.Pointer, in iid, out var array));
                try
                {
                    int count = 0;
                    Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int*, int>)(*(nint**)array)[3])(array, &count));
                    if (count is < 0 or > 1) throw new InvalidOperationException("Unexpected relationship target count.");
                    if (count == 1)
                        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)(*(nint**)array)[4])(array, 0, &element));
                }
                finally { Marshal.Release(array); }
            }
            else throw new InvalidOperationException("Unexpected relationship property.");
            var id = element == 0 ? null : ReadText(element, 30011);
            var name = element == 0 ? null : ReadText(element, 30005);
            var source = TooltipEventSink.CachedText(sender, 30011);
            Interlocked.Increment(ref _count);
            _events.Enqueue(new(property, source, id, name, element));
            element = 0;
            Console.WriteLine($"NativeRelationship : {property} / {source} / {id}");
            return 0;
        }
        catch (Exception failure)
        {
            Interlocked.CompareExchange(ref _failure, failure, null);
            return Marshal.GetHRForException(failure);
        }
        finally { if (element != 0) Marshal.Release(element); }
    }
    private static string ReadText(nint element, int property)
    {
        UiaVariant value = default;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)(*(nint**)element)[10])(element, property, &value));
        try
        {
            if (value.Type != 8) throw new InvalidOperationException("Relationship element text is unavailable.");
            return value.Data.Pointer == 0 ? string.Empty : Marshal.PtrToStringBSTR(value.Data.Pointer);
        }
        finally { Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref value)); }
    }
    internal NativeRelationshipProperty Wait()
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure) throw new InvalidOperationException("Native relationship callback failed.", failure);
            if (_events.TryDequeue(out var result)) return result;
            Thread.Sleep(10);
        }
        throw new TimeoutException("The native relationship property event was not delivered.");
    }
    internal void ReleaseQueuedElements()
    {
        while (_events.TryDequeue(out var item))
            if (item.Element != 0) Marshal.Release(item.Element);
    }
}
