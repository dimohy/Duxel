using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckAvatar(nint automation, Dictionary<string, nint> found, List<nint> owned,
        Action<bool, string> check)
    {
        var avatar = found["Native Avatar"];
        var identity = Text(avatar, 30011);
        check(ControlType(avatar) == 50006 && Text(avatar, 30012) == "Duxel.Image",
            "external Avatar is a native UIA Image control rather than a button or text alias");
        check(Boolean(avatar, 30016) && Boolean(avatar, 30017) && !Boolean(avatar, 30009),
            "external Avatar belongs to control and content views without a keyboard focus stop");
        check(!Boolean(avatar, 30031) && !Boolean(avatar, 30043),
            "external Avatar exposes neither Invoke nor editable Value patterns");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        static void WaitUntil(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= deadline) throw new TimeoutException("Avatar UIA state did not commit.");
                Thread.Sleep(10);
            }
        }
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Rename avatar");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar"),
                "external Avatar emits the committed Name change event");
            check(Text(avatar, 30005) == "Renamed Native Avatar" && Text(avatar, 30011) == identity,
                "external Avatar rename preserves the same live automation identity");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar activity");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 활동 중"),
                "external Avatar activity ring emits its localized committed Name event");
            check(Text(avatar, 30011) == identity && !Boolean(avatar, 30009),
                "activity ring preserves the same passive native image provider");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar activity");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar"),
                "external hiding of the activity ring removes activity from the committed name");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar shadow");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 활동 중")
                && Text(avatar, 30011) == identity && !Boolean(avatar, 30009),
                "external Avatar activity shadow emits its localized Name on the same passive provider");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar shadow");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar"),
                "external Avatar shadow removal commits its restored person-only name");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar presence");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 사용 가능"),
                "external Avatar presence emits its localized committed Name event");
            check(Text(avatar, 30011) == identity && !Boolean(avatar, 30009) && !Boolean(avatar, 30031),
                "presence preserves the same passive image without another Invoke provider");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Change avatar presence");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 다른 업무 중 외근"),
                "external Avatar presence mutation reports the new status on its existing provider");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar presence");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar"),
                "external Avatar presence removal removes its committed status wording");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar activity");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 활동 중"),
                "external inactive fixture begins with a committed visible activity name");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar inactive");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar"),
                "external inactive Avatar removes the hidden activity meaning in a committed Name event");
            check(Text(avatar, 30011) == identity && ControlType(avatar) == 50006
                && !Boolean(avatar, 30009) && !Boolean(avatar, 30031),
                "external inactive Avatar keeps one passive Image identity without Invoke or focus");
        });
        ReadPropertyChanges(automation, avatar, 1, owned, [30005], sink =>
        {
            Invoke("Toggle avatar inactive");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "Renamed Native Avatar 활동 중"),
                "external restored Avatar reports its retained activity on the same provider");
        });
        Invoke("Disable avatar");
        WaitUntil(() => !Boolean(avatar, 30010));
        check(!Boolean(avatar, 30009), "disabled Avatar remains nonfocusable");
        Invoke("Enable avatar");
        WaitUntil(() => Boolean(avatar, 30010));
        check(Text(avatar, 30011) == identity, "re-enabled Avatar keeps its original provider identity");
        Invoke("Hide avatar");
        WaitUntil(() => TooltipElementUnavailable(avatar));
        check(TooltipElementUnavailable(avatar), "omitted Avatar invalidates the former native provider");
        Invoke("Finish");
    }
}
