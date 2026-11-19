using Duxel.Core;
using System.Globalization;

internal static class CalendarWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var system = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-US"));
        var date = new UiState<DateOnly?>(null);
        var range = new UiState<UiCalendarDateRange?>(null);
        using var single = Dux.Calendar(date.Binding, new(System : system, Today : new(2024, 3, 15)));
        using var multiple = Dux.RangeCalendar(range.Binding, new(System : system, Today : new(2024, 3, 15)));
        UiCalendarView active = single;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiVector2 Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(900, 650), new(1, 1)), input ?? idle, new(0, 0, 900, 650), UiTextSettings.Default);
            var size = default(UiVector2);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); active.Render(ui); size = ui.GetItemRectSize(); }));
            using var data = context.GetDrawData();
            return size;
        }
        UiAccessibilityElement Cell(DateOnly value) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.GridCell
            && element.Node.Name == system.FormatFullDate(value));
        UiAccessibilityResult Action(DateOnly value, UiAccessibilityAction action)
        {
            var cell = Cell(value);
            var result = tree.RequestAction(cell.Node.Id, cell.RuntimeId, action, wait : false);
            Frame(); Frame();
            return result;
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        check(Frame() == new UiVector2(296, 356), "calendar reserves a measured296by356 logical-pixel panel");
        var grid = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Grid).Node;
        check(grid.Grid == new UiAccessibilityGridInfo(7, 7)
            && tree.CaptureSnapshot().Elements.Count(element => element.Node.GridItem?.GridId == grid.Id) == 49,
            "calendar publishes a complete seven-column grid including weekday and boundary cells");
        check(tree.CaptureSnapshot().Elements.Where(element => element.Node.GridItem?.Row == 0).All(element => !element.Node.SupportsSelectionItem)
            && Cell(new(2024, 3, 15)).Node.SupportsSelectionItem,
            "only date cells advertise native SelectionItem while weekday headers remain nonselectable");
        Action(new(2024, 3, 15), UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(single.FocusedDate == new DateOnly(2024, 3, 16) && date.Value is null,
            "arrow navigation changes focus independently of selected date");
        Key(UiKey.Enter);
        check(date.Value == new DateOnly(2024, 3, 16), "Enter commits the focused single date");
        Key(UiKey.PageDown);
        check(single.FocusedDate == new DateOnly(2024, 4, 16) && date.Value == new DateOnly(2024, 3, 16),
            "PageDown navigates the month without changing the bound selection");
        Key(UiKey.Home);
        check(single.FocusedDate == new DateOnly(2024, 4, 14), "Home follows the configured start of the week");
        Key(UiKey.End);
        check(single.FocusedDate == new DateOnly(2024, 4, 20), "End focuses the configured last weekday");
        var target = Cell(new(2024, 4, 20)).Node.Bounds;
        var point = new UiVector2(target.X + target.Width / 2, target.Y + target.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true }); Frame();
        check(date.Value == new DateOnly(2024, 4, 20), "pointer release commits the actual date cell");
        active = multiple; Frame();
        Action(new(2024, 3, 20), UiAccessibilityAction.Invoke);
        check(multiple.PendingStartDate == new DateOnly(2024, 3, 20) && range.Value is null,
            "range selection retains a pending start without prematurely committing a range");
        Action(new(2024, 3, 10), UiAccessibilityAction.Invoke);
        check(range.Value == new UiCalendarDateRange(new(2024, 3, 10), new(2024, 3, 20)) && multiple.PendingStartDate is null,
            "reverse endpoint selection commits a normalized inclusive range");
        Action(new(2024, 3, 5), UiAccessibilityAction.Focus);
        Key(UiKey.Enter); Key(UiKey.Escape);
        check(multiple.PendingStartDate is null && range.Value?.Start == new DateOnly(2024, 3, 10),
            "Escape cancels only the pending range and preserves the committed value");
        range.Value = null;
        multiple.Options = multiple.Options with { Availability = new(blackouts : [new(new(2024, 3, 12), new(2024, 3, 12))]) };
        Frame();
        Action(new(2024, 3, 5), UiAccessibilityAction.Invoke);
        Action(new(2024, 3, 20), UiAccessibilityAction.Invoke);
        check(range.Value is null && multiple.PendingStartDate == new DateOnly(2024, 3, 5),
            "a range crossing an unavailable interior date is rejected without losing its start");
        var unavailable = Cell(new(2024, 3, 12)).Node;
        check(!unavailable.Enabled && unavailable.DisabledFocusable,
            "unavailable dates remain discoverable while selection is disabled");
        multiple.CancelPendingSelection();
        Action(new(2024, 3, 21), UiAccessibilityAction.Select);
        check(range.Value == new UiCalendarDateRange(new(2024, 3, 21), new(2024, 3, 21)),
            "native SelectionItem.Select replaces the range with exactly the selected date");
        Action(new(2024, 3, 25), UiAccessibilityAction.AddToSelection);
        check(range.Value == new UiCalendarDateRange(new(2024, 3, 21), new(2024, 3, 25)),
            "native AddToSelection extends a contiguous available range");
        Action(new(2024, 3, 25), UiAccessibilityAction.Deselect);
        check(range.Value?.End == new DateOnly(2024, 3, 24), "native endpoint deselection trims a range");
        Action(new(2024, 3, 22), UiAccessibilityAction.Deselect);
        check(range.Value?.End == new DateOnly(2024, 3, 24), "native interior deselection cannot silently split a contiguous range");
        var selection = new UiState<DateOnly?>(new(2024, 1, 31));
        using var preferred = Dux.Calendar(selection.Binding, new(System : system));
        active = preferred; Frame();
        preferred.NavigateMonth(1); Frame(); preferred.NavigateMonth(1); Frame();
        check(preferred.FocusedDate == new DateOnly(2024, 3, 31) && selection.Value == new DateOnly(2024, 1, 31),
            "month navigation preserves preferred day31 across February without editing selection");
        check(!context.State.HasActiveAnimations, "static calendar interaction does not schedule perpetual animation frames");
        preferred.NavigateYear(1); Frame();
        check(preferred.FocusedDate == new DateOnly(2025, 3, 31) && selection.Value == new DateOnly(2024, 1, 31),
            "year navigation preserves focus day and does not edit the selected date");
        var japanese = new UiCultureCalendar(CultureInfo.GetCultureInfo("ja-JP"), new JapaneseCalendar());
        check(japanese.TryMoveYear(new(1989, 1, 15), -1, out var eraPrevious) && eraPrevious == new DateOnly(1988, 1, 15),
            "calendar-year navigation crosses Japanese era year1 without inventing a year0");
        var availability = new UiCalendarAvailability(blackouts : [new(new(2024, 3, 10), new(2024, 3, 12)),
            new(new(2024, 3, 13), new(2024, 3, 20))]);
        check(!availability.ContainsRange(new(new(2024, 3, 1), new(2024, 3, 31)))
            && availability.ContainsDate(new(2024, 3, 21)),
            "compiled blackout intervals reject crossing ranges and preserve their first available neighbor");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
