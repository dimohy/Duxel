using System.Globalization;
using Duxel.Core;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryRangeCalendar(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks, nuint? nativeWindow, string? captureOutput)
    {
        const int buttonRole = 43;
        const int optionRole = 32;
        const int calendarCellRole = 56;
        const int selectedState = 23;

        var system = new UiCultureCalendar();
        var currentMonth = new UiCalendarMonth(system, DateOnly.FromDateTime(DateTime.Today));
        var previousMonth = new UiCalendarMonth(system, DateOnly.FromDayNumber(currentMonth.MonthStartDayNumber - 1));
        var first = DateOnly.FromDayNumber(previousMonth.MonthStartDayNumber + 9);
        var last = first.AddDays(2);
        using var previous = required("이전 달", window, buttonRole);
        Reveal(previous);
        InvokeNamed(previous, "click");

        using var earlier = required(system.FormatFullDate(first), window, calendarCellRole);
        using var later = required(system.FormatFullDate(last), window, calendarCellRole);
        InvokeNamed(later, "click");
        Wait(() => IsSelected(later) && !IsSelected(earlier), "the pending range endpoint");
        using var uncommitted = required("선택 : 없음", window, null);
        checks.Add("The first date is exposed as pending and does not commit a range.");

        InvokeNamed(earlier, "click");
        using var middle = required(system.FormatFullDate(first.AddDays(1)), window, calendarCellRole);
        using var summary = required(FormatRange(system), window, null);
        Wait(() => IsSelected(earlier) && IsSelected(middle) && IsSelected(later), "all dates in the normalized range");
        checks.Add("Selecting the earlier endpoint commits the ascending range and exposes its middle date as selected.");

        if (nativeWindow is { } xid)
        {
            using var next = required(system.FormatFullDate(last.AddDays(1)), window, calendarCellRole);
            Reveal(next);
            using var component = new NativeObject(atspi_accessible_get_component_iface(next.Value));
            nint error = 0;
            var focused = atspi_component_grab_focus(component.Value, &error);
            Error(ref error);
            if (focused == 0)
                throw new InvalidOperationException("Could not focus the date after the committed range.");

            const nuint enter = 0xff0d;
            const nuint escape = 0xff1b;
            using var peer = new NativePeer(xid);
            peer.KeyBurst([enter]);
            Wait(() => IsSelected(next) && !IsSelected(later), "a keyboard-started pending range");
            using var pendingSummary = required(FormatRange(system), window, null);
            checks.Add("X11 Enter starts a new range without changing the committed summary.");

            peer.KeyBurst([escape]);
            Wait(() => IsSelected(earlier) && IsSelected(middle) && IsSelected(later) && !IsSelected(next),
                "the restored committed range after X11 Escape");
            using var restoredSummary = required(FormatRange(system), window, null);
            checks.Add("X11 Escape cancels the pending range and restores the committed date cells.");
            if (captureOutput is not null)
            {
                CaptureGalleryPair(peer, captureOutput, "RangeCalendarRestored", checks);
                error = 0;
                focused = atspi_component_grab_focus(component.Value, &error);
                Error(ref error);
                if (focused == 0)
                    throw new InvalidOperationException("Could not focus the start of the new keyboard range.");

                const nuint right = 0xff53;
                first = last.AddDays(1);
                last = first.AddDays(1);
                using var nextEnd = required(system.FormatFullDate(last), window, calendarCellRole);
                peer.KeyBurst([enter, right, enter]);
                using var keyboardSummary = required(FormatRange(system), window, null);
                Wait(() => IsSelected(next) && IsSelected(nextEnd) && !IsSelected(later),
                    "the committed keyboard range and cleared previous endpoint");
                checks.Add("One ordered X11 Enter, Right, Enter sequence commits the new range.");
                CaptureGalleryPair(peer, captureOutput, "RangeCalendarKeyboardCommit", checks);
            }
        }

        using var culture = required("문화권 선택", window, buttonRole);
        Reveal(culture);
        InvokeNamed(culture, "click");
        using var thaiOption = required("태국 · 불기", window, optionRole);
        Reveal(thaiOption);
        InvokeNamed(thaiOption, "select");
        var thai = new UiCultureCalendar(CultureInfo.GetCultureInfo("th-TH"));
        using var translated = required(FormatRange(thai), window, null);
        using var thaiFirst = required(thai.FormatFullDate(first), window, calendarCellRole);
        using var thaiLast = required(thai.FormatFullDate(last), window, calendarCellRole);
        Wait(() => IsSelected(thaiFirst) && IsSelected(thaiLast), "the translated endpoints");
        checks.Add("Changing the calendar to Thai Buddhist updates the accessible text and retains both dates.");
        if (captureOutput is not null && nativeWindow is { } captureWindow)
        {
            using var peer = new NativePeer(captureWindow);
            CaptureGalleryPair(peer, captureOutput, "RangeCalendarThai", checks);
        }

        using var clear = required("선택 지우기", window, buttonRole);
        Reveal(clear);
        InvokeNamed(clear, "click");
        using var cleared = required("선택 : 없음", window, null);
        Wait(() => !IsSelected(thaiFirst) && !IsSelected(thaiLast), "the cleared selection");
        checks.Add("Clearing the range removes the accessible selection from both endpoints.");

        string FormatRange(UiCultureCalendar calendar) =>
            $"선택 : {calendar.FormatFullDate(first)} ~ {calendar.FormatFullDate(last)}";

        static bool IsSelected(NativeObject cell)
        {
            atspi_accessible_clear_cache(cell.Value);
            using var states = new NativeObject(atspi_accessible_get_state_set(cell.Value));
            return atspi_state_set_contains(states.Value, selectedState) != 0;
        }
    }
}
