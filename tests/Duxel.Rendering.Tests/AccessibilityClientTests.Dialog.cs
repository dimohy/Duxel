using System.Globalization;
using System.Runtime.InteropServices;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckDialog(nint automation, nint window, nint root, Dictionary<string, nint> found,
        List<nint> owned, Action<bool, string> check)
    {
        void Wait(Func<bool> condition, string phase)
        {
            var end = Environment.TickCount64 + 5000;
            while (!condition())
            {
                if (Environment.TickCount64 >= end)
                    throw new TimeoutException("Native Dialog UIA : " + phase);
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
                nint element = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                owned.Add(element);
                if (Text(element, 30005) == name && (className is null || Text(element, 30012) == className))
                    return element;
            }
            return 0;
        }
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        var marker = found["Native frame"];
        int Serial() => int.Parse(Text(marker, 30045), CultureInfo.InvariantCulture);
        void Post(uint message, nuint key, nint parameter)
        {
            if (LabelPostMessage(window, message, key, parameter) == 0)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }
        void Key(uint key)
        {
            var serial = Serial();
            Post(0x100, key, 1);
            Post(0x101, key, unchecked((nint)0xc0000001));
            Wait(() => Serial() >= serial + 2, "posted key committed to a published frame");
        }
        void Backdrop()
        {
            var serial = Serial();
            nint position = (4 << 16) | 4;
            Post(0x200, 0, position);
            Post(0x201, 1, position);
            Post(0x202, 0, position);
            Wait(() => Serial() >= serial + 2, "posted backdrop click committed");
        }
        nint Surface(string className)
        {
            nint value = 0;
            Wait(() => (value = Find("Dialog profile", className)) != 0, "surface published : " + className);
            return value;
        }
        nint Editor()
        {
            nint value = 0;
            Wait(() => (value = Find("Dialog name", "Duxel.Input")) != 0 && Boolean(value, 30008), "editor focused");
            return value;
        }
        var modal = found["Open modal"];
        Invoke(modal);
        var surface = Surface("Duxel.Dialog");
        var editor = Editor();
        check(ControlType(surface) == 50033 && Text(surface, 30101) == "dialog"
            && Text(surface, 30102).Contains("modal=true", StringComparison.Ordinal),
            "native UIA modal Dialog exposes Pane, dialog role and modal ARIA state");
        check(Text(surface, 30005) == "Dialog profile" && Text(surface, 30013).Contains("Profile description", StringComparison.Ordinal),
            "native UIA Dialog exposes its actual name and optional description");
        check(!Boolean(found["Open non-modal"], 30010) && Boolean(editor, 30008),
            "native modal Dialog disables background availability and focuses its editor");
        var value = Pattern(editor, 10002);
        var korean = Marshal.StringToBSTR("정세일 UIA");
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, korean));
        }
        finally { Marshal.FreeBSTR(korean); Marshal.Release(value); }
        Wait(() => Text(editor, 30045) == "정세일 UIA", "Korean value published");
        check(Text(editor, 30045) == "정세일 UIA", "native UIA Value edits the actual retained Dialog model");
        Key(0x09);
        var save = Find("Save profile");
        Wait(() => Boolean(save, 30008), "Tab enters actions");
        Key(0x09);
        var cancel = Find("Cancel profile");
        Wait(() => Boolean(cancel, 30008), "Tab follows actions");
        Key(0x09);
        Wait(() => Boolean(editor, 30008), "Tab wraps to editor");
        check(Boolean(editor, 30008), "native Windows Tab remains inside modal Dialog");
        Key(0x1b);
        Wait(() => Find("Dialog name", "Duxel.Input") == 0 && Boolean(modal, 30008), "Escape closes and restores opener");
        check(TooltipElementUnavailable(editor) && Boolean(modal, 30008),
            "native Escape retires the editor provider and restores its real opener");

        var nonmodal = found["Open non-modal"];
        Invoke(nonmodal);
        surface = Surface("Duxel.Dialog");
        var close = Find("Close dialog");
        Wait(() => close != 0 && Boolean(close, 30008), "non-modal close receives initial focus");
        check(Text(surface, 30102).Contains("modal=false", StringComparison.Ordinal) && Boolean(modal, 30010),
            "native UIA non-modal Dialog permits background interaction");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(modal, 3))(modal));
        Wait(() => Boolean(modal, 30008), "focus moved outside non-modal Dialog");
        Key(0x1b);
        check(Find("Dialog profile", "Duxel.Dialog") != 0,
            "native Escape with outside focus preserves non-modal Dialog");
        Invoke(close);
        Wait(() => Find("Dialog profile", "Duxel.Dialog") == 0 && Boolean(nonmodal, 30008), "close icon dismisses non-modal Dialog");
        check(Boolean(nonmodal, 30008), "native close icon restores the non-modal opener");

        var alert = found["Open alert"];
        Invoke(alert);
        surface = Surface("Duxel.AlertDialog");
        editor = Editor();
        check(Text(surface, 30101) == "alertdialog" && Text(surface, 30102).Contains("modal=true", StringComparison.Ordinal),
            "native UIA alert Dialog exposes distinct alertdialog and modal semantics");
        Backdrop();
        check(Find("Dialog profile", "Duxel.AlertDialog") != 0 && Boolean(editor, 30008),
            "native alert backdrop retains the surface and its valid member focus");
        Key(0x1b);
        Wait(() => Find("Dialog profile", "Duxel.AlertDialog") == 0 && Boolean(alert, 30008), "alert Escape closes");
        check(TooltipElementUnavailable(editor) && Boolean(alert, 30008),
            "native focused Escape retires alert editor and restores its opener");

        Invoke(modal);
        Surface("Duxel.Dialog");
        Editor();
        Backdrop();
        Wait(() => Find("Dialog profile", "Duxel.Dialog") == 0 && Boolean(modal, 30008), "modal backdrop dismisses");
        check(Boolean(modal, 30008), "native modal backdrop dismisses through actual Windows pointer messages");
        Invoke(found["Finish Dialog"]);
    }
}
