using Duxel.Core;
using Duxel.Gallery;
using System.Globalization;

internal static class TemporalPickerRenderingTests
{
    internal static void RunLiveHolidayPage(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1000, 1000));
        using var tree = new UiAccessibilityTree();
        using var page = new HolidayProviderPage();
        using var frame = new GalleryPageFrame(page);
        fixture.Context.SetAccessibilityTree(tree);
        page.Preview.ShowDate(new(2026, 5, 1));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            frame.Render(ui);
        }
        fixture.Capture(Draw);
        var load = tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "현재 달 공휴일 조회");
        var activation = tree.RequestAction(load.Node.Id, load.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        check(load.Node.Enabled && activation == UiAccessibilityResult.Success,
            $"The actual Gallery retrieval button is enabled and accepts activation ({activation})");

        const string holiday = "대체공휴일(부처님 오신 날)";
        var deadline = Environment.TickCount64 + 10000;
        while (true)
        {
            var pixels = fixture.Capture(Draw);
            if (tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.GridCell
                && element.Node.Name.Contains(holiday, StringComparison.Ordinal)))
            {
                fixture.WriteCapturePng(Path.Combine(output, "live-holiday-gallery.png"), pixels);
                break;
            }
            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException("The published holiday did not reach the Gallery calendar.");
            Thread.Sleep(25);
        }
        check(true, "The published substitute holiday appears in the actual Gallery calendar");
        check(page.Samples[0].Source.Contains("new KoreanHolidayProvider(http)", StringComparison.Ordinal),
            "The displayed calendar and its live code example both use the keyless provider");
    }

    internal static void Run(string output, Action<bool, string> check, TemporalPickerKind? only = null, bool includeTail = false, bool diagnosticsOnly = false)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 1000));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var tree = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(tree);
        byte[] Capture(string name, Action<UiImmediateContext> render)
        {
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                render(ui);
            }
            fixture.Capture(Draw); fixture.Context.State.AdvanceTime(.4f);
            var pixels = fixture.Capture(Draw);
            fixture.WriteCapturePng(Path.Combine(output, name + ".png"), pixels);
            File.AppendAllText(Path.Combine(output, "capture-sizes.txt"), name + " : " + fixture.LastCaptureSize + Environment.NewLine);
            return pixels;
        }
        if (!only.HasValue && !diagnosticsOnly)
        using (var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true })
            foreach (var route in new[] { "DatePicker", "TimePicker", "DateTimePicker", "공휴일 제공자" })
            {
                gallery.SelectedPage = route; Capture("route-" + (route == "공휴일 제공자" ? "holidays" : route), gallery.Render);
                check(gallery.SelectedNavigationLabel == route && gallery.SourceCountForPage(route) == 1,
                    "actual Gallery route " + route + " displays its implemented sample and one adjacent source block");
            }
        foreach (var kind in diagnosticsOnly ? Array.Empty<TemporalPickerKind>() : only.HasValue ? new[] { only.Value } : new[] { TemporalPickerKind.Date, TemporalPickerKind.Time, TemporalPickerKind.DateTime })
        {
            using var page = new TemporalPickerPage(kind, ObserverServices.CreateWindows().TypographyFonts);
            using var frame = new GalleryPageFrame(page);
            Capture(kind + "-initial", frame.Render);
            check(page.Samples[0].Source.Contains("Dux." + kind + "Picker(") && !page.Samples[0].Source.Contains("using Duxel.Core;"),
                kind + " Gallery source starts from the public picker factory without repeated Core imports");
            if (page.Preview is UiDatePickerWidget date) date.ShowCalendar();
            else if (page.Preview is UiDateTimePickerWidget combined) combined.DatePicker.ShowCalendar();
            if (kind != TemporalPickerKind.Time)
            {
                Capture(kind + "-calendar", frame.Render);
                check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Grid && item.Node.Grid == new UiAccessibilityGridInfo(7, 7)),
                    kind + " editable field opens the real Calendar with its complete accessible grid");
                if (page.Preview is UiDatePickerWidget onlyDate) onlyDate.Close(); else ((UiDateTimePickerWidget)page.Preview).DatePicker.Close();
            }
            if (kind != TemporalPickerKind.Date)
            {
                var clock = page.Preview is UiTimePickerWidget time ? time : ((UiDateTimePickerWidget)page.Preview).TimePicker;
                clock.Options = clock.Options with { HourCycle = UiTimeHourCycle.Hours24, ShowSeconds = true };
                clock.ShowTime(); Capture(kind + "-time-wheels", frame.Render);
                File.WriteAllText(Path.Combine(output, kind + "-source.txt"), page.Samples[0].Source);
                File.WriteAllLines(Path.Combine(output, kind + "-wheels.txt"), tree.CaptureSnapshot().Elements
                    .Where(item => item.Node.Role is UiAccessibilityRole.ListBox or UiAccessibilityRole.Option)
                    .Select(item => item.Node.Role + " : " + item.Node.Name + " / " + item.Node.Selected));
                var wheelNames = new HashSet<string>(StringComparer.Ordinal) { "시", "분", "초" };
                var wheels = tree.CaptureSnapshot().Elements.Where(item => item.Node.Role == UiAccessibilityRole.ListBox && wheelNames.Contains(item.Node.Name)).ToArray();
                check(wheels.Length == 3 && wheels.All(wheel => tree.CaptureSnapshot().Elements.Count(item => item.Node.ParentId == wheel.Node.Id && item.Node.Selected == true) == 1)
                    && tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.ListBox && item.Node.Name == "표시 지역")
                    && page.Samples[0].Source.Contains("ShowSeconds : true") && page.Samples[0].Source.Contains("Hours24"),
                    kind + " hour/minute/second wheels and adjacent source follow the actual time settings");
                clock.Close();
            }
            if (kind == TemporalPickerKind.Time)
            {
                page.SelectCulture("ar-SA"); var arabicClock = (UiTimePickerWidget)page.Preview;
                arabicClock.ShowTime(); Capture("Time-rtl-wheels", frame.Render);
                var roots = tree.CaptureSnapshot().Elements.Where(item => item.Node.Role == UiAccessibilityRole.ListBox).ToArray();
                check(roots.Single(item => item.Node.Name == "시").Node.Bounds.X > roots.Single(item => item.Node.Name == "분").Node.Bounds.X,
                    "Arabic time-wheel columns follow the culture's right-to-left order");
                arabicClock.Close();
            }
            page.SelectCulture("th-TH"); Capture(kind + "-thai", frame.Render);
            check(page.Samples[0].Source.Contains("NotoSansThai") && tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Edit),
                kind + " preserves editable Thai-culture text under the explicit local font provider");
        }
        if (only.HasValue && !includeTail) return;
        using (var page = new HolidayProviderPage())
        using (var frame = new GalleryPageFrame(page))
        {
            Capture("holidays-available", frame.Render);
            if (!diagnosticsOnly) check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Name.Contains("신정")),
                "holiday names are published with the actual selectable calendar date");
            page.SimulateOutage(); Capture("holidays-offline", frame.Render);
            check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Grid)
                && !tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Name.Contains("신정")
                    || item.Node.Invalid || item.Node.Role == UiAccessibilityRole.AlertDialog),
                "unavailable holiday data removes annotations with no on-screen error and retains the functioning calendar");
        }
        var selected = new UiState<DateOnly?>(new(2024, 3, 15));
        using (var calendar = Dux.Calendar(selected.Binding, new(System : new UiCultureCalendar(CultureInfo.GetCultureInfo("ko-KR")), Today : new(2024, 3, 15))))
        {
            var pixels = Capture("calendar-weekend-alignment", calendar.Render);
            var pixelWidth = fixture.LastCaptureSize.Width;
            var system = calendar.Options.System!;
            UiRect DateBounds(DateOnly date) => tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.GridCell
                && item.Node.Name == system.FormatFullDate(date)).Node.Bounds;
            var saturday = DateBounds(new(2024, 3, 16)); var sunday = DateBounds(new(2024, 3, 17)); var monday = DateBounds(new(2024, 3, 18));
            calendar.Options = calendar.Options with { DayColors = new(Enabled : false) };
            var plain = Capture("calendar-weekends-disabled", calendar.Render);
            if (fixture.LastCaptureSize.Width != pixelWidth || plain.Length != pixels.Length) throw new InvalidOperationException("Framebuffer dimensions changed during the calendar pixel comparison.");
            bool EqualCell(UiRect rect)
            {
                for (var y = (int)rect.Y; y < rect.Y + rect.Height; y++)
                    for (var x = (int)rect.X; x < rect.X + rect.Width; x++)
                    { var at = (y * pixelWidth + x) * 4; if (!pixels.AsSpan(at, 4).SequenceEqual(plain.AsSpan(at, 4))) return false; }
                return true;
            }
            check(!EqualCell(saturday) && !EqualCell(sunday) && EqualCell(monday),
                "actual Korean calendar pixels distinguish Saturday/Sunday while preserving the unchanged weekday");
            calendar.Options = calendar.Options with { DayColors = new(Enabled : true) };
            float Center(int left, int right)
            {
                var top = 100; var bottom = -1;
                for (var y = 8; y < 40; y++) for (var x = left; x < right; x++)
                { var at = (y * pixelWidth + x) * 4; if (pixels[at] > 150 && pixels[at + 1] > 150 && pixels[at + 2] > 150) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); } }
                if (bottom < top) throw new InvalidOperationException("Calendar header ink is absent.");
                return (top + bottom) / 2f;
            }
            check(MathF.Abs(Center(12, 36) - Center(48, 248)) <= 2,
                "arrow and year/month header ink share the same vertical center within two physical pixels");
            calendar.YearPicker.Show(); Capture("calendar-year-wheel", calendar.Render);
            check(tree.CaptureSnapshot().Elements.Count(item => item.Node.Role == UiAccessibilityRole.Option && item.Node.Selected == true) == 1,
                "year wheel exposes one selected center row through the reusable wheel engine");
            calendar.YearPicker.Close(); calendar.MonthPicker.Show(); Capture("calendar-month-wheel", calendar.Render);
            check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.ListBox && item.Node.Name == "Choose month"),
                "month header opens a separate centered month wheel");
            calendar.MonthPicker.Close();
            var forced = new UiForcedColorPalette(new(0, 0, 0), new(255, 255, 255), new(128, 128, 128), new(0, 255, 0),
                new(0, 0, 0), new(0, 255, 255), new(0, 0, 0), new(255, 255, 255));
            fixture.Context.SetTheme(forced.Apply(UiCompiledDesign.Windows11Dark.Theme));
            calendar.Options = calendar.Options with { Holidays = new([new(new(2024, 3, 15), "Fixture")]) };
            pixels = Capture("calendar-forced-colors", calendar.Render);
            calendar.Options = calendar.Options with { DayColors = new(WeekendStart : new UiColor(255, 0, 255), WeekendEnd : new UiColor(0, 0, 255)),
                HolidayColor = new UiColor(0, 255, 0) };
            var overridePixels = Capture("calendar-forced-color-overrides", calendar.Render);
            check(pixels.AsSpan().SequenceEqual(overridePixels),
                "system forced colors ignore custom weekend and holiday colors with pixel-identical output");
        }
    }
}
