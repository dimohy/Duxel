using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckAnnouncements(nint window, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var clsid = new Guid("e22ad333-b25f-460c-83d0-0581107395c9"); // CUIAutomation8
        var iid = new Guid("25f700c8-d816-4057-a9dc-3cbdee77e256"); // IUIAutomation5
        nint automation = 0, root = 0, handler = 0;
        var subscribed = false;
        var sink = new AnnouncementNotificationSink();
        var automatic = found.ContainsKey("Mount message");
        void Invoke(string name)
        {
            var action = Pattern(found[name], 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(action, 3))(action));
            }
            finally { Marshal.Release(action); }
        }
        try
        {
            Ok(CoCreateInstance(in clsid, 0, 1, in iid, out automation));
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Slot(automation, 6))(automation, window, &root));
            handler = (nint)ComInterfaceMarshaller<IAnnouncementNotificationHandler>.ConvertToUnmanaged(sink);
            // Slots68/69 are pinned from the complete official IUIAutomation5Vtbl.
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint, int>)Slot(automation, 68))(automation, root, 1, 0, handler));
            subscribed = true;
            if (automatic)
                CheckMessageBarAnnouncements(automation, root, found, owned, sink, check);
            else
            {
                Invoke("Send polite announcement");
                var polite = sink.Wait();
                check(polite == new NativeAnnouncementNotification(4, 2, NativeAnnouncementScene.PoliteMessage, "owned-polite"),
                    "Native UIA notification preserves Korean/emoji BSTR,polite processing and activity identity");
                Invoke("Send assertive announcement");
                var urgent = sink.Wait();
                check(urgent == new NativeAnnouncementNotification(4, 0, NativeAnnouncementScene.AssertiveMessage, "owned-assertive"),
                    "Native UIA urgent announcement uses ImportantAll and the actual Korean payload");
                Invoke("Repeat announcement");
                var first = sink.Wait();
                var second = sink.Wait();
                check(first == new NativeAnnouncementNotification(4, 2, NativeAnnouncementScene.RepeatMessage, "owned-repeat") && second == first,
                    "Two explicit equal announcements deliver two actual native callbacks");
                Invoke("No announcement frame");
                var deadline = Environment.TickCount64 + 5000;
                while (!HasQuietMarker(automation, root))
                {
                    if (Environment.TickCount64 >= deadline)
                        throw new TimeoutException("The announcement fixture did not publish its quiet-frame marker.");
                    Thread.Sleep(5);
                }
                check(sink.Count == 4, "At least eight unchanged native fixture frames emit no duplicate announcement");
            }

            static bool HasQuietMarker(nint scopedAutomation, nint scopedRoot)
            {
                nint condition = 0, elements = 0;
                try
                {
                    Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(scopedAutomation, 21))(scopedAutomation, &condition));
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(scopedRoot, 6))(scopedRoot, 4, condition, &elements));
                    var count = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
                    for (var index = 0; index < count; index++)
                    {
                        nint element = 0;
                        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, index, &element));
                        try
                        {
                            if (Text(element, 30005) == "Quiet frames : ready")
                                return true;
                        }
                        finally { Marshal.Release(element); }
                    }
                    return false;
                }
                finally
                {
                    if (elements != 0)
                        Marshal.Release(elements);
                    if (condition != 0)
                        Marshal.Release(condition);
                }
            }
        }
        finally
        {
            try
            {
                if (subscribed)
                    Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(automation, 69))(automation, root, handler));
            }
            finally
            {
                if (handler != 0)
                    Marshal.Release(handler);
                if (root != 0)
                    Marshal.Release(root);
                if (automation != 0)
                    Marshal.Release(automation);
                GC.KeepAlive(sink);
            }
        }
        Invoke(automatic ? "Finish message bars" : "Finish announcements");
    }

    private static void CheckMessageBarAnnouncements(nint automation, nint root, Dictionary<string, nint> found,
        List<nint> owned, AnnouncementNotificationSink sink, Action<bool, string> check)
    {
        void Invoke(string name)
        {
            var action = Pattern(found[name], 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(action, 3))(action));
            }
            finally { Marshal.Release(action); }
        }
        nint Find(string name)
        {
            nint condition = 0, elements = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
            owned.Add(condition);
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(root, 6))(root, 4, condition, &elements));
            owned.Add(elements);
            var count = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
            for (var index = 0; index < count; index++)
            {
                nint item = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, index, &item));
                owned.Add(item);
                if (Text(item, 30005) == name)
                    return item;
            }
            return 0;
        }
        nint Required(string name)
        {
            var deadline = Environment.TickCount64 + 5000;
            nint item;
            while ((item = Find(name)) == 0)
            {
                if (Environment.TickCount64 >= deadline)
                    throw new TimeoutException("MessageBar native marker : " + name);
                Thread.Sleep(5);
            }
            return item;
        }
        Invoke("Mount message");
        var initial = sink.Wait();
        check(initial.Kind == 4 && initial.Processing == 2 && initial.Message == NativeMessageBarScene.OriginalMessage
            && initial.ActivityId.EndsWith("/mount", StringComparison.Ordinal),
            "Actual MessageBar mount delivers its prepared Korean title/body/actions and polite native notification");
        var message = Required("Native message bar");
        var title = RelatedProperty(message, 30018, new("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), owned);
        check(Text(title, 30005) == NativeMessageBarScene.Title
            && Text(title, 30011) == Text(Required(NativeMessageBarScene.Title), 30011),
            "Native MessageBar LabeledBy resolves its actual owned title provider");
        Invoke("Make message urgent");
        check(sink.Wait() == initial with
        {
            Processing = 0
        },
            "Changing MessageBar priority emits one native urgent notification under the same activity");
        Invoke("Edit mounted message");
        Required("Message quiet : ready : 1");
        check(sink.Count == 2, "Eight frames after mounted body edits do not repeat automatic native announcements");
        Invoke("Disable message announcement");
        Required("Message stage : disabled");
        check(sink.Count == 2, "Disabling automatic MessageBar announcements is committed without a replacement notification");
        Invoke("Enable message announcement");
        check(sink.Wait() == initial with
        {
            Processing = 0,
            Message = NativeMessageBarScene.UpdatedMessage
        },
            "Re-enabling MessageBar announces its updated actual content once");
        Invoke("Hide message");
        Required("Message stage : hidden");
        Invoke("Mount message");
        check(sink.Wait() == initial with
        {
            Processing = 0,
            Message = NativeMessageBarScene.UpdatedMessage
        },
            "Remounting the same owned MessageBar emits one native event with its stable activity identity");
        Invoke("Quiet message frames");
        Required("Message quiet : ready : 2");
        check(sink.Count == 4, "Stable remounted MessageBar frames do not replay native notifications");
    }
}

[GeneratedComInterface]
[Guid("c7cb2637-e6c2-4d0c-85de-4948c02175c7")]
internal partial interface IAnnouncementNotificationHandler
{
    [PreserveSig]
    int HandleNotificationEvent(nint sender, int kind, int processing, nint displayString, nint activityId);
}

internal readonly record struct NativeAnnouncementNotification(int Kind, int Processing, string Message, string ActivityId);

[GeneratedComClass]
internal sealed partial class AnnouncementNotificationSink : IAnnouncementNotificationHandler
{
    private readonly ConcurrentQueue<NativeAnnouncementNotification> _events = new();
    private Exception? _failure;
    private int _count;
    internal int Count => Volatile.Read(ref _count);

    public int HandleNotificationEvent(nint sender, int kind, int processing, nint displayString, nint activityId)
    {
        try
        {
            // UI Automation owns the callback's BSTRs; copy them without releasing them.
            var item = new NativeAnnouncementNotification(kind, processing,
                Marshal.PtrToStringBSTR(displayString), Marshal.PtrToStringBSTR(activityId));
            Console.WriteLine($"NativeAnnouncement : {item}");
            _events.Enqueue(item);
            Interlocked.Increment(ref _count);
            return 0;
        }
        catch (Exception failure)
        {
            Interlocked.CompareExchange(ref _failure, failure, null);
            return Marshal.GetHRForException(failure);
        }
    }

    internal NativeAnnouncementNotification Wait()
    {
        var deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        {
            if (Volatile.Read(ref _failure) is { } failure)
                throw new InvalidOperationException("Native announcement callback failed.", failure);
            if (_events.TryDequeue(out var item))
                return item;
            Thread.Sleep(5);
        }
        throw new TimeoutException("The actual UIA notification callback was not delivered.");
    }
}
