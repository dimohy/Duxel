using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckPopover(nint automation, nint window, nint root, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        void Wait(Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? expression = null)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= limit)
                {
                    Console.WriteLine($"POPOVER_WAIT_TIMEOUT tick={Environment.TickCount64} condition={expression}");
                    var currentEditor = Find("이름", "Duxel.Input");
                    Console.WriteLine($"POPOVER_WAIT_STATE editor={currentEditor != 0} triggerFocus={Boolean(found["프로필 편집 열기"], 30008)}");
                    throw new TimeoutException("Popover native transition did not commit");
                }
                Thread.Sleep(5);
            }
        }
        nint Find(string name, string? className = null)
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
                nint e = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &e));
                owned.Add(e);
                if (Text(e, 30005) == name && (className is null || Text(e, 30012) == className))
                    return e;
            }
            return 0;
        }
        void Invoke(nint e)
        {
            var p = Pattern(e, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(p, 3))(p));
            }
            finally { Marshal.Release(p); }
        }
        void Key(uint key)
        {
            Console.WriteLine($"POPOVER_CLIENT_KEY tick={Environment.TickCount64} key={key:X}");
            if (LabelPostMessage(window, 0x100, key, 1) == 0 || LabelPostMessage(window, 0x101, key, unchecked((nint)0xc0000001)) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned Popover key post failed");
        }
        Invoke(found["팝업 화살표"]);
        var trigger = found["프로필 편집 열기"];
        var expansion = Pattern(trigger, 10005);
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expansion, 3))(expansion));
        }
        finally { Marshal.Release(expansion); }
        nint editor = 0, surface = 0;
        Wait(() => (editor = Find("이름", "Duxel.Input")) != 0 && Boolean(editor, 30008));
        Wait(() => (surface = Find("프로필 편집 열기", "Duxel.Group")) != 0);
        check(Text(surface, 30101) == "group" && !Boolean(surface, 30009), "external default Popover publishes passive group and real child autofocus");
        var initialValue = Text(editor, 30045);
        check(initialValue == "정세일", "external Popover editor retains actual Korean bound value : " + initialValue);
        var value = Pattern(editor, 10002);
        var text = Marshal.StringToBSTR("한글 Popover");
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, text));
        }
        finally { Marshal.FreeBSTR(text); Marshal.Release(value); }
        Wait(() => Text(editor, 30045) == "한글 Popover");
        check(Text(editor, 30045) == "한글 Popover", "external Popover child Value writes actual model");
        var close = Find("프로필 편집 닫기");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(close, 3))(close));
        Wait(() => Boolean(close, 30008));
        Key(0x09);
        Wait(() => Boolean(found["팝업 포커스 제한"], 30008));
        check(Find("이름", "Duxel.Input") != 0 && Boolean(found["팝업 포커스 제한"], 30008), "external nonmodal Popover actual Windows Tab reaches background");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(editor, 3))(editor));
        Wait(() => Boolean(editor, 30008));
        Key(0x1b);
        Wait(() => Find("이름", "Duxel.Input") == 0 && Boolean(trigger, 30008));
        check(TooltipElementUnavailable(editor) && Boolean(trigger, 30008), "external Popover Escape retires child and restores real trigger focus");
        Invoke(found["팝업 포커스 제한"]);
        Invoke(trigger);
        Wait(() => (surface = Find("프로필 편집 열기", "Duxel.Dialog")) != 0);
        Wait(() => (editor = Find("이름", "Duxel.Input")) != 0 && Boolean(editor, 30008));
        check(ControlType(surface) == 50033 && Text(surface, 30101) == "dialog" && Text(surface, 30102).Contains("modal=true", StringComparison.Ordinal), "external trapped Popover maps dialog to Pane and modal ARIA properties");
        check(!Boolean(found["팝업 포커스 제한"], 30010) && Text(editor, 30045) == "한글 Popover", "external trapped Popover isolates background and retains edited model");
        close = Find("프로필 편집 닫기");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(close, 3))(close));
        Wait(() => Boolean(close, 30008));
        Key(0x09);
        Wait(() => Boolean(editor, 30008));
        check(Boolean(editor, 30008), "external trapped Popover actual Windows Tab wraps to editor");
        Invoke(close);
        Wait(() => Find("이름", "Duxel.Input") == 0 && Boolean(found["팝업 포커스 제한"], 30010));
        check(Boolean(found["팝업 포커스 제한"], 30010), "external closed Popover releases actual background availability");
        Invoke(found["팝업 열기 방식"]);
        var center = ClientCenter(window, trigger);
        Move(center.X, center.Y);
        Wait(() => (editor = Find("이름", "Duxel.Input")) != 0);
        check(!Boolean(editor, 30008), "external Hover demo opens on actual Windows mouse movement without stealing editor focus");
        Move(0, 0);
        Wait(() => Find("이름", "Duxel.Input") == 0);
        check(TooltipElementUnavailable(editor), "external Hover delayed leave retires its real native editor provider");
        Invoke(found["팝업 열기 방식"]);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(trigger, 3))(trigger));
        Wait(() => Boolean(trigger, 30008));
        Key(0x5d);
        Wait(() => (editor = Find("이름", "Duxel.Input")) != 0 && Boolean(editor, 30008));
        check(Text(editor, 30045) == "한글 Popover", "external Windows VK_APPS maps to ContextMenu and opens retained content");
        Key(0x1b);
        Wait(() => Find("이름", "Duxel.Input") == 0 && Boolean(trigger, 30008));
        check(Boolean(trigger, 30008), "external context Escape closes and restores real trigger focus");
        var customTrigger = Find("넓은 장식 팝업 열기");
        Invoke(customTrigger);
        nint customSurface = 0, customClose = 0;
        Wait(() => (customSurface = Find("넓은 장식 팝업 열기", "Duxel.Group")) != 0
            && (customClose = Find("장식 팝업 닫기")) != 0);
        check(Text(customSurface, 30101) == "group" && Boolean(customClose, 30010),
            "external custom paint-extent Popover publishes its real group and actionable child");
        Invoke(customClose);
        Wait(() => Find("장식 팝업 닫기") == 0);
        check(TooltipElementUnavailable(customClose), "external custom paint-extent close retires its real child provider");
        Invoke(found["팝업 포커스 제한"]);
        Invoke(found["팝업 열기 방식"]);
        Invoke(Find("포커스 이탈 시 닫기"));
        Invoke(trigger);
        Wait(() => (editor = Find("이름", "Duxel.Input")) != 0 && Boolean(editor, 30008));
        close = Find("프로필 편집 닫기");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(close, 3))(close));
        Wait(() => Boolean(close, 30008));
        Key(0x09);
        Wait(() => Find("이름", "Duxel.Input") == 0 && Boolean(found["팝업 포커스 제한"], 30008));
        check(TooltipElementUnavailable(editor) && !Boolean(trigger, 30008),
            "external focus-leave Popover closes on actual Windows Tab and preserves the next background focus");
        Invoke(found["Finish Popover"]);

        void Move(int x, int y)
        {
            var position = (nint)((x & 0xffff) | ((y & 0xffff) << 16));
            if (LabelPostMessage(window, 0x0200, 0, position) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Owned Popover mouse move failed");
        }
    }
}
