using Duxel.Core;
using System.Globalization;

internal static class CalendarWheelTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var system = new UiCultureCalendar(CultureInfo.GetCultureInfo("ko-KR"));
        var value = new UiState<DateOnly?>(new(2024, 3, 15));
        using var calendar = Dux.Calendar(value.Binding, new(System : system, Today : new(2024, 3, 15)));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.3f, new(700, 650), new(1, 1)), input ?? idle, new(0, 0, 700, 650), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); calendar.Render(ui); }));
            using var data = context.GetDrawData();
        }
        void Invoke(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var node = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == role && item.Node.Name == name);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "calendar header/wheel accepts " + action + " for " + name);
            Frame(); Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        Frame();
        Invoke(system.FormatYear(calendar.FocusedDate));
        check(calendar.YearPicker.IsOpen && tree.CaptureSnapshot().Elements.Count(item => item.Node.Role == UiAccessibilityRole.Option) == 7,
            "year header opens a seven-row centered wheel through the shared Popover");
        Invoke(system.FormatYear(new(2025, 3, 15)), UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        check(!calendar.YearPicker.IsOpen && calendar.FocusedDate == new DateOnly(2025, 3, 15) && value.Value == new DateOnly(2024, 3, 15),
            "choosing a year changes the visible calendar without editing the selected date");
        Invoke(system.FormatMonth(calendar.FocusedDate));
        var wheel = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.ListBox).Node.Bounds;
        var pointer = new UiVector2(wheel.X + wheel.Width / 2, wheel.Y + wheel.Height / 2);
        Frame(idle with { MousePosition = pointer, MouseWheel = -1 }); Frame();
        check(calendar.MonthPicker.IsOpen && calendar.FocusedDate == new DateOnly(2025, 4, 15),
            "mouse wheel moves exactly one native month while keeping the picker open");
        Frame(idle with { MousePosition = pointer, LeftMouseDown = true, LeftMousePressed = true });
        var dragged = pointer with { Y = pointer.Y + 64 };
        Frame(idle with { MousePosition = dragged, LeftMouseDown = true });
        Frame(idle with { MousePosition = dragged, LeftMouseReleased = true }); Frame();
        check(calendar.MonthPicker.IsOpen && calendar.FocusedDate == new DateOnly(2025, 2, 15),
            "dragging the month wheel moves two rows without interpreting release as a click");
        Key(UiKey.Escape);
        check(!calendar.MonthPicker.IsOpen && value.Value == new DateOnly(2024, 3, 15),
            "Escape dismisses the period wheel while preserving the committed date");
        Invoke(system.FormatYear(calendar.FocusedDate)); Key(UiKey.DownArrow); Key(UiKey.Enter);
        check(calendar.FocusedDate.Year == 2026 && !calendar.YearPicker.IsOpen,
            "year wheel supports keyboard navigation and Enter confirmation");
        var hebrew = new UiCultureCalendar(CultureInfo.GetCultureInfo("he-IL"), new HebrewCalendar());
        calendar.Options = calendar.Options with { System = hebrew }; calendar.ShowDate(new(2024, 3, 10)); Frame();
        Invoke(hebrew.FormatMonth(calendar.FocusedDate));
        wheel = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.ListBox).Node.Bounds;
        Frame(idle with { MousePosition = new(wheel.X + 20, wheel.Y + 100), MouseWheel = -1 }); Frame();
        check(hebrew.GetParts(calendar.FocusedDate).Month == 7 && hebrew.GetParts(calendar.FocusedDate).Year == 5784,
            "Hebrew wheel advances from AdarI into AdarII without assuming twelve months");
        Key(UiKey.Escape);
        check(UiCalendarWeekend.ForCulture(CultureInfo.GetCultureInfo("ko-KR")) == UiCalendarWeekend.SaturdaySunday
            && UiCalendarWeekend.ForCulture(CultureInfo.GetCultureInfo("ar-SA")) == new UiCalendarWeekend(DayOfWeek.Friday, DayOfWeek.Saturday)
            && UiCalendarWeekend.ForCulture(CultureInfo.GetCultureInfo("fa-IR")) == new UiCalendarWeekend(DayOfWeek.Friday, DayOfWeek.Friday),
            "CLDR weekend rules distinguish Korea, Saudi Arabia and Iran independently of week starts");
        var custom = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        custom.DateTimeFormat.YearMonthPattern = "yyyy M";
        var shortPattern = new UiCultureCalendar(custom);
        check(shortPattern.FormatMonth(new(2024, 3, 15)) == "3" && shortPattern.FormatYear(new(2024, 3, 15)) == "2024",
            "single custom month tokens remain custom tokens instead of invoking standard date formats");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
