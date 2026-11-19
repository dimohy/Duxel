using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckPersona(nint automation, Dictionary<string, nint> found, Dictionary<string, nint> labels, List<nint> owned, Action<bool, string> check)
    {
        var persona = found["Native Persona, Developer, 사용 가능"];
        var status = labels["Persona view : avatar"];
        var identity = Text(persona, 30011);
        check(ControlType(persona) == 50026 && Text(persona, 30012) == "Duxel.Group" && Text(persona, 30101) == "group",
            "external Persona exposes actual native Group semantics");
        check(Boolean(persona, 30016) && Boolean(persona, 30017) && !Boolean(persona, 30009)
            && !Boolean(persona, 30031) && !Boolean(persona, 30041) && !Boolean(persona, 30043),
            "external Persona is passive information without invented input patterns");
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        static void WaitUntil(Func<bool> condition)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Persona native state did not commit");
                Thread.Sleep(5);
            }
        }
        ReadPropertyChanges(automation, persona, 1, owned, [30005], sink =>
        {
            Invoke("Rename persona");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "정##세일, 개발자, 사용 가능"),
                "external Persona emits committed Korean/literal-hash person information");
        });
        ReadPropertyChanges(automation, persona, 1, owned, [30005], sink =>
        {
            Invoke("Persona activity");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "정##세일, 개발자, 사용 가능, 활동 중"),
                "external Persona announces localized displayed avatar activity");
        });
        Invoke("Persona presence only");
        WaitUntil(() => Text(status, 30005) == "Persona view : presence");
        check(Text(persona, 30011) == identity && Text(persona, 30005) == "정##세일, 개발자, 사용 가능",
            "external Persona media-mode changes preserve provider identity and complete information");
        ReadPropertyChanges(automation, persona, 1, owned, [30005], sink =>
        {
            Invoke("Hide persona media");
            check(sink.Wait() == new TooltipNativeProperty(30005, identity, "정##세일, 개발자"),
                "external Persona hiding media removes the unshown presence from its accessible name");
        });
        Invoke("Disable persona");
        WaitUntil(() => !Boolean(persona, 30010));
        check(!Boolean(persona, 30009), "external disabled Persona remains nonfocusable");
        Invoke("Enable persona");
        WaitUntil(() => Boolean(persona, 30010));
        check(Text(persona, 30011) == identity, "external re-enabled Persona retains the same provider");
        Invoke("Hide persona");
        WaitUntil(() => TooltipElementUnavailable(persona));
        check(TooltipElementUnavailable(persona), "external omitted Persona retires its provider");
        Invoke("Finish");
    }
}
