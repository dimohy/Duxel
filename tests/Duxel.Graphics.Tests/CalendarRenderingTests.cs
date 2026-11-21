using Duxel.Core;
using Duxel.Gallery;
using System.Globalization;

internal static class CalendarRenderingTests
{
    internal static void Run(string output, Action<bool, string> check, bool galleryOnly = false)
    {
        if (!galleryOnly)
        using (var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(600, 500)))
        using (var tree = new UiAccessibilityTree())
        {
            fixture.Context.SetAccessibilityTree(tree);
            var singleValue = new UiState<DateOnly?>(new(2024, 3, 15));
            var rangeValue = new UiState<UiCalendarDateRange?>(new(new(2024, 3, 10), new(2024, 3, 20)));
            using var single = Dux.Calendar(singleValue.Binding, new(Today : new(2024, 3, 15)));
            using var range = Dux.RangeCalendar(rangeValue.Binding, single.Options);
            using var singleHost = Fluent.FluentProvider(single);
            using var rangeHost = Fluent.FluentProvider(range);
            UiThemeProvider active = singleHost;
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, 600, 500), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                active.Render(ui);
            }
            byte[] Capture(string name)
            {
                fixture.Capture(Draw);
                var pixels = fixture.Capture(Draw);
                fixture.WriteCapturePng(Path.Combine(output, name + ".png"), pixels);
                return pixels;
            }
            foreach (var light in new[] { false, true })
            {
                fixture.Context.SetDesign(light ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark);
                var theme = light ? "light" : "dark";
                foreach (var locale in new[] { "ko-KR", "en-US", "en-GB", "ar-SA", "fa-IR", "he-IL", "th-TH", "ja-JP" })
                {
                    var culture = CultureInfo.GetCultureInfo(locale);
                    Calendar? calendar = locale == "he-IL" ? new HebrewCalendar() : locale == "ja-JP" ? new JapaneseCalendar() : null;
                    var system = new UiCultureCalendar(culture, calendar);
                    single.Options = single.Options with { System = system };
                    var thaiPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansThai", "NotoSansThai.ttf");
                    var thaiFonts = locale == "th-TH" ? new UiTypographyFonts(thaiPath, thaiPath, thaiPath) : null;
                    singleHost.Fonts = thaiFonts;
                    active = singleHost;
                    var pixels = Capture(theme + "-" + locale);
                    var nodes = tree.CaptureSnapshot().Elements;
                    var dates = nodes.Where(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Selected.HasValue).ToArray();
                    check(dates.Count(item => item.Node.Selected == true) == 1
                        && dates.Any(item => item.Node.Name == system.FormatFullDate(new(2024, 3, 15))),
                        theme + " " + locale + " renders the selected absolute date with localized full-date semantics");
                    check(pixels.AsSpan().SequenceEqual(fixture.Capture(Draw)) && !fixture.Context.State.HasActiveAnimations,
                        theme + " " + locale + " remains pixel-identical without perpetual frame requests");
                    if (system.RightToLeft)
                    {
                        var ordered = dates.Where(item => item.Node.GridItem?.Row == 2).OrderBy(item => item.Node.Bounds.X).ToArray();
                        check(ordered.Select(item => item.Node.GridItem!.Value.Column).SequenceEqual(Enumerable.Range(0, 7)),
                            theme + " " + locale + " native grid columns follow physical RTL placement");
                    }
                    range.Options = single.Options;
                    rangeHost.Fonts = thaiFonts;
                    active = rangeHost;
                    var band = Capture(theme + "-" + locale + "-range");
                    var expectedVisible = range.Month.Cells.ToArray().Count(cell => cell.Date is { } date && rangeValue.Value!.Value.Contains(date));
                    check(tree.CaptureSnapshot().Elements.Count(item => item.Node.Selected == true) == expectedVisible
                        && rangeValue.Value == new UiCalendarDateRange(new(2024, 3, 10), new(2024, 3, 20))
                        && !band.AsSpan().SequenceEqual(pixels),
                        theme + " " + locale + " paints exactly the visible part of its unchanged inclusive range");
                }
            }
        }
        using (var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 1000)))
        using (var tree = new UiAccessibilityTree())
        using (var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true })
        {
            fixture.Context.SetAccessibilityTree(tree);
            foreach (var route in new[] { "Calendar", "RangeCalendar" })
            {
                gallery.SelectedPage = route;
                void Draw(UiImmediateContext ui)
                {
                    ui.GetWindowDrawList().AddRectFilled(new(0, 0, 1280, 1000), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                    gallery.Render(ui);
                }
                fixture.Capture(Draw);
                var pixels = fixture.Capture(Draw);
                fixture.WriteCapturePng(Path.Combine(output, "gallery-" + route + ".png"), pixels);
                check(gallery.SelectedNavigationLabel == GalleryText.PageTitle(route)
                    && tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Grid),
                    "actual Gallery route " + route + " displays its implemented date grid");
            }
        }
        using (var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1000, 1000)))
        using (var tree = new UiAccessibilityTree())
        {
            fixture.Context.SetAccessibilityTree(tree);
            foreach (var multiple in new[] { false, true })
            {
                using var page = new CalendarPage(multiple, ObserverServices.CreateWindows().TypographyFonts);
                using var frame = new GalleryPageFrame(page);
                void Draw(UiImmediateContext ui)
                {
                    ui.EnableRootViewportContentLayout(contentPadding : 0);
                    ui.GetWindowDrawList().AddRectFilled(new(0, 0, 1000, 1000), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                    frame.Render(ui);
                }
                fixture.Capture(Draw); fixture.Capture(Draw);
                check(page.Samples.Count == 1 && page.Samples[0].Source.Contains(multiple ? "Dux.RangeCalendar(" : "Dux.Calendar("),
                    page.Title + " shows its own public factory next to its example");
                page.SelectCulture("he-IL"); fixture.Capture(Draw); fixture.Capture(Draw);
                check(page.Samples[0].Source.Contains("HebrewCalendar()") && page.Samples[0].Source.Contains("he-IL"),
                    page.Title + " source follows its actual Hebrew-calendar setting");
                var toggle = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.Button && item.Node.Name == "글 방향 전환");
                tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
                fixture.Capture(Draw); fixture.Capture(Draw);
                check(page.Samples[0].Source.Contains("RightToLeft : false"),
                    page.Title + " source follows an actual Gallery direction-toggle activation");
                var focusedName = page.Preview.Options.System!.FormatFullDate(page.Preview.FocusedDate);
                var selectedDate = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.GridCell && item.Node.Name == focusedName);
                tree.RequestAction(selectedDate.Node.Id, selectedDate.RuntimeId, UiAccessibilityAction.Select, wait : false);
                fixture.Capture(Draw); fixture.Capture(Draw);
                page.SelectCulture("th-TH"); fixture.Capture(Draw);
                var thai = fixture.Capture(Draw);
                fixture.WriteCapturePng(Path.Combine(output, "gallery-thai-" + page.Title + ".png"), thai);
                check(page.Samples[0].Source.Contains("NotoSansThai") && page.Samples[0].Source.Contains("example.Fonts"),
                    page.Title + " explicitly selects its bundled Thai font and displays the equivalent source");
                check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Label
                    && item.Node.Name.StartsWith("선택 : ", StringComparison.Ordinal)
                    && item.Node.Name.Contains(page.Preview.Options.System!.FormatFullDate(page.Preview.FocusedDate), StringComparison.Ordinal)),
                    page.Title + " retains its selected date while rendering the localized Thai selection summary");
                var blackout = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.Button && item.Node.Name == "선택 불가 날짜 전환");
                tree.RequestAction(blackout.Node.Id, blackout.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
                fixture.Capture(Draw); fixture.Capture(Draw);
                check(page.Samples[0].Source.Contains("blackouts :"),
                    page.Title + " renders its Thai blackout explanation without escaping the local font scope");
            }
        }
    }
}
