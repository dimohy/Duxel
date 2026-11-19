using System.Runtime.InteropServices;

// Reuses the pinned UIA COM slots and ownership helpers of the independent client.

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckSkeleton(nint automation, nint root, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
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
        void Wait(Func<bool> condition, string name)
        {
            var end = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= end)
                    throw new TimeoutException(name);
                Thread.Sleep(5);
            }
        }
        nint Required(string name)
        {
            nint value = 0;
            Wait(() => (value = Find(name)) != 0, "Skeleton UIA object: " + name);
            return value;
        }
        void Invoke(nint item)
        {
            var action = Pattern(item, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(action, 3))(action));
            }
            finally { Marshal.Release(action); }
        }
        for (var round = 0; round < 2; round++)
        {
            var staticText = Required("Static native text 정세일");
            var staticIdentity = Text(staticText, 30011);
            check(ControlType(staticText) == 50020 && !Boolean(staticText, 30009) && !Boolean(staticText, 30043),
                "Native UIA standalone Korean text has Text type and no focus/value pattern: " + round);
            var skeleton = Required("Native Skeleton");
            check(ControlType(skeleton) == 50012 && Text(skeleton, 30102).Contains("busy=true", StringComparison.Ordinal),
                "Native UIA Skeleton exposes progress type and busy ARIA property: " + round);
            check(!Boolean(skeleton, 30009) && !Boolean(skeleton, 30008) && !Boolean(skeleton, 30033),
                "Native UIA Skeleton is nonfocusable and has no numeric range: " + round);
            Invoke(found["Complete Skeleton"]);
            Wait(() => Find("Native Skeleton") == 0, "Completed Skeleton retires from UIA");
            var updatedText = Required("Updated standalone text 부산");
            check(Text(updatedText, 30011) == staticIdentity && Find("Static native text 정세일") == 0,
                "Native UIA standalone text updates without replacing its identity: " + round);
            var content = Required("Skeleton content ready");
            check(Text(content, 30045) == "정세일" && !Text(content, 30102).Contains("busy=true", StringComparison.Ordinal),
                "Completed native UIA data is readable and not busy: " + round);
            if (round == 0)
                Invoke(found["Reload Skeleton"]);
        }
        Invoke(found["Finish Skeleton fixture"]);
    }
}
