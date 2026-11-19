using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckPopupInput(nint automation, nint window, nint root, Dictionary<string, nint> found, Dictionary<string, nint> labels, List<nint> owned, Action<bool, string> check)
    {
        static void Wait(Func<bool> condition)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("Popup input native transition did not commit");
                Thread.Sleep(5);
            }
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
            for (var i = 0; i < count; i++)
            {
                nint element = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                owned.Add(element);
                if (Text(element, 30005) == name)
                    return element;
            }
            return 0;
        }
        void Invoke(nint element)
        {
            var p = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(p, 3))(p));
            }
            finally { Marshal.Release(p); }
        }
        void Key(uint key)
        {
            if (LabelPostMessage(window, 0x100, key, 1) == 0 || LabelPostMessage(window, 0x101, key, unchecked((nint)0xc0000001)) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned popup input key post failed");
        }
        var outside = found["팝업 바깥 명령"];
        var status = labels["팝업 안 실행 : 0 / 바깥 실행 : 0"];
        Invoke(found["입력 팝업 열기"]);
        nint close = 0;
        Wait(() => (close = Find("입력 팝업 닫기")) != 0);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(close, 3))(close));
        Wait(() => Boolean(close, 30008));
        Key(0x09);
        Wait(() => Boolean(outside, 30008));
        check(Boolean(outside, 30010) && Boolean(outside, 30008) && Find("입력 팝업 닫기") != 0,
            "external nonmodal popup Windows Tab reaches background without closing popup");
        nint first = 0;
        Wait(() => (first = Find("팝업 안 명령")) != 0);
        Invoke(first);
        Wait(() => Text(status, 30005) == "팝업 안 실행 : 1 / 바깥 실행 : 0");
        Invoke(outside);
        Wait(() => Text(status, 30005) == "팝업 안 실행 : 1 / 바깥 실행 : 1");
        check(Boolean(outside, 30010) && Find("입력 팝업 닫기") != 0, "external nonmodal popup permits real native inside/outside commands");
        Invoke(close);
        Wait(() => Find("입력 팝업 닫기") == 0);
        check(TooltipElementUnavailable(close), "external popup close retires its actual child provider");
        Invoke(found["팝업 입력 모드 전환"]);
        Invoke(found["입력 팝업 열기"]);
        Wait(() => (close = Find("입력 팝업 닫기")) != 0 && !Boolean(outside, 30010));
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(close, 3))(close));
        Wait(() => Boolean(close, 30008));
        Key(0x09);
        Wait(() => (first = Find("팝업 안 명령")) != 0 && Boolean(first, 30008));
        check(Boolean(first, 30008) && !Boolean(outside, 30010), "external exclusive popup retains native background isolation and circular Tab");
        Invoke(close);
        Wait(() => Find("입력 팝업 닫기") == 0 && Boolean(outside, 30010));
        check(Boolean(outside, 30010), "external closed exclusive popup releases native background availability");
        Invoke(found["Finish popup input"]);
    }
}
