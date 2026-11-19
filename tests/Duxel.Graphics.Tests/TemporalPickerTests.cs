using Duxel.Core;
using System.Globalization;

internal static class TemporalPickerTests
{
    internal static void Run(Action<bool, string> check)
    {
        var gb = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-GB"));
        var date = new UiState<DateOnly?>(new(2024, 2, 29));
        using var picker = Dux.DatePicker("Date", date.Binding, new(System : gb));
        check(picker.Text == "29/02/2024", "DatePicker formats the absolute leap date using its declared UK culture");
        check(!picker.TrySetText("31/02/2024") && date.Value == new DateOnly(2024, 2, 29) && picker.ValidationMessage is not null,
            "invalid date text is retained without overwriting the last valid bound date");
        check(picker.TrySetText("01/03/2024") && date.Value == new DateOnly(2024, 3, 1) && picker.ValidationMessage is null,
            "valid date input updates the typed binding and clears validation");
        check(picker.TrySetText("") && date.Value is null, "clearing DatePicker produces a nullable empty date");
        var fixedDate = new DateOnly(2024, 1, 1);
        using var rejected = Dux.DatePicker("Controlled date", new UiBinding<DateOnly?>(() => fixedDate, _ => { }), new(System : gb));
        check(!rejected.TrySetText("02/01/2024") && rejected.ValidationMessage is not null,
            "DatePicker reports controlled binding refusal without pretending the value changed");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        UiWidget active = picker;
        void Frame()
        {
            context.NewFrame(new(.3f, new(900, 750), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 900, 750), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); active.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame(); picker.ShowCalendar(); picker.Calendar.ShowDate(new(2024, 3, 1)); Frame(); Frame();
        var dateNodes = tree.CaptureSnapshot().Elements;
        var dateSurface = dateNodes.Single(item => item.Node.Role == UiAccessibilityRole.Group && item.Node.Name == "Choose date").Node.Bounds;
        var dateGrid = dateNodes.Single(item => item.Node.Role == UiAccessibilityRole.Grid).Node.Bounds;
        check(picker.Calendar.EmbeddedInPopup && dateGrid.X - dateSurface.X == 8 && dateSurface.Width == 296,
            "DatePicker lays the calendar directly in a single popup surface with only the grid's native inset");
        var targetDate = new DateOnly(2024, 3, 5);
        var cell = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Name == gb.FormatFullDate(targetDate));
        tree.RequestAction(cell.Node.Id, cell.RuntimeId, UiAccessibilityAction.Invoke, wait : false); Frame(); Frame();
        check(date.Value == targetDate && picker.Text == "05/03/2024" && !picker.IsOpen,
            "DatePicker calendar activation updates the same input binding and closes its popup");
        var edit = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.Edit);
        tree.RequestAction(edit.Node.Id, edit.RuntimeId, UiAccessibilityAction.SetValue, "06/03/2024", wait : false); Frame(); Frame();
        check(date.Value == new DateOnly(2024, 3, 6), "native editable-value requests flow through DatePicker parsing");
        var time = new UiState<TimeOnly?>(new(15, 4, 5, 123));
        using var clock = Dux.TimePicker("Time", time.Binding, new(Culture : CultureInfo.GetCultureInfo("en-US")));
        check(clock.DisplayPattern.Contains("tt") && clock.TrySetText("03:09 PM") && time.Value == new TimeOnly(15, 9, 5, 123),
            "TimePicker follows12hour culture and preserves hidden seconds and fractional precision");
        check(!clock.TrySetText("25:00 PM") && time.Value == new TimeOnly(15, 9, 5, 123), "invalid time input cannot overwrite the bound time");
        clock.Options = clock.Options with { HourCycle = UiTimeHourCycle.Hours24, ShowSeconds = true };
        active = clock; Frame(); clock.ShowTime(); Frame(); Frame();
        UiAccessibilityElement CheckTimeFooter()
        {
            var nodes = tree.CaptureSnapshot().Elements;
            var surface = nodes.Single(item => item.Node.Role == UiAccessibilityRole.Group && item.Node.Name == "Choose time").Node.Bounds;
            var footer = nodes.Single(item => item.Node.Role == UiAccessibilityRole.Button && item.Node.Name == "Done");
            var done = footer.Node.Bounds;
            check(done.X - surface.X == 16 && surface.X + surface.Width - done.X - done.Width == 16
                && done.Y + done.Height <= surface.Y + surface.Height - 16,
                "TimePicker confirmation uses the actual full content width without clipping its borders");
            return footer;
        }
        CheckTimeFooter();
        var lists = tree.CaptureSnapshot().Elements.Where(item => item.Node.Role == UiAccessibilityRole.ListBox).ToArray();
        check(lists.Length == 3 && lists.All(list => tree.CaptureSnapshot().Elements.Count(item => item.Node.ParentId == list.Node.Id && item.Node.Selected == true) == 1),
            "24hour time popup exposes independent hour/minute/second wheels with exactly one selection each");
        var minutes = lists.Single(item => item.Node.Name == "Minute");
        var minute = tree.CaptureSnapshot().Elements.Single(item => item.Node.ParentId == minutes.Node.Id && item.Node.Name == "10");
        tree.RequestAction(minute.Node.Id, minute.RuntimeId, UiAccessibilityAction.Select, wait : false); Frame(); Frame();
        check(time.Value == new TimeOnly(15, 10, 5, 123) && clock.Text == "15:10:05", "native minute-wheel selection updates the time input while preserving other components");
        clock.Close(); Frame(); clock.Options = clock.Options with { HourCycle = UiTimeHourCycle.Hours12, ShowSeconds = false }; clock.ShowTime(); Frame(); Frame();
        var doneButton = CheckTimeFooter();
        check(tree.CaptureSnapshot().Elements.Count(item => item.Node.Role == UiAccessibilityRole.ListBox) == 3
            && tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Option && item.Node.Name == "PM"),
            "12hour time popup displays the separate AM/PM wheel");
        tree.RequestAction(doneButton.Node.Id, doneButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(); Frame();
        check(!clock.IsOpen, "the full-width TimePicker confirmation closes its popup");
        var combinedValue = new UiState<DateTime?>(null);
        using var combined = Dux.DateTimePicker("Appointment", combinedValue.Binding,
            new(Date : new(System : gb), Time : new(Culture : CultureInfo.GetCultureInfo("en-GB"), HourCycle : UiTimeHourCycle.Hours24)));
        check(combined.TimePicker.TrySetText("14:30") && combinedValue.Value is null,
            "DateTimePicker can retain a time draft until a date is also present");
        check(combined.DatePicker.TrySetText("15/03/2024") && combinedValue.Value == new DateTime(2024, 3, 15, 14, 30, 0, DateTimeKind.Unspecified),
            "DateTimePicker commits one wall-clock value when both date and time are present");
        RunKind(check);
        var fixedCombined = new DateTime(2024, 1, 1, 10, 0, 0);
        using var controlled = Dux.DateTimePicker("Controlled", new UiBinding<DateTime?>(() => fixedCombined, _ => { }),
            new(Date : new(System : gb), Time : new(Culture : CultureInfo.GetCultureInfo("en-GB"), HourCycle : UiTimeHourCycle.Hours24)));
        check(!controlled.TimePicker.TrySetText("11:00") && controlled.TimePicker.ValidationMessage is not null,
            "combined picker rolls back a rejected component instead of publishing a fictitious accepted draft");
        var arabic = CultureInfo.GetCultureInfo("ar-SA");
        using var arabicTime = Dux.TimePicker("Arabic", new UiState<TimeOnly?>(null).Binding,
            new(Culture : arabic, HourCycle : UiTimeHourCycle.Hours24));
        check(arabicTime.TrySetText("١٤:٣٠") && arabicTime.Value.Value == new TimeOnly(14, 30),
            "time input accepts the configured native decimal digits");
        var holidays = new UiCalendarHolidaySet([new(new(2024, 3, 6), "Fixture holiday")]);
        picker.Options = picker.Options with { Holidays = holidays };
        active = picker; picker.ShowCalendar(); Frame(); Frame();
        var holiday = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Name.Contains("Fixture holiday"));
        check(holiday.Node.Enabled && holiday.Node.Name.Contains(gb.FormatFullDate(new(2024, 3, 6))),
            "holiday annotation augments the accessible date without disabling date selection");
    }
    internal static void RunKind(Action<bool, string> check)
    {
        DateTime? utc = new DateTime(2024, 3, 15, 14, 30, 0, DateTimeKind.Utc);
        using var picker = Dux.DateTimePicker("UTC", new UiBinding<DateTime?>(() => utc, value => utc = value),
            new(Time : new(Culture : CultureInfo.GetCultureInfo("en-GB"), HourCycle : UiTimeHourCycle.Hours24)));
        picker.TimePicker.TrySetText("16:45");
        check(utc?.Kind == DateTimeKind.Utc && utc?.Hour == 16,
            "DateTimePicker preserves the existing Kind without timezone conversion");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
