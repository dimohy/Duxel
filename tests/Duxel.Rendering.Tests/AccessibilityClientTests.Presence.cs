using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckPresence(nint automation, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var badge = found["available"];
        var identity = Text(badge, 30011);
        check(ControlType(badge) == 50006 && Text(badge, 30012) == "Duxel.Image", "external presence badge is a named native Image");
        check(!Boolean(badge, 30009) && !Boolean(badge, 30031) && !Boolean(badge, 30043),
            "external presence badge exposes no keyboard stop Invoke or Value patterns");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        foreach (var (button, expected) in new[]
        {
            ("Change presence", "away"), ("Set out of office", "away out of office"),
            ("Localize presence", "자리 비움 / 외근")
        })
        {
            ReadPropertyChanges(automation, badge, 1, owned, [30005], sink =>
            {
                Invoke(button);
                check(sink.Wait() == new TooltipNativeProperty(30005, identity, expected),
                    "external presence badge emits the committed Name event : " + expected);
                check(Text(badge, 30005) == expected && Text(badge, 30011) == identity,
                    "external presence badge keeps provider identity after its actual model update : " + expected);
            });
        }
        Invoke("Hide presence");
        var deadline = Environment.TickCount64 + 5000;
        while (!TooltipElementUnavailable(badge))
        {
            if (Environment.TickCount64 >= deadline) throw new TimeoutException("Presence provider did not retire.");
            Thread.Sleep(10);
        }
        check(TooltipElementUnavailable(badge), "external presence badge omission invalidates its former provider");
        Invoke("Finish");
    }
}
